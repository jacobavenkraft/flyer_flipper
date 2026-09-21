# Flyer Flipper — Progress log

Last updated: 2026-09-20

This is a session-resume checkpoint. Read this first (then `PLAN.md`) to pick up where we left off.

---

## Current status

**Slice 1: DONE — approved** (commit `3f74297`).
**Slice 2: DONE — approved** (commit `5e3192b`).
**.NET 10 migration: DONE — approved** (commit `7207f3d`).
**Slice 3: DONE — approved** (commits `a9dfade`, fixes `f281bc9`).
**Slice 4a (hybrid image store refactor): DONE — approved** (commit `88128bc`).
**Slice 4b (pipeline pass-through): DONE — approved** (commit `24abec8`).
**Slice 5 (processor tabs + grayscale + resize + image scaling): DONE — approved** (commit `086a335`). Design choices: PLAN.md decision 15.
**Slice 6 (settings persistence): DONE — approved** (commit `69d1396`). Design choices: PLAN.md decision 16.
**Slice 7 (polish + Linux verification): DONE — MVP ACCEPTED by the user 2026-09-20.**
**Slice 8 (custom window chrome): COMMITTED** (`ad63d1e`) — green on both platforms and committed by
the user, but the hands-on pass (drag, the eight resize grips, maximize by button and by double-click,
minimize, close, frame comparison) has **not been reported back**. Slice 8 was added *after* MVP
acceptance, so nothing here blocks the MVP.

Old Slice 7 status line, kept for the record:
**Slice 7 (polish + Linux verification): WAS IN PROGRESS** (started 2026-09-18; resumed 2026-09-20). Linux environment: **WSL2** (user's choice), Ubuntu 26.04.1. Automated verification is **green on both platforms** — 275/275 tests on Windows and Linux, clean AOT publish for `win-x64` and `linux-x64`. The user's first Linux smoke test found two real defects (theme, window placement); both are **fixed and verified**, and it is back with the user for re-verification and MVP acceptance.

Design decided before Slice 4 (PLAN.md decision 14): hybrid store — thumbnails for all images, full-size sliding window (current ± 1) loaded only in single view, no byte budget. Original Slice 4 split into 4a (store, no behavior change) and 4b (pipeline pass-through inside the store).

Slice 6 was approved by the user on 2026-09-18; Slice 7 is under way.

**Slice 7 has NOT been approved and NOT been committed.** The MVP acceptance STOP gate is still ahead.

---

## Where the work lives

- Plan: `D:\001_source\flyer_flipper\PLAN.md` (source of truth for scope + slices + decisions).
- Code: `D:\001_source\flyer_flipper\` (git repo, branch `main`).
- Memories that constrain how I collaborate on this project: `C:\Users\jacob\.claude\projects\D--001-source\memory\MEMORY.md`. Key ones:
  - `feedback_plan_future_enhancements.md` — non-MVP items go into a "Future enhancements" section of `PLAN.md`.
  - `feedback_honor_user_selections.md` — after `AskUserQuestion` selections, honor them literally; ask a follow-up if ambiguous.
  - `feedback_per_slice_manual_verification.md` — every slice ends with a STOP gate; the user builds/runs/evaluates before I start the next slice.
- Permission allowlist: `D:\001_source\.claude\settings.json` (24 rules for `dotnet` verbs + read-only PowerShell/Bash).

---

## Decisions locked in (see PLAN.md table for full detail)

.NET 10 (was .NET 9 through Slice 2), Avalonia 11.3.22 (was 11.2.4), Fluent theme + Inter font, compiled bindings, `PublishAot=true`, `Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder` for DI, CommunityToolkit.Mvvm source generators, SkiaSharp 2.88.9 for imaging (pinned to Avalonia.Skia's version), xUnit + Moq for tests, JSON settings persistence (Slice 6). Future native-plugin story: unmanaged DLLs loaded via `NativeLibrary` + wrapped via `ComWrappers` — in-process, AOT-safe.

---

## Slice 1 — what shipped (approved)

- Solution + 6 projects per `PLAN.md` layout; `global.json` pinned SDK 9.0.0 with `latestFeature` rollForward (now 10.0.100 — see migration section).
- `Core/Layout`: `LayoutOrientation`, `ILayoutModeService`, `LayoutModeService`. `Core/Application/IApplicationShutdown`.
- `App`: composition root in `Program.cs` (`Host.CreateApplicationBuilder`, static `App.Services`), `App.axaml` Fluent theme, `AvaloniaApplicationShutdown`, `app.manifest`.
- `UI`: `MainWindow` (outer `DockPanel` with menu; inner `DockPanel` where the controls region's `DockPanel.Dock` is bound — `Top` in vertical mode, `Left` in horizontal), `MainWindowViewModel`, `AddFlyerFlipperUi()`.
- Menu: File → Exit; View → Toggle Orientation (Ctrl+Shift+O); Help → About (writes to `Trace`).
- 6 `LayoutModeService` tests.

---

## Slice 2 — what shipped

**Core** (no new dependencies):
- `Source/ImageSourceQuery` — record `(RootPath, Recursive = false, FormatWildcard = "*.jpg;*.jpeg;*.png;*.bmp;*.webp", NameWildcard = "*")`. Wildcards are `;`/`,`-separated, case-insensitive.
- `Source/ImageReference` — record `(FullPath)` + `FileName`.
- `Source/IImageSource` — `IReadOnlyList<ImageReference> Enumerate(query, ct)`; sorted by path; throws `DirectoryNotFoundException` for a missing folder.
- `Source/IImageCatalog` + `ImageCatalog` — **new abstraction not in the original plan.** Holds the current image set (`Query`, `Images`, `ImagesChanged`). `LoadAsync` enumerates on the thread pool, then replaces images and raises the event on the awaiting (UI) context. On failure/cancellation, previous images are kept. Rationale: decouples the folder input VM from the grid VM, and Slice 3 (single view) and Slice 6 (restore last folder) plug into the same seam.
- `Imaging/ImageBuffer` — plain pixel buffer (`ReadOnlyMemory<byte>`, width, height, stride, `ImagePixelFormat.Bgra8888Premultiplied`); validates dimensions, buffer length, and that stride is a whole number of pixels (Skia requires it).
- `Imaging/SourceImage` — record `(ImageReference, ImageBuffer)`. **Deviation from plan text:** the plan sketched `SourceImage` as wrapping an `SKBitmap`, which would pull SkiaSharp into Core. `PLAN.md` updated.
- `Imaging/IImageLoader` (sync, CPU-bound — callers use `Task.Run`), `ImageLoadException`.
- `Imaging/IThumbnailService` — `ImageBuffer CreateThumbnail(ImageBuffer source, int maxEdge)`.
- `Imaging/ThumbnailSizing.Fit` — aspect-preserving fit, never upscales, min 1px.

**Infrastructure:**
- `Source/FileSystemImageSource` — `Directory.EnumerateFiles` + `FileSystemName.MatchesSimpleExpression`; skips hidden/system files and inaccessible folders.
- `DependencyInjection/AddFlyerFlipperInfrastructure()`.

**Imaging** (SkiaSharp 2.88.9, `AllowUnsafeBlocks`):
- `SkiaImageLoader` — `SKCodec` decode to BGRA/premul; tolerates `IncompleteInput` (truncated files show partially); applies EXIF orientation.
- `SkiaOrientation` (internal) — undoes all 7 non-upright EXIF origins via canvas transforms.
- `SkiaThumbnailService` — pins the source buffer, wraps it zero-copy in an `SKBitmap`, `Resize(..., SKFilterQuality.High)`.
- `SkiaImageBuffers` (internal) — the only `ImageBuffer` ↔ `SKBitmap` mapping.
- `DependencyInjection/AddFlyerFlipperImaging()`.

**UI** (`AllowUnsafeBlocks`, `InternalsVisibleTo` tests):
- `ImageSourceViewModel` + `ImageSourceView` — folder `TextBox` (Enter or **Load** button), status line (image count / red error). Strips surrounding quotes from pasted paths (Explorer "Copy as path"). A new load cancels any in-flight one.
- `ThumbnailGridViewModel` + `ThumbnailGridView` — `ScrollViewer` → `ItemsControl` with `WrapPanel`.
  - Vertical app layout → `WrapPanel` rows, vertical scroll. Horizontal app layout → `WrapPanel` columns, horizontal scroll (bound to `ILayoutModeService`).
  - Code-behind maps plain mouse wheel to horizontal scrolling when vertical scrolling is disabled (Avalonia otherwise requires Shift+wheel).
  - Each cell: 180×200, spinner while loading, filename caption, path tooltip, error text on decode failure.
  - Thumbnails generated at 256px longest edge, `≤ min(4, cores/2)` concurrent decodes; a new folder load cancels outstanding work and disposes old bitmaps.
- `ThumbnailItemViewModel`, `Imaging/ImageBufferBitmap` (copies `ImageBuffer` into an Avalonia `Bitmap`).
- `MainWindowViewModel` now exposes `ImageSource` / `ThumbnailGrid` child VMs and `ControlsWidth` (fixed 300 in horizontal mode so long paths don't widen the side panel).

**App:** `Program.cs` registers `IImageCatalog → ImageCatalog`, `AddFlyerFlipperInfrastructure()`, `AddFlyerFlipperImaging()`.

**Tests:** 70 total (64 new) — `FileSystemImageSourceTests` (13), `ImageCatalogTests` (5), `ImageBufferTests`, `ThumbnailSizingTests`, `SkiaImageLoaderTests` (PNG exact pixels, hand-built BMP, JPEG, WebP, corrupt, missing, cancelled), `SkiaOrientationTests` (all 7 origins, pixel-exact), `SkiaThumbnailServiceTests`, `ImageSourceViewModelTests` (path normalization, errors, cancel-previous).

**Future enhancements added to `PLAN.md`:** large-folder thumbnail performance (virtualized layout, visible-only generation, scaled decode).

---

## Automated verification — Slice 2 (all green)

- `dotnet build -c Release`: 0 warnings, 0 errors.
- `dotnet test -c Release`: 70/70 passed.
- `dotnet publish src/FlyerFlipper.App -c Release -r win-x64 --self-contained` (AOT): clean, no warnings. ~19 MB `FlyerFlipper.App.exe`.
- Smoke test: published AOT exe launches, shows "Flyer Flipper" window, closes with exit code 0. (Thumbnail rendering in the running app not exercised by automation — part of manual verification.)

---

## .NET 10 migration — what changed

- `global.json`: SDK `9.0.0` → `10.0.100` (`latestFeature`; this box has SDK 10.0.401, runtime 10.0.12).
- All 6 projects: `net9.0` → `net10.0`.
- `Microsoft.Extensions.Hosting` / `Microsoft.Extensions.DependencyInjection.Abstractions`: 9.0.0 → 10.0.12.
- Avalonia, Avalonia.Desktop, Avalonia.Diagnostics, Avalonia.Fonts.Inter, Avalonia.Themes.Fluent: 11.2.4 → 11.3.22.
  - Why: the .NET 10 SDK audits transitive packages by default and raised **NU1903** on `Tmds.DBus.Protocol` 0.20.0 (GHSA-xrw6-gwf8-vvr9 / CVE-2026-39959: D-Bus signal spoofing, FD exhaustion, DoS; Linux only). Patched in 0.21.3; Avalonia.FreeDesktop 11.3.22 depends on exactly 0.21.3. User chose the Avalonia upgrade over pinning the package directly or deferring.
  - SkiaSharp stays 2.88.9 (Avalonia.Skia 11.3.22 still depends on it).
- Unchanged: CommunityToolkit.Mvvm 8.4.0, xUnit 2.9.2, Moq 4.20.72, Microsoft.NET.Test.Sdk 17.12.0. No source code changes were needed.

**Automated verification (all green):**
- `dotnet build -c Release` and `-c Debug`: 0 warnings, 0 errors.
- `dotnet test -c Release`: 70/70 passed on `net10.0`.
- `dotnet list package --vulnerable --include-transitive`: no vulnerable packages in any project.
- AOT publish `win-x64`: clean, no warnings; ~19.3 MB exe now at `src/FlyerFlipper.App/bin/Release/net10.0/win-x64/publish/`.
- Smoke test: published exe launches, shows "Flyer Flipper", closes with exit code 0.

**Manual verification:** user re-ran the Slice 2 checklist on the upgraded stack and approved.

---

## Slice 3 — what shipped

**Core** — `Viewport/ViewportMode` (`Grid`, `Single`), `Viewport/IViewportModeService` + `ViewportModeService`:
- State: `Mode`, `CurrentIndex` (-1 when empty), `CurrentImage`, `ImageCount`, `CanMovePrevious/Next`.
- Events: `ModeChanged`, `CurrentImageChanged` (also raised when the catalog is replaced even if the index is unchanged).
- Operations: `ShowGrid`, `ShowSingle(int? index)` (false when no images), `ToggleMode`, `MovePrevious/MoveNext` — **no wrap-around** at the ends.
- Catalog replaced → current index resets to 0; single mode is kept (shows the new folder's first image); an empty folder forces grid mode.

**UI:**
- `SingleImageViewModel` + `SingleImageView`:
  - Image fills the viewport (`Stretch=Uniform`); circular chevron overlays left/right (hidden at the ends); "‹ Grid" button top-left; bottom caption with file name and "n / N"; spinner while decoding; red error text for undecodable files.
  - Keeps **current + previous + next** decoded (`Task<Bitmap>` cache keyed by `ImageReference`), so stepping is instant after the first image; other bitmaps are evicted and disposed. All full-size bitmaps are released when returning to the grid or when the folder changes.
- `ThumbnailGridViewModel`: `OpenImageCommand` (double-click a thumbnail → single view at that image); tracks `IsCurrent` (accent border on the current thumbnail); raises `ScrollIntoViewRequested` when returning to the grid so the view scrolls to the image you were on. Hover border on thumbnails.
- `MainWindowViewModel`: `IsGridMode` / `IsSingleMode`, `ToggleViewportModeCommand` (disabled with no images), exposes `SingleImage`.
- `MainWindow`:
  - Menu **View → Toggle Viewport Mode (Grid / Single Image)**, Ctrl+Shift+V.
  - `Window.KeyBindings` now back **Ctrl+Shift+O** and **Ctrl+Shift+V** — `MenuItem.InputGesture` only displays a shortcut in Avalonia (the binding is now covered by a headless test).
  - Left / Right / Escape are handled by a tunnelling `KeyDown` handler in code-behind, not `KeyBindings`: a headless test showed window `KeyBindings` fire even while the folder `TextBox` has focus (Right would navigate images instead of moving the caret). Arrows are ignored when focus is in a `TextBox`; Escape always returns to the grid; all three are no-ops in grid mode.
- **Fix to Slice 2:** error text used `SystemFillColorCriticalBrush`, which Avalonia's Fluent theme doesn't define (rendered black). Now `SystemControlErrorTextForegroundBrush` (red) in all three views. Found by rendering screenshots headlessly.

**App:** `Program.cs` registers `IViewportModeService → ViewportModeService`; UI DI adds `SingleImageViewModel`.

**Tests:** 98 total (28 new).
- `Viewport/ViewportModeServiceTests` (12) — plain unit tests with a Moq catalog.
- New **Avalonia headless** test infrastructure (`Avalonia.Headless.XUnit` 11.3.22): `Headless/TestAppBuilder` (assembly-level `[AvaloniaTestApplication]`, Fluent theme), `Headless/AppHarness` (composes the real services + VMs + `MainWindow` over a mocked `IImageSource` and a `FakeImageLoader`).
  - `MainWindowKeyboardTests` (7) — real key presses against `MainWindow`: Ctrl+Shift+O, Ctrl+Shift+V (with/without images), arrows + Escape in single mode, arrows ignored in grid mode, arrows keep the TextBox caret, arrows navigate when the Load button has focus.
  - `ViewportViewModelTests` (8) — open from grid, neighbour pre-decoding and reuse, decode error + recovery, bitmaps released on return to grid, command enablement, folder change during single view, current-thumbnail tracking, scroll-into-view request.
- Headless screenshots (Skia renderer, scratch project outside the repo) were used to eyeball single view, error state, first-image chevrons, grid highlight, and horizontal grid.

**Future enhancements added to `PLAN.md`:** keyboard navigation inside the grid (arrows/Enter); display-sized decoding for very large images in single view.

## Automated verification — Slice 3 (all green)

- `dotnet build -c Release`: 0 warnings, 0 errors.
- `dotnet test -c Release`: 98/98 passed (run 4× — stable).
- AOT publish `win-x64`: clean, no warnings.
- Smoke test: published exe launches and closes with exit code 0.

---

## Environment quirk to remember

Native AOT publish on this box needs `vswhere.exe` on `PATH`, otherwise the linker step fails with error MSB3073 ("`vswhere.exe` is not recognized"). Fix: prepend the VS Installer folder before running `dotnet publish`:

```powershell
$env:PATH = "C:\Program Files (x86)\Microsoft Visual Studio\Installer;$env:PATH"
```

Not needed for `dotnet build` / `dotnet test` / `dotnet run` — only Native AOT publish invokes the MSVC linker.

---

## Slice 3 — fixes from manual verification

1. **Chevron glyphs not centered in their circles.** Cause: `Path` sizes itself from the geometry's origin (0,0), not its bounds, so glyphs drawn at `M 14,4 …` carried 6px/4px of empty space and rendered ~3px right / 2px low. Measured by headless Skia render (glyph bbox vs circle center): offset (3.0, 2.0) → (0.0, 0.0) after redrawing all three glyphs (both chevrons and the "‹ Grid" arrow) to start at the origin. Regression test: `Headless/SingleImageViewLayoutTests` (glyph geometry center vs button center).
2. **Selecting a thumbnail then toggling to single view showed a different image.** Cause: single-click did nothing — only double-click changed the current image. Fix: `IViewportModeService.Select(index)` (changes current image, not mode); grid view's `Tapped` → `ThumbnailGridViewModel.SelectImageCommand`. The accent border now marks the selection, and View → Toggle Viewport Mode / Ctrl+Shift+V opens it. Tests: 3 `ViewportModeServiceTests` (`Select`), `Headless/ThumbnailGridMouseTests` (real mouse: single click selects without leaving grid; click + Ctrl+Shift+V opens selected; double-click opens).

Both regression tests were confirmed to fail with the fix temporarily reverted (3 failures: chevron off by 3.0px, both selection tests), then pass with it restored. Totals: 106/106 tests; build 0 warnings; AOT publish clean.

## Manual verification checklist — Slice 3 (approved; re-used for 4a "nothing changed" check)

**What to check:**
- Load a folder. **Double-click** a thumbnail → it opens full-size in the viewport.
- **Right / Left arrows** and the **chevrons** step through images; chevrons hide at the first/last image (no wrap-around). Caption shows file name and "n / N".
- Stepping to the next/previous image is instant after the first open (neighbours are pre-decoded).
- **Escape** or the **‹ Grid** button returns to the grid, scrolled to the image you were on, which has an accent border.
- **View → Toggle Viewport Mode** / **Ctrl+Shift+V** switches grid ↔ single (opens the current image); disabled until a folder with images is loaded.
- **Ctrl+Shift+O** toggles orientation from the keyboard; single view works in both layouts.
- Click into the folder box while in single view: Left/Right move the caret, not the image.
- Load a different folder while in single view → shows that folder's first image.
- Undecodable files show red error text (grid, single view, and the folder status line).
- Review: `Core/Viewport`, `SingleImageViewModel` (neighbour cache), `MainWindow.axaml.cs` key handling, and the new headless test harness.

---

## Task snapshot (Slice 3)

| # | Status | Task |
|---|--------|------|
| 1 | completed | `ViewportMode` + `IViewportModeService` / `ViewportModeService` (Core) |
| 2 | completed | `SingleImageViewModel` + `SingleImageView` (chevrons, caption, neighbour cache) |
| 3 | completed | Grid: double-click to open, current highlight, scroll-into-view on return |
| 4 | completed | MainWindow: viewport switching, menu item, Ctrl+Shift+V/O bindings, arrow/Escape handling |
| 5 | completed | Unit tests + Avalonia headless keyboard/view-model tests (98 passing) |
| 6 | completed | Fix error brush resource key (found via headless screenshots) |
| 7 | completed | Verify: build, test, AOT publish, launch smoke test |
| 8 | completed | Hand off Slice 3 for manual verification (user committed `a9dfade`, reported 2 issues) |
| 9 | completed | Fix chevron centering + click-to-select in grid; regression tests (106 passing) |
| 10 | completed | Hand off fixes for re-verification (approved; committed `f281bc9`) |

---

## Slice 4a — what shipped

**Core — `Store/`** (no new dependencies):
- `IImageStore<TImage>` / `ImageStore<TImage>` — the single source of display images for both views.
  - `Images`, `GetThumbnail(i)`, `GetFullImage(i)` → `ImageSlot<TImage>` (`State` = `NotLoaded | Loading | Ready | Failed`, `Image`, `Error`).
  - Events: `ImagesReset`, `ThumbnailChanged(index)`, `FullImageChanged(index)`.
  - **Thumbnails:** on every catalog change a new *session* starts; `MaxConcurrentThumbnailLoads` worker loops walk the images in order (decode → `IThumbnailService` → display image). Previous session is cancelled and its images disposed. Thumbnails are kept for the session's lifetime.
  - **Full-size window:** `UpdateWindow()` reconciles with the viewport — current ± 1 in single mode, nothing in grid mode. Loads the current image first; releases indices leaving the window (cancel in-flight load, slot → `NotLoaded`, raise, then dispose). A load that finishes after release is discarded and disposed.
  - **Priority:** thumbnail workers wait (between items) while any full-size load is active.
  - **Thumbnail reuse:** a full-size load whose thumbnail isn't ready also creates the thumbnail from the same decode; thumbnail workers skip entries already ready.
  - **Event ordering:** robust to catalog/viewport/store handler order — `UpdateWindow` does nothing until the store has reset for the catalog the viewport points at.
  - **Threading:** UI-thread-affine; decoding on the thread pool; continuations return via the owning `SynchronizationContext`. Change events are raised *before* a replaced image is disposed.
- `IDisplayImageFactory<TImage>` — buffer → display image (UI implements for Avalonia `Bitmap`); the store disposes `IDisposable` images it releases.
- `ImageStoreOptions` (`ThumbnailMaxEdge` = 256, `MaxConcurrentThumbnailLoads` = clamp(cores/2, 1, 4)).
- `Imaging/ImageLoadErrors.Describe` — shared "log + user-facing message" for decode failures (moved out of both view models).

**UI:**
- `Imaging/AvaloniaBitmapFactory : IDisplayImageFactory<Bitmap>`. DI registers `ImageStoreOptions`, the factory, and `IImageStore<Bitmap> → ImageStore<Bitmap>` in `AddFlyerFlipperUi()` (the store is Core code closed over the UI's image type).
- `ThumbnailGridViewModel` — now depends on `IImageStore<Bitmap>`, `ILayoutModeService`, `IViewportModeService` only. Rebuilds items on `ImagesReset`, applies slots on `ThumbnailChanged`. Its own loading loop, cancellation, and bitmap disposal are gone.
- `ThumbnailItemViewModel` — `Apply(ImageSlot<Bitmap>)`; no longer disposes bitmaps (store owns them).
- `SingleImageViewModel` — now depends on `IViewportModeService` + `IImageStore<Bitmap>`; mirrors the current index's full-size slot. Its private neighbour cache, cancellation, and disposal logic are gone. Refreshes for both the current and the previously displayed index, so a released bitmap is never left bound.

**Tests:** 119 total (13 new).
- `Store/ImageStoreTests` — plain unit tests (no Avalonia) over the real `ImageCatalog` + `ViewportModeService`, a `GatedImageLoader` (hold/fail individual decodes, count starts), fake thumbnail service and display images: thumbnails for all + no full images in grid; window of 3 / 2 at ends; slide reuses retained images and disposes released; grid releases full images but keeps thumbnails; image alive during its release event; late load discarded; priority over thumbnails; thumbnail produced by full load (no second decode); decode failure; folder change resets/cancels/disposes; dispose releases everything.
- `TestSupport/SingleThreadedContext` — UI-thread-like pump for those tests (drops continuations posted after a test ends).
- All pre-existing headless tests pass unchanged against the store (`AppHarness` now composes it).
- Mutation checks: disabling priority, disabling thumbnail reuse, and disposing before raising the release event each fail exactly their targeted test.

## Automated verification — Slice 4a (all green)

- `dotnet build -c Release`: 0 warnings, 0 errors.
- `dotnet test -c Release`: 119/119 passed (repeated runs stable).
- AOT publish `win-x64`: clean, no warnings. Smoke test: launches, exit code 0.
- Headless screenshots (scratch project) identical in layout to Slice 3; chevron glyphs still measure (0.0, 0.0) offset.

---

## Manual verification checklist — Slice 4a (approved)

**Goal: nothing should look or behave differently from Slice 3.** Re-run the Slice 3 checklist above, paying attention to:
- Thumbnails fill in progressively; loading a second folder mid-generation switches cleanly.
- Opening a thumbnail and stepping left/right is instant after the first image; holding an arrow key through many images.
- Opening single view while a large folder is still generating thumbnails — single view should stay responsive (full-size loads now take priority).
- Returning to the grid, loading a new folder while in single view.
- Review: `Core/Store/ImageStore.cs` and the two slimmed-down view models.

---

## Task snapshot (Slice 4a)

| # | Status | Task |
|---|--------|------|
| 1 | completed | Update PLAN.md: decision 14 (hybrid store), Slice 4a/4b split, future enhancements |
| 2 | completed | Core store: `IImageStore<TImage>`, `ImageStore<TImage>`, `ImageSlot`, `IDisplayImageFactory`, `ImageStoreOptions` |
| 3 | completed | UI: `AvaloniaBitmapFactory`, DI, grid + single view models as store observers |
| 4 | completed | Store unit tests + single-threaded test context; mutation checks |
| 5 | completed | Verify: build, tests, AOT publish, smoke test, headless screenshots |
| 6 | completed | Hand off Slice 4a for manual verification (approved; committed `88128bc`) |

---

## Slice 4b — what shipped

**Core — `Pipeline/`** (no new dependencies):
- `ProcessedImage` — record: `ImageBuffer Buffer` + `IReadOnlyDictionary<string, string> Metadata`. `FromSource(SourceImage)` records `ImageMetadataKeys.SourcePath` / `SourceFileName`; `WithMetadata(key, value)` returns a copy. String-only metadata keeps it ABI-friendly for the future plugin boundary.
- `IImageProcessor` — `int Order`, `ProcessedImage Process(ProcessedImage input, CancellationToken)`. Contract: don't mutate the input buffer; must be thread-safe (called concurrently by thumbnail workers and full-size loads).
- `IImageProcessingPipeline` / `ImageProcessingPipeline` — snapshots and stable-sorts the injected processors at construction (ties keep DI registration order); checks cancellation before each step and after the last; a processor returning null → `InvalidOperationException`. No processors → returns the input instance.
- `ImageStore` now takes `IImageProcessingPipeline`; a single private `LoadAndProcess` (decode → `ProcessedImage.FromSource` → pipeline) feeds **both** the thumbnail worker and full-size loads. Pipeline exceptions surface as `Failed` slots ("Could not load image."); cancellation (folder change / window release) reaches running processors through their token.

**Imaging — `Processors/DiagnosticLoggingProcessor`:** pass-through, `Order = int.MaxValue` (logs final output), `[LoggerMessage]` source-generated `ILogger` call: `Pipeline run #N: <file> (<W>x<H>) on thread <id>`. Added `Microsoft.Extensions.Logging.Abstractions` 10.0.12 to Imaging.

**App — `Program.cs`:** registers `IImageProcessingPipeline → ImageProcessingPipeline`; `#if DEBUG` registers `DiagnosticLoggingProcessor` as an `IImageProcessor`. Release/AOT builds have zero processors (pure pass-through).

**Tests:** 139 total (20 new).
- `Pipeline/ImageProcessingPipelineTests` (10): pass-through returns same instance; runs by `Order`; ties keep registration order; output feeds next; pre-cancelled; cancelled mid-pipeline stops before next; cancelled during last still throws; token passed through; null result throws; processor list snapshot.
- `Pipeline/ProcessedImageTests` (3), `Pipeline/DiagnosticLoggingProcessorTests` (2).
- `Store/ImageStoreTests` (+5): pipeline runs exactly once per decode (= loader decodes) across thumbnails and the full-size window; once when a full-size load also produces the thumbnail; pipeline output is what both thumbnails and full images show (dimension-changing processor); processor failure → failed slots; folder change cancels the token a running processor sees.
- Mutation checks: skipping the pipeline in the thumbnail path, skipping it in the full-size path, and ignoring `Order` each fail the targeted tests; restored → all pass.

## Automated verification — Slice 4b (all green)

- `dotnet build -c Release` and `-c Debug`: 0 warnings, 0 errors.
- `dotnet test -c Release`: 139/139 passed.
- AOT publish `win-x64` (Release, no logging processor): clean, no warnings. Smoke test: launches, exit code 0.
- **End-to-end on the real Debug app** (scratch UI Automation script, 5 generated 1200×1600 PNG/JPG flyers, stdout redirected to a file): loading the folder logged runs #1–#5 (one per image, on 4 background threads); Ctrl+Shift+V into single view on image 0 logged #6 (`flyer0.png`) and #7 (`flyer1.jpg`) — the current image and its only neighbour. Exactly the expected once-per-decode behaviour.

---

## Manual verification checklist — Slice 4b (approved)

**How to build & run (Debug, so the logging processor is registered):**
```powershell
cd D:\001_source\flyer_flipper
dotnet run --project src/FlyerFlipper.App | Out-Host
```
Log lines go to standard output via the console logger (and to the IDE's Debug Output window when run under a debugger). Because the app is a GUI-subsystem exe, pipe (`| Out-Host`) or redirect (`> pipeline.log`) its output; redirecting `dotnet run`'s stdout to a file was verified to capture the lines.

**What to check:**
- UX unchanged from Slice 4a (grid, single view, navigation, orientation).
- Loading a folder of N images logs N `Pipeline run #…` lines — one per thumbnail decode.
- Entering single view logs the current image and its neighbours (up to 3 lines); stepping right logs only the newly entered neighbour; returning to the grid logs nothing.
- Loading a new folder mid-generation: remaining lines are for the new folder only.
- Review: `Core/Pipeline/*`, `ImageStore.LoadAndProcess`, `Imaging/Processors/DiagnosticLoggingProcessor.cs`, and the `#if DEBUG` registration in `Program.cs`.

---

## Task snapshot (Slice 4b)

| # | Status | Task |
|---|--------|------|
| 1 | completed | Core pipeline: `ProcessedImage`, `ImageMetadataKeys`, `IImageProcessor`, `IImageProcessingPipeline`, `ImageProcessingPipeline` |
| 2 | completed | Wire pipeline into `ImageStore` (`LoadAndProcess` for thumbnails + full-size) |
| 3 | completed | `DiagnosticLoggingProcessor` (Imaging) + Debug-only registration; pipeline DI registration |
| 4 | completed | Tests: pipeline, processed image, logging processor, store+pipeline; mutation checks |
| 5 | completed | Verify: build (Release+Debug), tests, AOT publish, smoke test, end-to-end log check via UI Automation |
| 6 | completed | Hand off Slice 4b for manual verification (approved; committed `24abec8`) |

---

## Slice 5 — what shipped

Design decisions made with the user before coding (PLAN.md decision 15): folder input above the tabs; settings apply on commit; `IProcessorControlProvider` in UI; View → Image Scaling (display only); Resize = fit inside, never enlarge; no cached originals yet. Resize *output* modes deferred to Future enhancements (possibly as a separate "Resizing" processor/plugin).

**Core:**
- `Pipeline/ProcessorSettings<TOptions>` — holder for an immutable options record: `Current` (volatile read, any thread), `Update` (no-op + no event when equal), `Changed`.
- `Pipeline/IConfigurableImageProcessor : IImageProcessor` — adds `SettingsChanged`. `ImageProcessingPipeline` forwards any configurable processor's event as `IImageProcessingPipeline.SettingsChanged` (and unsubscribes on `Dispose`).
- `Processors/GrayscaleOptions(Enabled)`, `Processors/ResizeOptions(Enabled, MaxWidth = 800, MaxHeight = 800)` (validated 1–20 000), `Processors/ProcessorOrder` (Grayscale = 100, Resize = 200).
- `Imaging/ThumbnailSizing.FitWithin(w, h, maxW, maxH)`; `Fit` now delegates to it.
- `Viewport/ViewportScaleMode` (`FitToWindow` default, `FitWithoutEnlarging`, `StretchToFill`, `ActualSize`) + `IViewportModeService.ScaleMode` / `ScaleModeChanged` / `SetScaleMode`.
- `Store/ImageStore` — **re-processing:** a processing *generation* counter bumps on `pipeline.SettingsChanged`. The full-size window reloads first (each load supersedes the previous one), then a new thumbnail pass (its own cancellation, replacing the old pass) regenerates every thumbnail not yet produced for the current generation. `ImageSlot.Refreshing(stale)` keeps the previous image visible (state `Loading`) until the replacement is ready; the stale image is disposed after the change event. Full-size loads still supply missing/stale thumbnails.

**Imaging — `Processors/`:**
- `GrayscaleProcessor` — `SKColorFilter` color matrix with Rec. 709 luma (0.2126 / 0.7152 / 0.0722), alpha preserved; shared static filter; pass-through (same instance) when disabled.
- `ResizeProcessor` — `SKBitmap.Resize(..., High)` to `FitWithin` the max box; pass-through when disabled or already inside. Only raises `SettingsChanged` when output can change (editing dimensions while disabled doesn't reprocess the folder).

**UI:**
- `Processors/IProcessorControlProvider` (`Header`, `Order`, `CreateControl()`), `GrayscaleControlProvider`, `ResizeControlProvider`.
- `ViewModels/ProcessorTabHostViewModel` (tabs sorted by `Order`, created lazily on the UI thread) + `Views/ProcessorTabHost` (`TabControl`).
- `ViewModels/Processors/GrayscaleSettingsViewModel`, `ResizeSettingsViewModel` — write options on commit; **follow external settings changes** (found via screenshots: tabs showed stale values after settings changed in code — matters for Slice 6 restore). Resize clamps/rounds values and keeps the last valid value when a field is cleared; syncing from settings doesn't write back partial combinations.
- `Views/Processors/GrayscaleSettingsView` (checkbox), `ResizeSettingsView` (checkbox + two `NumericUpDown`s). **Commit semantics:** a headless test showed `NumericUpDown` pushes its value on every keystroke, so bindings use `UpdateSourceTrigger=LostFocus` and code-behind pushes explicitly on Enter and after spins (buttons or arrow keys).
- `MainWindow` — controls region is now folder input (top) + `ProcessorTabHost`; **View → Image Scaling** submenu with four radio items (`SetScaleModeCommand`, `IsScale*` checked states).
- `SingleImageView` — image inside a `ScrollViewer`: `Stretch`/`StretchDirection` from the scale mode; scroll bars only in Actual Size. `SingleImageViewModel` exposes `ImageStretch`, `ImageStretchDirection`, `ScrollBarVisibility`.

**App — `Program.cs`:** registers `ProcessorSettings<GrayscaleOptions>` + `GrayscaleProcessor` + `GrayscaleControlProvider`, and the same trio for Resize; `DiagnosticLoggingProcessor` still Debug-only. UI DI adds `ProcessorTabHostViewModel`.

**Tests:** 196 total (57 new).
- `Pipeline/ProcessorSettingsTests` — settings holder, `ResizeOptions` validation/defaults, pipeline change forwarding + dispose, `FitWithin` cases.
- `Imaging/ProcessorImplementationTests` — grayscale: pass-through, Rec. 709 values for R/G/B/white, premultiplied alpha, input not mutated, change event; resize: pass-through, shrink keeps aspect + color, never enlarges, change event only when output can change; pipeline order grayscale → resize.
- `Store/ImageStoreTests` (+5) — settings change regenerates every thumbnail and disposes old ones; stale thumbnail stays visible until replaced; single mode reloads the window keeping the current image visible; rapid changes end on the latest settings with no leaked images; no work without images.
- `ViewModels/ProcessorSettingsViewModelTests` — apply, reflect, follow external changes without clobbering, dispose, clamping, cleared field.
- `Viewport/ViewportModeServiceTests` (+3) — scale mode default/change/independence/validation.
- Headless: `ProcessorTabsUiTests` (tab order + placement below folder input, checkbox applies immediately, typing doesn't apply until Enter, focus loss applies, spinner applies) and `ImageScalingUiTests` (each mode drives the real `Image`/`ScrollViewer`; menu radio items checked exactly for the active mode). `AppHarness` now composes the real processors, settings, and providers; its fake loader produces colored banded images.
- Mutation checks on store re-processing: dropping the stale full image, dropping the stale thumbnail, not reloading the window, and ignoring settings changes each fail targeted tests. Removing the outdated-generation check in `OfferThumbnail` is **not** caught — that guard is currently unreachable (superseded loads/passes are discarded by their cancellation checks first) and is kept as a defensive safeguard.

## Automated verification — Slice 5 (all green)

- `dotnet build -c Release` and `-c Debug`: 0 warnings, 0 errors.
- `dotnet test -c Release`: 196/196 passed.
- AOT publish `win-x64`: clean, no warnings. Smoke test (the launched process only): Release exe — now containing the real processors — launches, closes with exit code 0. Processing itself was not exercised under AOT (no further GUI automation on the user's machine).
- Headless Skia screenshots (scratch project): color grid → grayscale grid after enabling; Resize tab reflecting enabled 30×30; single view Actual Size (tiny 23×30 result) vs Fit to Window; horizontal layout with folder input + tabs in the side panel.

---

## Manual verification checklist — Slice 5 (approved)

**How to build & run:**
```powershell
cd D:\001_source\flyer_flipper
dotnet run --project src/FlyerFlipper.App
```

**What to check:**
- Controls region: folder input on top, **Grayscale** and **Resize** tabs below (both orientations).
- **Grayscale:** ticking the checkbox re-processes immediately — thumbnails update progressively (old thumbnail stays visible with a spinner until replaced); single view shows the grayscale result; unticking restores color.
- **Resize:** tick *Shrink to fit*, set max width/height. Typing a number does nothing until Enter or leaving the field; spinner arrows apply each step. Effect is easiest to see with **View → Image Scaling → Actual Size** (image shrinks to the box) — in Fit to Window it just looks softer.
- **View → Image Scaling:** Fit to Window / Fit without Enlarging / Stretch to Fill / Actual Size change only how single view displays the image (Actual Size scrolls when larger than the viewport); the checked item follows the active mode; thumbnails are unaffected.
- Changing settings while in single view: current image stays on screen until its reprocessed version replaces it; stepping left/right shows reprocessed neighbours.
- Rapidly toggling settings on a large folder: ends on the latest settings, app stays responsive.
- Review: the provider seam (`UI/Processors/*`, `ProcessorTabHost`), `ProcessorSettings<T>` + `IConfigurableImageProcessor`, the store's generation-based re-processing, and the two processors.

---

## Task snapshot (Slice 5)

| # | Status | Task |
|---|--------|------|
| 1 | completed | Design questions with user; PLAN.md decision 15, Slice 5 text, future enhancement (resize output modes) |
| 2 | completed | Core: settings holder, configurable processor + pipeline change signal, options, `FitWithin`, scale mode |
| 3 | completed | Store: generation-based re-processing keeping stale images visible |
| 4 | completed | Imaging: `GrayscaleProcessor`, `ResizeProcessor` |
| 5 | completed | UI: provider seam, tab host, settings tabs with commit semantics, Image Scaling menu, single view scaling |
| 6 | completed | Composition root registrations |
| 7 | completed | Tests (unit, store, headless UI); fixes found by tests/screenshots (NumericUpDown commit, tab VM sync); mutation checks |
| 8 | completed | Verify: builds, tests, AOT publish, smoke test, screenshots |
| 9 | completed | Hand off Slice 5 for manual verification (approved; committed `086a335`) |

---

## Slice 6 — what shipped

Design decisions made with the user before coding (PLAN.md decision 16): processor settings persisted **generically** as id-keyed JSON snippets handed to/from each processor; window size/position/maximized persisted; debounced save after each change + flush on exit; viewed image by file name (fallback: first image); missing folder shows its error and is kept; corrupt file → defaults + `settings.json.bad`; active tab by name. Image scaling mode is also persisted (recommended earlier; not explicitly discussed in the user's answers — flagged at handoff).

**Core — `Settings/`:**
- `AppSettings` record: `Version` (=1), `LastFolder`, `Orientation`, `ViewportMode`, `ViewedImageFileName`, `ScaleMode`, `ActiveProcessorTab`, `Window`, `Processors` (`IReadOnlyDictionary<string, JsonElement>` — opaque per-processor JSON).
- `ISettingsStore` (`Load` → defaults on missing/bad file; `Save`).
- `WindowPlacement(X, Y, Width, Height, IsMaximized)` (position in screen pixels, size in DIPs = normal/restore-down bounds), `ScreenArea`, `WindowPlacementRules.IsReachable` (≥120×24 px of title bar must land on a screen's working area; handles negative multi-monitor coordinates and DPI scaling) + `MinWidth/MinHeight` 600×400.
- `IWindowPlacementSource` (`Current`, `Changed`).
- `Pipeline/IConfigurableImageProcessor` gained `SettingsId`, `GetSettingsJson()`, `TryApplySettingsJson(json)`. `ProcessorSettings<T>` gained `ToJson(JsonTypeInfo<T>)` / `TryUpdateFromJson(json, JsonTypeInfo<T>)` (false and unchanged on malformed JSON or validation failure).

**Imaging:** `Processors/ProcessorOptionsJsonContext` (source-generated, camelCase) for `GrayscaleOptions`/`ResizeOptions`. `GrayscaleProcessor` id `flyerflipper.grayscale`, `ResizeProcessor` id `flyerflipper.resize` — **these ids must never change**.

**Infrastructure — `Settings/`:** `AppSettingsJsonContext` (source-generated; indented, camelCase, enum names as strings, tolerates comments/trailing commas) and `JsonSettingsStore` (default `Environment.SpecialFolder.ApplicationData/FlyerFlipper/settings.json` → `%APPDATA%` on Windows, `$XDG_CONFIG_HOME` or `~/.config` on Linux). Atomic save via `settings.json.tmp` + move. On JSON/IO/validation errors: trace, move the file to `settings.json.bad` (replacing an older one), return defaults. `AddFlyerFlipperInfrastructure(settingsFilePath)` registers it.

**UI — `Settings/`:**
- `WindowPlacementTracker : IWindowPlacementSource` — `Attach(window, saved)` before show: raises size to the minimum, positions manually if reachable else centers, applies maximized; then tracks `PositionChanged`/`ClientSize`/`WindowState`, remembering normal bounds while maximized and never saving minimized.
- `SettingsCoordinator` — phase 1 `LoadAndApplyStartupState()` (orientation, scale mode, each configurable processor's snippet by id, tab by header; starts watching), phase 2 `RestoreImagesAsync()` (folder via the normal Load command, select by file name or first, return to single view). Watches catalog, layout, viewport (mode/current image/scale), processors' `SettingsChanged`, tab selection, and window placement; saves 500 ms after the last change (`TimeProvider`-based debounce), `Flush()` on exit. Nothing is saved while restoring (saved once afterwards if anything changed). `Capture()` preserves snippets for processors not currently registered and, until a folder has loaded this session, keeps the saved folder/image/mode.
- `ProcessorTabHostViewModel` gained `SelectedHeader` and `SelectTab(header)`.

**App:** `App.axaml.cs` runs phase 1, attaches the tracker with the saved placement, runs phase 2 when the window opens, flushes on `desktop.Exit`. `Program.cs` accepts **`--settings-path <file>`** (via host configuration) to use another settings file — used for verification so the real per-user file isn't touched.

**Tests:** 250 total (54 new).
- `Settings/JsonSettingsStoreTests` — defaults when missing; full round trip; readable JSON (named enums, embedded processor objects, no temp file left); replace; 5 kinds of corrupt file → defaults + `.bad`; older `.bad` replaced; locked file doesn't throw; unknown/missing properties & comments tolerated; default path.
- `Settings/WindowPlacementRulesTests` — reachable/unreachable edges, negative-coordinate second monitor (and after unplugging it), nonsense sizes, no screens.
- `Settings/ProcessorSettingsJsonTests` — stable ids, round trips, apply raises `SettingsChanged`, missing properties use defaults, 6 invalid snippets rejected leaving settings unchanged.
- `Settings/SettingsCoordinatorTests` (fake store, `FakeTimeProvider`, single-threaded context) — startup applies layout/scale/tab/snippets; invalid snippet/unknown tab keep defaults; restore reopens folder + selects by file name + single view; missing file → first image; missing folder → error shown, folder/image/mode kept on save; debounce (one save, countdown restarts); captures folder/image/mode/window; flush only when dirty and no double save; unregistered processor entries preserved; changes during restore saved once afterwards.
- `Headless/SettingsPersistenceUiTests` — tracker applies saved size/position/maximized; off-screen → centered; tiny size raised to minimum; tracks moves/resizes/maximize, not minimized; tab selected by name shows selected; **two-session relaunch through a real settings file** restores layout, scaling, both processors (and their tab controls), tab, folder, single view on the same image, window position and width.
- Mutation checks (all caught): no debounce, image not found by name, unknown processor entries dropped, missing folder overwritten, saving during restore, bad file not set aside, placement applied without reachability check.

## Automated verification — Slice 6 (all green)

- `dotnet build -c Release`: 0 warnings, 0 errors.
- `dotnet test -c Release`: 250/250 passed.
- AOT publish `win-x64`: clean, no warnings.
- **Real AOT exe relaunch check** (scratch script, `--settings-path` to a scratch file, only the launched process driven): run 1 with no file → wrote `settings.json` with defaults, window placement, and both processor snippets; run 2 with pre-written settings → window opened at exactly (210, 160) with 900×640 client area (916×679 outer), and horizontal layout / Actual Size / Resize tab / both processor snippets survived the save on exit; run 3 with a corrupt file → started, kept `settings.json.bad` with the original text, wrote a fresh file. All exits code 0. Display scaling on this machine was 100%; >100% DPI and multi-monitor restore not exercised on real hardware.

---

## Manual verification — Slice 6 (STOP gate — awaiting user)

**How to build & run:**
```powershell
cd D:\001_source\flyer_flipper
dotnet run --project src/FlyerFlipper.App
# Settings file: %APPDATA%\FlyerFlipper\settings.json
# Optional: dotnet run --project src/FlyerFlipper.App -- --settings-path D:\temp\ff-settings.json
```

**What to check (close and relaunch after each group):**
- Load a folder, open an image in single view, toggle orientation, pick a scaling mode, enable grayscale, set resize values, select the Resize tab, move/resize the window → relaunch: all of it comes back, including the same image in single view.
- Maximize, close, relaunch → opens maximized; restore-down returns to the previous normal size/position.
- Delete or rename the viewed image file (or its folder) between runs → first image selected (or folder error shown with an empty grid; the path stays in the box).
- Put garbage in `settings.json` → app starts with defaults and `settings.json.bad` holds the garbage.
- Settings are written about half a second after a change (watch the file's timestamp), and on exit.
- If you have multiple monitors: place the window on a secondary monitor, close, disconnect it (or change arrangement), relaunch → window appears centered on an available screen.
- Review: `Core/Settings/*`, `IConfigurableImageProcessor` JSON members, `JsonSettingsStore`, `SettingsCoordinator`, `WindowPlacementTracker`, and the startup wiring in `App.axaml.cs`.

---

## Task snapshot (Slice 6)

| # | Status | Task |
|---|--------|------|
| 1 | completed | Design questions with user; PLAN.md decision 16 and Slice 6 text |
| 2 | completed | Core settings model, store interface, window placement rules; processor JSON members |
| 3 | completed | Imaging processor JSON contexts + ids; Infrastructure `JsonSettingsStore` |
| 4 | completed | UI `WindowPlacementTracker`, `SettingsCoordinator`, tab selection by name; App startup/exit wiring; `--settings-path` |
| 5 | completed | Tests (store, rules, processor JSON, coordinator with fake clock, headless tracker + relaunch); mutation checks |
| 6 | completed | Verify: build, tests, AOT publish, real AOT exe relaunch check with scratch settings |
| 7 | in_progress | Hand off Slice 6 for manual verification |

---

## Slice 7 — in progress (started 2026-09-18)

Scope per `PLAN.md § Slice 7`: polish + Linux verification + MVP acceptance.

### Commit state (as of 2026-09-20)

**Everything is committed and pushed. The working tree is clean and `main` matches `origin/main`.**

| Commit | What |
|---|---|
| `cd505b0` | Force dark theme on Linux when the OS reports none |
| `53b848d` | Window placement fixes |
| `0b87c0c` | Final cleanup — end of Slice 7 (**MVP accepted here**) |
| `ad63d1e` | Custom window chrome — Slice 8 |

> Check state with **Windows** `git status`, not with git inside WSL. The repo lives on `/mnt/d` and is
> checked out CRLF, but WSL's git has its own `core.autocrlf` setting, so from the distro it reports
> `.gitignore`, `LICENSE`, `README.md` and `flyer_flipper.sln` as modified. They are not.

### Linux environment

User chose **WSL2**. **Confirmed up and running on 2026-09-20**: default distro `Ubuntu` (26.04.1
LTS), WSL version 2, kernel 6.18.33.2-microsoft-standard-WSL2, state `Running`. WSLg is present
(`DISPLAY=:0`, `WAYLAND_DISPLAY=wayland-0`) — so the smoke test can drive a real window, and note the
**Wayland** path is the live one, which is exactly the window-placement risk flagged below.

The table below records the box's *pre-install* state on 2026-09-18, kept for history:

| Component | State on 2026-09-18 |
|---|---|
| WSL (`wsl --status`) | Not installed (only the inbox `wsl.exe` stub) |
| `Microsoft-Windows-Subsystem-Linux` feature | Present, **disabled** |
| `VirtualMachinePlatform` feature | Present, **disabled** |
| `Microsoft-Hyper-V-All` | **Enabled** |
| Docker / Podman / VirtualBox / VMware | None installed |

**The user began installing WSL2 on 2026-09-18** (`wsl --install` from an elevated prompt, then a
reboot) and the session ended there. **First thing on resume: check whether it finished.**

```powershell
wsl --status        # "not installed" means the install didn't take
wsl --list --verbose
```

The repo is reachable from the distro at `/mnt/d/001_source/flyer_flipper` — no clone needed.

**Native AOT cannot cross-compile:** `dotnet publish -r linux-x64` with `PublishAot=true` invokes the
platform linker, so the `linux-x64` publish and the Linux smoke test must both run *inside* WSL, not
from Windows.

#### Distro setup — status as of 2026-09-20

**DONE (no sudo needed).** .NET SDK **10.0.401** installed to `/home/jacob/.dotnet` via
`dotnet-install.sh --channel 10.0 --install-dir $HOME/.dotnet`. Microsoft's apt feed was skipped
deliberately — it has no Ubuntu 26.04 entry. `dotnet --list-sdks` succeeds. `global.json` pins
10.0.100 with `latestFeature`, which accepts the 10.0.4xx feature band, so `global.json` needs no
change.

`~/.dotnet` is **not** on the distro's default `PATH`, so every WSL build command needs a prefix:

```bash
export DOTNET_ROOT=$HOME/.dotnet; export PATH=$HOME/.dotnet:$PATH
```

**DONE (2026-09-20, run by the user).** `sudo` in this distro requires an interactive password, which a
non-interactive tool call cannot supply, so the user ran this themselves. Keep it that way — never try
to `sudo` from a tool call; it just fails. The command was:

```bash
sudo apt update
sudo apt install -y libicu78 clang zlib1g-dev libice6 libsm6
```

| Package | Why | State before the install |
|---|---|---|
| `libicu78` | **.NET itself will not start without ICU.** `dotnet build` dies with `Couldn't find a valid ICU package installed on the system` before any project work begins. Ubuntu 26.04's ICU soname is 78 — `libicu74`/`libicu76` do not exist here. | was missing — **hard blocker** |
| `clang`, `zlib1g-dev` | Native AOT link step. Only `/usr/bin/ld` is present; there is no `gcc` and no `cc`. | was missing |
| `libice6`, `libsm6` | Skia / Avalonia at runtime. | was missing |
| `libfontconfig1` | Skia font enumeration. | **already installed** |
| `libssl3` | TLS. | **already installed** (`libssl.so.3`) |

Do **not** work around the ICU blocker with `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`. It would let
the build run, but invariant globalization changes string casing and comparison — including the
case-insensitive wildcard and path-sort logic in `FileSystemImageSource` that this Linux pass exists
to verify. A green run under invariant mode would not be evidence of anything.

**Build artifacts.** Linux builds use `--artifacts-path /tmp/ff-linux` so the Linux `obj/bin` never
clobbers the Windows ones in the shared tree (the repo lives on `/mnt/d`, visible to both). Keep
using that flag; without it the two platforms invalidate each other's restore on every switch.

### Done so far (Windows side)

- Full suite green on Windows: **252/252** (`dotnet test -c Release`), 0 build warnings.
- `win-x64` Native AOT publish clean, exit code 0, no warnings. (Remember the `vswhere.exe` PATH
  workaround under "Environment quirk" above.)
- **Cross-platform fix — Linux Skia natives in tests.** The `SkiaSharp` metapackage ships Win32 and
  macOS natives only; `FlyerFlipper.Tests` had no Linux native asset, so every test touching the
  Imaging project would have failed on Linux with `DllNotFoundException` for `libSkiaSharp`. Added
  `SkiaSharp.NativeAssets.Linux` 2.88.9 to the test project (the app already gets it transitively via
  `Avalonia.Desktop`). Verified `runtimes/linux-x64/native/` now lands in the test output.
- **Cross-platform fix — nondeterministic enumeration order.** `FileSystemImageSource` sorted with
  `StringComparer.OrdinalIgnoreCase` alone. On a case-sensitive file system `A.jpg` and `a.jpg` can
  coexist and compare equal, and `List.Sort` is unstable, so their grid order would vary between
  runs. Extracted `FileSystemImageSource.ComparePaths` (case-insensitive, ordinal tiebreak) with
  `InternalsVisibleTo` on Infrastructure; 2 new tests.

### Audited and confirmed fine for Linux

- `JsonSettingsStore.DefaultFilePath` — `SpecialFolder.ApplicationData` resolves to `$XDG_CONFIG_HOME`
  or `~/.config` on Linux, matching the documented behaviour. Atomic save uses `File.Move(overwrite)`.
- `FileSystemImageSource` wildcard matching is explicitly case-insensitive
  (`MatchCasing.CaseInsensitive` + `MatchesSimpleExpression(ignoreCase: true)`), so `*.jpg` still
  matches `PHOTO.JPG` on Linux. `AttributesToSkip = Hidden | System` also skips dot-files there,
  since .NET maps them to `FileAttributes.Hidden` on Unix.
- No hard-coded drive letters or `\` separators anywhere in `src/`. The three Windows-looking path
  literals in tests are inert strings, not file-system operations.
- `OutputType=WinExe` is correct for a cross-platform Avalonia app (treated as `Exe` off Windows).

### Session 2026-09-20 — what was done

- Confirmed the tree still matched the uncommitted list above; `HEAD` still `69d1396`.
- Re-established the Windows baseline: `dotnet test -c Release` → **252/252 passed, 0 failed**.
- Confirmed WSL2 installed and running (Ubuntu 26.04.1, WSLg present).
- Installed .NET SDK 10.0.401 into `~/.dotnet` in the distro (no sudo required).
- User ran the `sudo apt install`; all 5 packages confirmed installed (clang 21.1.8).
- **First real Linux test run: 10 failures / 252.** All ten traced to **one** root cause — below.
- Fixed it; **Linux now 252/252**, and Windows re-verified **252/252** with the same changes.
- **`linux-x64` Native AOT publish: clean, zero warnings.** Produces a 20 MB stripped ELF PIE
  executable alongside `libSkiaSharp.so` and `libHarfBuzzSharp.so`.
- **Launch check:** the published binary ran 20 s under WSLg with empty stderr and had to be killed
  by `timeout` (exit 124) — i.e. it started and stayed up rather than crashing. This is a *liveness*
  check only; it is **not** the smoke test, which is still the user's to do.

#### Linux failure (fixed) — Windows path literals in test fixtures

10 tests failed on Linux: 5 in `ProcessorImplementationTests`, 3 in `Pipeline`, 1 in
`DiagnosticLoggingProcessorTests`, and `ImageStoreTests.ProcessorFailure_MarksSlotsFailed`.

Root cause, shared by all ten: fixtures hard-coded Windows literals such as `@"C:\flyers\gig.png"`
and handed them to `ImageReference`. `ImageReference.FileName` is `Path.GetFileName`, which on Linux
correctly treats a backslash as an ordinary file-name character — so `FileName` returned the whole
string `C:\flyers\gig.png` rather than `gig.png`, and the `SourceFileName` metadata built from it
followed. Most of the failures were a blunt string mismatch. The `ImageStoreTests` one was sneakier:
its failure-injection processor keys on `Metadata[SourceFileName] == "image01.png"`, which never
matched, so the processor never threw, the slot never reached `Failed`, and the test died on a 5 s
timeout.

**This was a test-fixture bug, not a product bug.** The 2026-09-18 audit note claiming "the three
Windows-looking path literals in tests are inert strings, not file-system operations" was **wrong** —
they are not inert, because `FileName` derives from them. `ImageReference` itself was left alone
deliberately: teaching it to split on a backslash under Linux would be incorrect, since a backslash
is a legal character in a Linux file name.

Fix: new `tests/FlyerFlipper.Tests/TestSupport/TestPaths.cs` — `TestPaths.Folder("flyers")` and
`TestPaths.File("flyers", "gig.png")`, rooted at `C:\` on Windows and `/` elsewhere via
`Path.Combine`. Converted every fixture literal that feeds `ImageReference`, `ImageSourceQuery`, or
path metadata, across 8 test files.

Deliberately **not** converted: the literals in `ImageSourceViewModelTests`. Those tests are about
trimming whitespace and stripping quotes from a path the *user typed* (Explorer "Copy as path"), so a
Windows-shaped string is the realistic input and it never reaches a path API. They pass on both
platforms as they are.

#### Linux smoke test round 1 (user, 2026-09-20) — two defects, both fixed

The user ran the published `linux-x64` binary: the app runs and functions, and window **sizing**
restores correctly. Two things were wrong.

**Defect A — the theme was light on Linux, dark on Windows.** `App.axaml` asks for
`RequestedThemeVariant="Default"`, i.e. "follow the OS". Windows answers; Linux answers through the
XDG desktop portal, and WSLg has **no portal at all** — verified directly:

```
$ dbus-send --session --dest=org.freedesktop.portal.Desktop ... Settings.Read color-scheme
Error org.freedesktop.DBus.Error.ServiceUnknown: The name org.freedesktop.portal.Desktop
was not provided by any .service files
```

Avalonia then falls back to *light*. Fixed per decision 18: new `FlyerFlipper.UI/Theming/StartupTheme.cs`
forces Dark only when nothing can be asked, detected by looking for the portal's D-Bus service file in
the XDG data directories (no D-Bus call, so startup stays synchronous and AOT-friendly). Note a session
bus **does** exist under WSLg, so `DBUS_SESSION_BUS_ADDRESS` is useless as a discriminator — the
service file is the signal that works.

**Defect B — window placement never restored.** This took real digging; throwaway Avalonia probes
under `/tmp/ffprobe` (outside the repo) established the facts, none of which reproduce on Windows:

| Probe finding | Windows | WSLg / XWayland |
|---|---|---|
| Position set *before* the window is mapped | honoured | **silently discarded** — accepted, then the WM places the window where it likes |
| `Window.Position` read back after setting it | exact, `delta=(0,0)` | **32 px short on both axes**, every time |
| Position set *after* the window is on screen | honoured | honoured |
| User drags raise `PositionChanged` | yes | yes (confirmed by the user driving a probe) |

Those two combined into the observed symptom. The pre-show position was dropped, so the window sat at
the WM's default spot, reported through the −32 offset as `-22,-22`. That got saved; on the next launch
`IsReachable` did this:

```
overlapX = min(-22+1062, 3840) - max(-22, 0) = 1040  >= 120  ✓
overlapY = min(-22+  24, 2160) - max(-22, 0) =    2  >=  24  ✗
```

— only 2 px of title bar on screen — so the placement was discarded and the window centred, which WSLg
again turned into its default spot, which re-saved `-22,-22`. Self-reinforcing, and `-22,-22` never
changed across runs. Had the reachability test not caught it, the read-back offset alone would have
walked the window −32,−32 per launch.

Fixed per decision 19, in `WindowPlacementTracker.CompleteRestore`: after the window opens, re-apply the
saved position and then re-ask until the window reports the target back (max 5 attempts, 400 ms apart),
with `_restoreTarget` pinning the tracked position so the half-settled values are never saved back.
`WindowPlacementRules.OriginCorrection` computes each next assignment.

**One trap worth recording:** the correction must measure the offset against *what was last assigned*,
not against the target. Measuring against the target oscillates — observed live, with target `56,132`:

```
tick 0  reported=-22,-22  -> assign 134,286
tick 1  reported=102,254  -> assign  10, 10
tick 2  reported=-22,-22  -> assign 134,286   (repeats until attempts run out)
```

`OriginCorrection` therefore takes both the target and the last assignment, and
`OriginCorrection_DrivenAsTheTrackerDoes_ConvergesOnTheTarget` pins the loop's convergence, `56,132`
included.

`IsReachable` was deliberately **not** changed. With the drift fixed it only fires for the case it was
written for — a saved position on a monitor that is no longer attached.

**Verified after the fix** (AOT binary, unattended, two launches per seed):

| Seed | Run 1 saved | Run 2 saved |
|---|---|---|
| `56,132` (the value that used to oscillate) | `56,132` | `56,132` |
| `500,300` | `500,300` | `500,300` |
| `1200,640` | `1200,640` | `1200,640` |

Exact round trip, no drift. A trace also confirmed the intended shape on every launch:
`reported=-22,-22 → assign target → reported=target−32 → assign target+32 → reported=target → done`,
i.e. two corrections and stop. On Windows the first tick already reports the target, so the loop makes
no assignments at all.

Suite: **275/275** on Windows and Linux (23 new tests since the fixture fix).

#### Startup position jump — fixed (user report, round 2)

With placement restored, the window was visible at the window manager's default spot for a moment
before jumping to the saved position — unavoidable given that WSLg only accepts the position *after*
the window is mapped. `WindowPlacementTracker` now sets `Opacity = 0` before the window is shown and
restores it from every exit of the restore, so the jump happens while invisible.

Probe measurements drove the timing: WSLg reveals its own position about **140 ms** after `Opened`
(not the 400 ms first guessed), so `RestoreSettleDelay` dropped to **150 ms**. The window is hidden
~450 ms on Linux (settle + two assignments) and ~150 ms on Windows, where the first check already
matches and no assignment is made.

**Testability note.** `DispatcherTimer` does not tick under `Avalonia.Headless` — neither
`Dispatcher.UIThread.RunJobs()` nor `AvaloniaHeadlessPlatform.ForceRenderTimerTick()` drives it — so a
hidden window with a timer-driven reveal was untestable, and "app never becomes visible" is too bad a
failure to leave uncovered. The tracker now takes an injectable `Action<TimeSpan, Action>` scheduler
(public parameterless constructor still uses `DispatcherTimer`, so DI is unchanged). Tests run the
steps immediately, including `Tracker_RevealsTheWindowEvenWhenThePositionNeverSticks`, which drives a
window manager that refuses every position and asserts the window is still revealed.

#### `<ApplicationManifest>` — resolved, no change needed

`src/FlyerFlipper.App/FlyerFlipper.App.csproj` sets
`<ApplicationManifest>app.manifest</ApplicationManifest>` unconditionally. The open question was
whether that needs a Windows-only condition. It does **not**: the `linux-x64` Native AOT publish
completed with zero warnings and a working binary — the SDK ignores the manifest for non-Windows
targets. Leaving it unconditioned.

### Still to do

- [x] WSL2 installed; .NET 10 SDK installed in the distro; apt dependencies installed.
- [x] `dotnet test -c Release --artifacts-path /tmp/ff-linux` inside WSL — **252/252**.
- [x] Windows suite still green with the fixture fix — **252/252**.
- [x] `dotnet publish -r linux-x64 -c Release --self-contained` (AOT) inside WSL — clean, no warnings.
- [x] `<ApplicationManifest>` question resolved — no condition needed.
- [x] Linux smoke test, first pass (user, 2026-09-20): app runs and functions; window **sizing**
      restores correctly. Found two defects — window **placement** never restored, and the theme was
      light on Linux while dark on Windows. Both fixed below.
- [x] Window placement on Linux — fixed and verified (decision 19).
- [x] Theme — fixed per the user's choice (decision 18).
- [x] Startup position jump — fixed (user report, 2026-09-20): the window was briefly visible at the
      window manager's default spot before being moved to the restored position. The window is now
      held transparent until the restore converges. Measured: WSLg settles ~140 ms after `Opened`, so
      the settle delay dropped 400 ms → 150 ms; hidden for ~450 ms on Linux, ~150 ms on Windows.
- [x] **Linux smoke test, second pass (user, 2026-09-20):** dark theme confirmed, position restores.
      **Residual, accepted:** the window *frame* still visibly jumps at startup, though the contents
      do not. `Opacity = 0` hides the client area, which Avalonia renders; under WSLg the frame is
      drawn **server-side by the compositor**, so it is not ours to hide. Ruled out: positioning
      off-screen before showing (the window manager discards any pre-map position — the same reason
      the restore has to run post-map), and mapping minimized then restoring (trades the jump for
      taskbar flicker and a restore animation). Expected to disappear in **Slice 8**: with
      `SystemDecorations="None"` there is no server-side frame, so the whole window is client area
      and `Opacity = 0` hides all of it. Re-check this after Slice 8.
- [x] **Windows re-check (user, 2026-09-20):** placement and theme both good. Binary: `/tmp/ff-linux/publish/FlyerFlipper.App/release_linux-x64/FlyerFlipper.App`
      (`/tmp` does not survive a WSL shutdown — re-publish if it has gone).
- [ ] Known cost, accepted for now: the pre-show hide buys nothing on Windows, which honours the
      pre-show position anyway, yet still costs one settle delay (~150 ms) of blank before the window
      appears. It is kept because pre-hiding is the only thing that hides the *content* jump on
      Linux — hiding after the window is already mapped would have flashed regardless. Worth
      revisiting after Slice 8: if custom chrome removes the frame jump, the hide may be shortened
      or dropped.
- [x] **Display scaling — verified by the user on Windows (2026-09-20)** at 100% and 150%, in both
      directions, including a fresh launch after each change:
      - **Position restores correctly** across a scale change. Expected: `WindowPlacement.X`/`Y` are
        stored in *screen pixels*, which do not move when the scale changes.
      - **Physical window size changes with the scale — correct, not a bug.** `Width`/`Height` are
        stored in *device-independent* pixels, so 1062×789 logical is 1062×789 physical at 100% and
        1593×1183 at 150%, and the content scales with it. Preserving *physical* size instead would
        leave a 150% window only ~708 logical pixels wide while its menu bar, folder input and
        processor tabs are laid out for 1062 — controls would clip. The mixed units in
        `WindowPlacement` are deliberate and right.
      - **View → Image Scaling → Actual Size** looks correct at both 100% and 150%.
      - Not drift-prone: earlier runs held `1062×789` byte-identical across a dozen launches.
      - Gap found and deferred, not a regression: nothing clamps a restored window *down* to the
        screen it lands on. See PLAN.md Future enhancements. Unreachable on this hardware.
      - Linux scaling not exercised. `AVALONIA_GLOBAL_SCALE_FACTOR=1.5 ./FlyerFlipper.App` forces it
        (verified to drive `RenderScaling`, `DesktopScaling` and `Screen.Scaling`);
        `AVALONIA_SCREEN_SCALE_FACTORS` and `GDK_SCALE` have no effect in Avalonia 11.3.22.
- [ ] MVP acceptance (STOP gate): user exercises the whole flow end-to-end on Windows and Linux.

---

## How to resume a fresh session

**Session ended 2026-09-20 at the Slice 7 manual-verification STOP gate.** Every automated check
in Slice 7 is green on both platforms; what is left is the user's hands-on smoke test and their
formal MVP acceptance. Do not start new work until they report back.

1. Read this file, then `PLAN.md`.
2. Read the memory index at `C:\Users\jacob\.claude\projects\D--001-source\memory\MEMORY.md`.
   The per-slice manual verification gate (`feedback_per_slice_manual_verification.md`) governs how
   Slice 7 ends: hand off, then STOP for the user's MVP acceptance.
3. Slices 1–8 are committed and pushed (`HEAD` = `ad63d1e`); the MVP was accepted on 2026-09-20 at the
   end of Slice 7. The tree is clean. Slice 8's hands-on verification pass was never reported back —
   ask about it before assuming it passed.
4. Re-establish the baseline on Windows: `dotnet test -c Release` should report **295/295**.
5. The Linux toolchain is fully set up. To confirm it survived a WSL restart:

   ```bash
   wsl -e bash -lc 'dpkg -s libicu78 clang zlib1g-dev libice6 libsm6 2>&1 | grep -c "^Status: install ok"'
   ```

   Expect `5`. If it is lower, ask the user to re-run the apt line — never try to `sudo` from a tool
   call, as this distro's `sudo` demands an interactive password and the call simply fails.
   - Every WSL dotnet call needs the `PATH` prefix:
     `export DOTNET_ROOT=$HOME/.dotnet; export PATH=$HOME/.dotnet:$PATH`
   - Every WSL build needs `--artifacts-path /tmp/ff-linux`, so Linux output does not clobber the
     Windows `obj/bin` in the shared `/mnt/d` tree.
6. The only open Slice 7 items are manual and belong to the user. If they have not yet reported
   smoke-test results, ask — do not start Slice 8 or any new work.
7. Slice 7 is committed; Slice 8 is not. Do not commit without the user asking.

### Open question for the user, carried over

1. **The Slice 7 STOP gate:** the user's re-verification and formal MVP acceptance. Nothing else in
   Slice 7 is outstanding.
2. **Slice 8 (custom window chrome, decision 20) is agreed but deliberately not started** — the user
   asked on 2026-09-20 to hold it until the Slice 7 changes are approved. Do not begin it before
   that approval.
3. Carried over, non-blocking: whether to commit the Slice 7 fixes now or as a single commit after
   acceptance. Still uncommitted as of 2026-09-20.


---

## Slice 8 — custom window chrome (implemented 2026-09-20, awaiting verification)

Per PLAN.md decision 20. Started only after the user accepted the MVP and approved the Slice 7 work.

### What shipped

- `MainWindow` sets `SystemDecorations="None"` and an explicit `Background`, so the app owns the whole
  window. Root is now a `Panel` so the resize grips can overlay the content.
- `UI/Views/CaptionBar.axaml[.cs]` — app-drawn title bar: vector app mark, window title, and
  minimize / maximize-restore / close buttons, plus drag-to-move and double-click-to-maximize.
  The maximize glyph swaps to a restore glyph, and its tooltip follows.
- `UI/Chrome/WindowChromeRules.cs` — the two decisions worth testing on their own: `ToggleMaximized`
  (full-screen counts as enlarged, so it restores down) and `ShowsResizeGrips`.
- `MainWindow` resize grips: a 3×3 `Grid` of eight transparent 5px edge/corner cells, each tagged with
  its `WindowEdge` and calling `BeginResizeDrag`. The `Grid` and its centre cell have no background, so
  clicks in the middle fall through to the content. Hidden while maximized.
- The menu bar keeps its own row below the title bar — merging it into the title bar was offered
  during planning and **not** chosen.

**Glyphs are vector `Path` geometry, not icon-font characters.** Segoe MDL2 does not exist on Linux,
so a font-based caption would have looked different on each platform, defeating the point of the slice.

### Verified

| | Windows | Linux |
|---|---|---|
| Tests | **295/295** | **295/295** |
| AOT publish | clean, no warnings | clean, no warnings |

16 new tests: `WindowChromeRulesTests` (pure) and `Headless/CaptionBarTests` (title tracking, maximize
toggle + glyph swap, minimize, close, grips hidden when maximized, all eight edges present).

**Placement round trip re-verified on Linux** — this was the regression risk, since dropping system
decorations changes the window's frame extents and the restore corrects for a frame-vs-client offset.
Seeds `640,380` and `56,132` both round-tripped exactly over two launches each, size included.

### Deliberate limitations, to raise at the gate

- **Dragging a maximized window does not tear it off and restore it down.** The OS title bar does this;
  re-implementing it well (restore under the cursor, proportional grab point) is its own piece of work.
  Today a drag on a maximized window does nothing; double-click or the button restores it.
- **No drop shadow or rounded corners on Windows.** Those came from the OS frame. Avalonia can be asked
  for them, but it is a separate styling decision.
- **Aero Snap should still work on Windows** — `BeginMoveDrag` hands off to the OS move loop — but it
  has not been verified by hand.
- The app mark is a **vector placeholder**, not artwork. Swapping in a real icon is a later task, and
  would also give the window a taskbar icon.

### Residual startup flash on Linux — investigated, accepted

After the chrome landed, the user reported the startup jump was *nearly* gone on Linux: a brief
border still flashed at the window manager's default spot before the window appeared in place.

**Root cause, identified by the user on close inspection: it is the compositor's drop shadow.** The
window itself stays invisible; what flashes is the shadow WSLg would draw around the frame, at the
surface's first position. That shadow follows the **surface geometry**, not its contents, so nothing
the app renders — or declines to render — affects it.

Two fixes were tried against it and neither worked:

| Attempt | Result |
|---|---|
| `Opacity = 0` (kept) | Hides the app's own contents. Fixed the *content* jump; the shadow remains. |
| `Background = Transparent` + `TransparencyLevelHint = [Transparent]` | **No change. Reverted.** Asking for a transparent surface does not stop the compositor drawing a shadow for it. |

The transparency attempt was reverted rather than left in: it changed nothing, and carrying two extra
properties through save/restore is real complexity for no benefit. `Opacity = 0` stays, because it
still does the job it was added for — without it the app's contents render at the wrong position first.

**Accepted as a WSLg artifact.** One further idea exists and was not pursued: map the window at a
near-zero size so its shadow is negligible, position it, then set the real size. That needs
`MinWidth`/`MinHeight` temporarily cleared and would trade the shadow flash for a window visibly
growing into place — plausibly worse, and squarely a workaround for one compositor. Worth re-checking
on a real Linux desktop before spending anything more on it; a different compositor may not draw the
shadow at all, and Windows does not.

### Resize-grip cursors on Linux — environmental, not fixable in the app

The user confirmed the grips **resize correctly on both platforms**, but on Linux no resize cursor
appears on hover. Because there is no OS frame any more, nothing else hints that an edge is grabbable.

First hypothesis (**wrong**): a missing cursor theme. It fitted the edges neatly — Avalonia.X11 holds no
cursor-name strings, so it calls `XCreateFontCursor` with numeric ids that Xcursor remaps through the
active theme, and Adwaita ships no `left_side` / `right_side` / `top_side` / `bottom_side`, which is
exactly what the edge grips asked for. But it never explained the **corners**, whose names
(`top_left_corner` and friends) Adwaita does ship, and which showed no cursor either.

**Actual cause, established by one check:** hovering the folder-path `TextBox` does not produce an
I-beam either. That is a stock Avalonia control with a built-in cursor, nothing to do with this slice.
**No cursor change of any kind reaches the display under WSLg.** It composites each Linux window into a
Windows window and does not propagate X cursor changes. Nothing in the app can affect this, and no apt
package helps — `adwaita-icon-theme` and `libxcursor1` were already installed and the theme resolves.
Had theme lookup been the problem, X would fall back to its built-in cursor font and show *different*
shapes, not no change at all.

While chasing the wrong hypothesis the edge grips were switched to `SizeWestEast` / `SizeNorthSouth`,
with a test pinning all eight cursors. **Both were reverted at the user's request** once the real cause
was known: the change fixed nothing observable — Windows renders the two shapes identically and Linux
shows neither — so it was a diff with no demonstrated effect. The grips keep the `TopSide` / `LeftSide`
/ `RightSide` / `BottomSide` shapes they shipped with in `ad63d1e`.

Worth knowing if this is ever revisited: `Cursor` has no value equality, so two cursors built from the
same `StandardCursorType` compare unequal — a test has to compare `ToString()`.

### What WSLg cannot verify

Two Slice 8 defects on Linux both turned out to be WSLg compositor behaviour rather than app bugs:
the **startup shadow flash** and **absent cursor feedback**. Neither reproduces on Windows, and both
are plausibly absent on a real Linux desktop.

The conclusion worth carrying forward: **WSLg is good enough to verify the app's logic on Linux —
rendering, file system, settings, placement round trips — but not its window-manager or compositor
behaviour.** If chrome fidelity on Linux matters before shipping, it needs a real desktop session: a
Hyper-V Linux VM, which decision 17 already names as the fallback and which is available on this box.

### Still to do

- [x] Resize grips: confirmed working from all edges and corners on both platforms (user, 2026-09-20).
- [x] Resize cursors on Linux: **not fixable in the app** — WSLg propagates no cursor changes at all
      (a stock `TextBox` shows no I-beam either). Recorded as an environment limitation.
- [ ] **Manual verification (STOP — user; committed but never reported back):** drag by the title bar; maximize and restore by both the button and a double-click; minimize; close; confirm
      the frame looks the same on Windows and Linux.
- [ ] Confirm the residual Linux **startup frame jump is gone** — with no server-side frame the whole
      window is client area, which `Opacity = 0` hides. This was the main reason the slice was wanted.
- [ ] Confirm the menu bar, keyboard shortcuts, and the viewport still behave under the new root layout.
- [ ] If the frame jump is gone, revisit whether the pre-show hide still needs a full settle delay on
      Windows (it costs ~150 ms there and buys nothing).


---

## Slice 9 — flip and rotate processors (implemented 2026-09-20, awaiting verification)

Per PLAN.md decision 21. First work after MVP acceptance.

The user asked for "another image processing plugin". Built as **in-process processors**, like grayscale
and resize — the native plugin host (decisions 10–12) is still a future enhancement, and that reading
was flagged to the user rather than assumed silently.

### What shipped

| Layer | Flip | Rotate |
|---|---|---|
| Options | `FlipOptions(Enabled, MirrorHorizontally, MirrorVertically)` | `RotateOptions(enabled, angle)` + `RotationAngle` |
| Processor | `FlipProcessor` | `RotateProcessor` |
| UI | `FlipSettingsView` + view model + control provider | same shape, radio buttons |
| Settings id | `flyerflipper.flip` | `flyerflipper.rotate` |

Nothing had to change in the settings plumbing: `SettingsCoordinator` takes `IEnumerable<IImageProcessor>`
and filters to the configurable ones, so both persist automatically (decision 16 paying off).

### Decisions worth remembering

- **Default pipeline order is grayscale → flip → rotate → resize** — a *default*, not a guarantee.
  Geometry before resize means the resize box applies to the final orientation rather than being undone
  by a later quarter turn swapping the sides; flip before rotate because the two do not commute and a
  default has to pick one. `DefaultOrder_PutsGeometryBeforeResize` pins the shipped values.
  **Correction (user, 2026-09-20):** the first version of the rotate tab said "Rotation is applied
  before shrink-to-fit, so the size limits apply to the final orientation", which presents a default as
  a property of the app. Reordering the pipeline is a planned enhancement, and a different order
  produces real, sometimes desirable, visual results — resize-then-rotate is a legitimate thing to want.
  That sentence was removed, and the doc comments on `ProcessorOrder` and `RotateOptions` now say
  "default" explicitly. **No UI text should imply the order is fixed.**
- **UI wording avoids "flip horizontal"**, which is read both ways — as the mirror line or as the
  direction pixels move. The checkboxes say "Mirror left ↔ right" and "Mirror top ↕ bottom".
- **The rotation angle is validated, not merely typed.** `System.Text.Json` deserializes *any* number
  into an enum, so a settings file reading `"angle": 42` would otherwise have produced a real 42°
  rotation with clipped corners — the `switch` in `Process` has no arm for it, and `RotateDegrees(42)`
  would still have run. `RotateOptions` now rejects undefined values in its constructor, which
  `TryUpdateFromJson` already turns into a clean "rejected, settings unchanged". Found by a test written
  to assert the rejection; it failed, which is how the hole surfaced.
- Angles serialize as **names** (`"Clockwise270"`), via `UseStringEnumConverter` to match
  `AppSettingsJsonContext`. A hand-edited settings file stays readable and reordering the enum cannot
  silently change what a saved file means.
- Both processors suppress `SettingsChanged` when a change cannot alter output — toggling an axis while
  flipping is off, or changing the angle while rotation is off — so the folder is not reprocessed for
  nothing. Same pattern as `ResizeProcessor`.

### Test harness drift, fixed

`AppHarness` claims to compose "the real services and view models (as `Program.cs` does)", but it had its
own processor list, so the new tabs would have had **no UI coverage at all** while the headless tests
still looked green. Added flip and rotate to the harness's pipeline and tab host, deliberately
registering the providers out of order so the host's `Order` sort is exercised.

Six existing tests then failed because they hard-coded `["Grayscale", "Resize"]` and tab **index 1**.
They now select tabs **by name**, so the next processor does not break them again.

### Verified

| | Windows | Linux |
|---|---|---|
| Tests | **329/329** | **329/329** |
| AOT publish | clean, no warnings | clean, no warnings |

25 new tests. `GeometryProcessorTests` builds an image whose every pixel encodes its own coordinates, so
the assertions pin exactly where each pixel lands — not merely that the output is the right size. They
cover both mirror axes, all three angles, that a quarter turn swaps width and height, that flipping both
axes equals a 180° turn, and that four 90° turns return the original.

### Still to do

- [ ] **Manual verification (STOP — user):** exercise both tabs on a real folder. Confirm the rotation
      direction matches expectations, the flip axes are the right way round, a quarter turn visibly swaps
      the image's dimensions, and both settings survive a restart.
- [ ] Sanity-check the interaction with resize: rotate 90° with shrink-to-fit on and a non-square box
      (e.g. 800×400) and confirm the result respects the box in its final orientation — i.e. that the
      default order behaves as intended.
- [ ] Confirm no remaining UI text implies the pipeline order is fixed.


---

## Slice 10 — channel map processor (implemented 2026-09-20, awaiting verification)

Per PLAN.md decision 22. A "Channels" tab where each output colour channel reads from any source channel.

### Shape of the options

The request was phrased source → destination(s) ("red can be assigned to blue"; "each channel should be
able to be assigned to one or more other channels"). Modelled the other way round — **destination ←
source, exactly one source per destination**. Identical expressive power (blue into all three is
`Red = Blue, Green = Blue, Blue = Blue`), but a destination with *two* sources, which has no meaning,
cannot be represented at all. Flagged to the user when built rather than silently reinterpreted.

No combination is rejected. Collapsing every channel onto one is a legitimate request and the result is
supposed to look like that.

### Why bytes, not a colour matrix

`GrayscaleProcessor` uses an `SKColorFilter` colour matrix, so that was the obvious route. It would have
been wrong here: **Skia applies colour matrices to unpremultiplied colours.** The buffers are BGRA
*premultiplied*, so a matrix divides by alpha and multiplies back — rounding every partially transparent
pixel, and destroying colour entirely where alpha is 0. Moving whole channels needs no arithmetic at all:
every channel in a premultiplied pixel carries the same alpha factor, so copying one over another stays
premultiplied and stays exact. `LeavesAlphaAlone_AndDoesNotRoundPartiallyTransparentPixels` pins that
with an alpha of 17.

Two details the byte loop has to get right, both covered: all three source channels are read *before* any
is written (a rotate like `R←G, G←B, B←R` reads channels it also overwrites), and rows are walked by
**stride**, not by width × 4, so padded buffers are not shifted.

### Default order

`ChannelMap` is 50, i.e. **before** grayscale, so the two compose: rearranging channels changes which
colours dominate the resulting luma. After grayscale every channel is already equal and a remap would do
nothing at all. A default, not a rule — same framing as decision 21.

Consequence worth noting: **"Channels" is now the first tab**, where "Grayscale" used to be.

### Test fragility found and fixed

Two existing tests located the folder path box with `OfType<TextBox>().Single()` and a
`FindAncestorOfType<NumericUpDown>() is null` filter — both of which quietly depended on there being
exactly one or two text boxes in the whole window. The new tab's combo boxes broke that. Rather than
patch the filters, the folder box is now **named** (`x:Name="FolderPathInput"`) and both tests look it up
by name. `GrayscaleCheckbox_AppliesImmediately` also had to stop assuming grayscale is the selected tab.

### Verified

| | Windows | Linux |
|---|---|---|
| Tests | **353/353** | **353/353** |
| AOT publish | clean, no warnings | clean, no warnings |

24 new tests, including a red/blue swap, a three-way rotate, one source feeding all three outputs, the
partially-transparent exactness check, a padded-stride image, JSON round trip with channels written as
names, and rejection of out-of-range channel numbers.

### Still to do

- [ ] **Manual verification (STOP — user):** exercise the Channels tab on a real folder — swaps look
      right, one source can feed all three, settings survive a restart.
- [ ] Confirm the tab order change (Channels first) is acceptable.

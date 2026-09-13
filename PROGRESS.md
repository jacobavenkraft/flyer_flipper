# Flyer Flipper — Progress log

Last updated: 2026-09-13

This is a session-resume checkpoint. Read this first (then `PLAN.md`) to pick up where we left off.

---

## Current status

**Slice 1: DONE — approved** (commit `3f74297`).
**Slice 2: DONE — approved** (commit `5e3192b`).
**.NET 10 migration: DONE — approved** (commit `7207f3d`).
**Slice 3: DONE — approved** (commits `a9dfade`, fixes `f281bc9`).
**Slice 4a (hybrid image store refactor): code + automated verification DONE; manual verification (STOP gate) PENDING USER.** Uncommitted.

Design decided before Slice 4 (PLAN.md decision 14): hybrid store — thumbnails for all images, full-size sliding window (current ± 1) loaded only in single view, no byte budget. Original Slice 4 split into 4a (store, no behavior change) and 4b (pipeline pass-through inside the store).

Do not begin Slice 4b until the user explicitly approves Slice 4a.

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

## Manual verification — Slice 4a (STOP gate — awaiting user)

**How to build & run:**
```powershell
cd D:\001_source\flyer_flipper
dotnet run --project src/FlyerFlipper.App
```

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
| 6 | in_progress | Hand off Slice 4a for manual verification |

---

## What's next — Slice 4b preview (do not start until Slice 4a approved)

From `PLAN.md § Slice 4b`:
- Define `IImageProcessor` (`ProcessedImage Process(ProcessedImage input, CancellationToken ct)`, `Order`), `IImageProcessingPipeline`, `ProcessedImage` (carries an `ImageBuffer` + metadata).
- Default pipeline runs injected `IEnumerable<IImageProcessor>` in `Order` sequence; pass-through with none registered.
- Wire the pipeline inside `ImageStore` between decode and display-image creation (both the thumbnail worker and full-size loads), so both views get processed images.
- Debug-only logging processor proving it runs per load.
- Tests: pipeline ordering, pass-through, cancellation; store invokes pipeline once per decode.

---

## How to resume a fresh session

1. Read this file, then `PLAN.md`.
2. Read the memory index at `C:\Users\jacob\.claude\projects\D--001-source\memory\MEMORY.md`.
3. Check whether the user has approved Slice 4a. If not, ask.
4. Slice 4b begins only after that approval.

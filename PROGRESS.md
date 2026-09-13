# Flyer Flipper — Progress log

Last updated: 2026-09-12

This is a session-resume checkpoint. Read this first (then `PLAN.md`) to pick up where we left off.

---

## Current status

**Slice 1: DONE — verified and approved by the user** (commit `3f74297`).
**Slice 2: DONE — verified and approved by the user** (not yet committed at time of writing).
**.NET 10 migration (user-requested, between Slices 2 and 3): code + automated verification DONE; manual verification PENDING USER.**

Do not begin Slice 3 until the user confirms the app still behaves correctly on .NET 10 / Avalonia 11.3.22.

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

**Manual verification (awaiting user):** re-run the Slice 2 checklist below on the upgraded stack — Avalonia 11.2 → 11.3 is a minor-version UI framework bump, so look for any visual/behavioral differences (menu, folder box, thumbnail grid, orientation toggle, wheel scrolling).

---

## Environment quirk to remember

Native AOT publish on this box needs `vswhere.exe` on `PATH`, otherwise the linker step fails with error MSB3073 ("`vswhere.exe` is not recognized"). Fix: prepend the VS Installer folder before running `dotnet publish`:

```powershell
$env:PATH = "C:\Program Files (x86)\Microsoft Visual Studio\Installer;$env:PATH"
```

Not needed for `dotnet build` / `dotnet test` / `dotnet run` — only Native AOT publish invokes the MSVC linker.

---

## Manual verification checklist — Slice 2 (approved on .NET 9; re-run on .NET 10)

**How to build & run:**
```powershell
cd D:\001_source\flyer_flipper
dotnet run --project src/FlyerFlipper.App
```

**What to check:**
- Paste a folder path (quotes OK) into the **Image folder** box; press Enter or click **Load**.
- Status shows the image count; thumbnails appear progressively (spinners first) for JPG/PNG/BMP/WebP files in that folder (not sub-folders).
- Phone photos with EXIF rotation appear upright.
- A bad path shows a red error; the previous grid stays.
- Loading a second folder replaces the grid promptly (in-flight work is cancelled).
- **View → Toggle Orientation**: vertical layout = rows scrolling down; horizontal layout = columns scrolling right (mouse wheel scrolls sideways).
- Review the imaging abstractions: `Core/Source`, `Core/Imaging`, `Imaging/*`, and the new `IImageCatalog` seam.

---

## Task snapshot (Slice 2)

| # | Status | Task |
|---|--------|------|
| 1 | completed | Core abstractions: query, reference, source, catalog, buffer, loader, thumbnail service |
| 2 | completed | `FileSystemImageSource` (Infrastructure) |
| 3 | completed | `SkiaImageLoader` + EXIF orientation, `SkiaThumbnailService` (Imaging) |
| 4 | completed | Folder input view/VM, thumbnail grid view/VM, orientation-bound scrolling |
| 5 | completed | DI wiring in `Program.cs` |
| 6 | completed | xUnit tests (70 passing) |
| 7 | completed | Verify: build, test, AOT publish, launch smoke test |
| 8 | completed | Hand off Slice 2 for manual verification (approved) |
| 9 | completed | Migrate to .NET 10 + Avalonia 11.3.22; verify build, test, audit, AOT publish |
| 10 | in_progress | Hand off .NET 10 migration for manual verification |

---

## What's next — Slice 3 preview (do not start until the .NET 10 migration is confirmed)

From `PLAN.md § Slice 3`:
- Menu **View → Viewport Mode** toggles grid ↔ single.
- `IViewportModeService` in Core (mode + current image index); single view observes `IImageCatalog`.
- Single-image view fills the viewport with left/right chevron overlays.
- Keyboard: Left/Right navigate, Escape returns to grid. (Likely also: double-click a thumbnail to open it — confirm with user.)

---

## How to resume a fresh session

1. Read this file, then `PLAN.md`.
2. Read the memory index at `C:\Users\jacob\.claude\projects\D--001-source\memory\MEMORY.md`.
3. Check whether the user has confirmed the .NET 10 migration. If not, ask.
4. Slice 3 begins only after that confirmation.

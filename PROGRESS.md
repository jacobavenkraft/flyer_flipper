# Flyer Flipper — Progress log

Last updated: 2026-09-12

This is a session-resume checkpoint. Read this first (then `PLAN.md`) to pick up where we left off.

---

## Current status

**Slice 1 code + automated verification: DONE.**
**Slice 1 manual verification (STOP gate): PENDING USER.**

Do not begin Slice 2 until the user explicitly approves after building and running Slice 1 locally.

---

## Where the work lives

- Plan: `D:\001_source\flyer_flipper\PLAN.md` (source of truth for scope + slices + decisions).
- Code: `D:\001_source\flyer_flipper\` (git repo, currently on `master`, no commits yet).
- Memories that constrain how I collaborate on this project: `C:\Users\jacob\.claude\projects\D--001-source\memory\MEMORY.md`. Key ones:
  - `feedback_plan_future_enhancements.md` — non-MVP items go into a "Future enhancements" section of `PLAN.md`.
  - `feedback_honor_user_selections.md` — after `AskUserQuestion` selections, honor them literally; ask a follow-up if ambiguous.
  - `feedback_per_slice_manual_verification.md` — every slice ends with a STOP gate; the user builds/runs/evaluates before I start the next slice.
- Permission allowlist: `D:\001_source\.claude\settings.json` (24 rules for `dotnet` verbs + read-only PowerShell/Bash).

---

## Decisions locked in (see PLAN.md table for full detail)

.NET 9, Avalonia 11.2.4, Fluent theme + Inter font, compiled bindings, `PublishAot=true`, `Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder` for DI, CommunityToolkit.Mvvm source generators, SkiaSharp for imaging (comes in later slices), xUnit + Moq for tests, JSON settings persistence (Slice 6). Future native-plugin story: unmanaged DLLs loaded via `NativeLibrary` + wrapped via `ComWrappers` — in-process, AOT-safe.

---

## Slice 1 — what shipped

**Solution layout at `D:\001_source\flyer_flipper\`:**
- `flyer_flipper.sln`, `global.json` (pins SDK 9.0.0 with `latestFeature` rollForward), `.gitignore`, `PLAN.md`, `PROGRESS.md` (this file).
- `src/FlyerFlipper.Core/` — pure abstractions, `<IsAotCompatible>true</IsAotCompatible>`.
  - `Layout/LayoutOrientation.cs`, `Layout/ILayoutModeService.cs`, `Layout/LayoutModeService.cs`.
  - `Application/IApplicationShutdown.cs`.
- `src/FlyerFlipper.Imaging/` — empty (populated in Slice 2). References Core. `IsAotCompatible`.
- `src/FlyerFlipper.Infrastructure/` — empty (populated in Slice 6). References Core. `IsAotCompatible`.
- `src/FlyerFlipper.UI/` — References Core. Compiled bindings default. Packages: Avalonia, Avalonia.Fonts.Inter, Avalonia.Themes.Fluent, CommunityToolkit.Mvvm 8.4.0, Microsoft.Extensions.DependencyInjection.Abstractions 9.0.0.
  - `DependencyInjection/UiServiceCollectionExtensions.cs` (`AddFlyerFlipperUi()`).
  - `ViewModels/MainWindowViewModel.cs` — `[ObservableProperty]` on `ControlsDock` (`Avalonia.Controls.Dock`) and `ControlsBorderThickness` (`Avalonia.Thickness`); `[RelayCommand]` on `ToggleOrientation`, `Exit`, `About`.
  - `Views/MainWindow.axaml` + `.axaml.cs` — has both a parameterless ctor (for XAML previewer/runtime loader — avoids AVLN3001) and a DI ctor that chains to it.
- `src/FlyerFlipper.App/` — Composition root. WinExe, `PublishAot=true`, `BuiltInComInteropSupport=true`, `app.manifest` for PerMonitorV2 DPI.
  - `Program.cs` — `[STAThread] Main`, `Host.CreateApplicationBuilder`, registers `ILayoutModeService → LayoutModeService`, `IApplicationShutdown → AvaloniaApplicationShutdown`, then calls `AddFlyerFlipperUi()`. Stashes `IServiceProvider` on static `App.Services`.
  - `App.axaml` + `.axaml.cs` — Fluent theme; `OnFrameworkInitializationCompleted` resolves `MainWindow` from `App.Services` and hands it to `IClassicDesktopStyleApplicationLifetime.MainWindow`.
  - `AvaloniaApplicationShutdown.cs` — calls `IClassicDesktopStyleApplicationLifetime.Shutdown()`.
- `tests/FlyerFlipper.Tests/` — xUnit + Moq.
  - `Layout/LayoutModeServiceTests.cs` — 6 tests: default state, toggle, idempotency, event raise/no-raise.

**Layout mechanic:** outer `DockPanel` has Menu docked top; inner `DockPanel` has the controls region with `DockPanel.Dock={Binding ControlsDock}`. Vertical mode → `Dock.Top`; horizontal mode → `Dock.Left`; viewport fills remainder via `LastChildFill`. `ControlsBorderThickness` bound similarly for the divider.

**Menu:** File → Exit; View → Toggle Orientation (Ctrl+Shift+O); Help → About (About currently just writes to `Trace`).

---

## Automated verification (all green)

- `dotnet build -c Release`: 0 warnings, 0 errors.
- `dotnet test -c Release`: 6/6 passed.
- `dotnet publish src/FlyerFlipper.App -c Release -r win-x64 --self-contained` with `PublishAot=true`: clean, no AOT warnings. Produces ~19 MB native `FlyerFlipper.App.exe` at `src/FlyerFlipper.App/bin/Release/net9.0/win-x64/publish/`, plus SkiaSharp/HarfBuzz/GLES native side-libraries bundled by Avalonia.

---

## Environment quirk to remember

Native AOT publish on this box needs `vswhere.exe` on `PATH`, otherwise the linker step fails with error MSB3073 ("`vswhere.exe` is not recognized"). Fix: prepend the VS Installer folder before running `dotnet publish`:

```powershell
$env:PATH = "C:\Program Files (x86)\Microsoft Visual Studio\Installer;$env:PATH"
```

Not needed for `dotnet build` / `dotnet test` / `dotnet run` — only Native AOT publish invokes the MSVC linker.

---

## Manual verification (STOP gate — awaiting user)

**How to build & run:**
```powershell
cd D:\001_source\flyer_flipper
dotnet run --project src/FlyerFlipper.App
```

**What to check:**
- App launches, shows placeholder Controls + Viewport panels with a menu bar.
- `View → Toggle Orientation` (or Ctrl+Shift+O) flips layout — controls region moves between top and left; viewport fills the rest.
- `File → Exit` closes the app cleanly.
- Code and project structure match `PLAN.md`.

---

## Task snapshot (Slice 1)

| # | Status | Task |
|---|--------|------|
| 1 | completed | Scaffold solution and projects |
| 2 | completed | Add NuGet package references |
| 3 | completed | Implement `ILayoutModeService` in Core |
| 4 | completed | Wire `App.axaml` + `Program.cs` composition root |
| 5 | completed | Implement `MainWindow` + `MainWindowViewModel` |
| 6 | completed | Add xUnit test for `LayoutModeService` |
| 7 | completed | Verify: build, test, AOT publish |
| 8 | in_progress | Hand off Slice 1 for manual verification |

---

## What's next — Slice 2 preview (do not start until Slice 1 approved)

From `PLAN.md § Slice 2`:
- Add a folder-path `TextBox` in the controls region.
- Implement `IImageSource` in `FlyerFlipper.Core.Source` — takes an `ImageSourceQuery` record (`RootPath`, `Recursive`, `FormatWildcard`, `NameWildcard`); MVP UI wires only `RootPath` with defaults for the rest.
- Implement `IImageLoader` in `FlyerFlipper.Imaging` (SkiaSharp-backed). Need to add SkiaSharp NuGet package here — see `AvaloniaControls` sibling repo for version choice, but latest stable is fine.
- Implement `IThumbnailService` in `FlyerFlipper.Imaging`.
- Viewport shows a scrollable grid of thumbnails; scroll direction bound to `ILayoutModeService` (vertical when app is vertical; horizontal when app is horizontal).
- No pipeline yet — thumbnails are of originals.
- xUnit tests for `IImageSource` enumeration.

Formats to support: JPG, PNG, BMP, WebP — all natively via SkiaSharp.

---

## How to resume a fresh session

1. Read this file, then `PLAN.md`.
2. Read the memory index at `C:\Users\jacob\.claude\projects\D--001-source\memory\MEMORY.md`.
3. Check the current task state and whether the user has approved Slice 1. If not approved, ask.
4. Slice 2 begins only after explicit user approval of Slice 1.

# Flyer Flipper — Cross-Platform Image Processing App (Plan)

## Context

Build a new standalone desktop application at `D:\001_source\flyer_flipper` for cross-platform (Windows + Linux) image processing. Written in C# on Avalonia, architected around SOLID principles, `Microsoft.Extensions.DependencyInjection`, and AOT-compatible from day one. The MVP focuses on: a two-mode main window (vertical/horizontal layout), a tabbed control section, a dual-mode viewport (thumbnail grid vs. Photos-style single view), and a DI-injected image-processing pipeline architected to be extensible and reorderable — even though MVP does not expose reordering UI.

The pre-existing repo `D:\001_source\AvaloniaControls` establishes the user's baseline stack (Avalonia 11.2.4, .NET 9, Fluent theme, Inter font, compiled bindings). This project mirrors that baseline.

---

## Decisions (resolved during planning)

| # | Decision |
|---|----------|
| 1 | **Source path input.** MVP UI: single folder path, non-recursive. Underlying `IImageSource` designed from day one to accept a full query record (`RootPath`, `Recursive`, `FormatWildcard`, `NameWildcard`) so future UI is pure UI work. |
| 2 | **Imaging library.** SkiaSharp — Avalonia's internal renderer, MIT-licensed, AOT-friendly, minimal incremental dependency. |
| 3 | **MVP processors.** Ship two real processors — **grayscale** and **resize/downscale** — to prove the DI + tab-control-provider seam end-to-end. |
| 4 | **Save/Export.** Not in MVP. Deferred to Future enhancements. |
| 5 | **Input formats.** JPG, PNG, BMP, WebP (all natively supported by SkiaSharp). |
| 6 | **MVVM helper.** `CommunityToolkit.Mvvm` — source-generator-based, fully AOT-friendly. |
| 7 | **Menu style.** Traditional menu bar (File / View / Help). Orientation toggle lives under **View**. |
| 8 | **Settings persistence.** `ISettingsStore` backed by JSON at `%APPDATA%/FlyerFlipper/settings.json` (Windows) and `~/.config/FlyerFlipper/settings.json` (Linux). Uses `System.Text.Json` source generators (AOT-safe). Persisted state: last folder, layout mode, active tab, viewport mode. |
| 9 | **Test framework.** xUnit for the test runner + **Moq** for mocking interfaces. |
| 10 | **Plugin ABI (future).** True COM via .NET 8+ `ComWrappers` (`[GeneratedComInterface]`, source-generated, AOT-safe). |
| 11 | **Plugin UI (future).** Native window handles — HWND (Windows), X11 Window (Linux), NSView (macOS). Host embeds via Avalonia `NativeControlHost`. |
| 12 | **Plugin packaging & loading (future).** Plugins are **native unmanaged DLLs** (C / C++ / Rust / any language that can produce a native library and a COM vtable). Each plugin DLL exports a well-known C entry point (e.g. `FlyerFlipperCreatePlugin`) that returns an `IUnknown*` to the plugin's root COM object. AOT host loads them **in-process** via `NativeLibrary.Load` + `NativeLibrary.GetExport`, and marshals COM calls via `ComWrappers`. No IPC, no child process, no `AssemblyLoadContext`. AOT-safe because only *native* code is loaded at runtime — the ban on runtime managed-IL loading doesn't apply. |

---

## Architecture

### Solution layout

```
flyer_flipper/
├── flyer_flipper.sln
├── src/
│   ├── FlyerFlipper.App/          Avalonia desktop entry point, DI composition root
│   ├── FlyerFlipper.UI/           Views, ViewModels, Avalonia controls
│   ├── FlyerFlipper.Core/         Domain abstractions, pipeline interfaces, DTOs
│   ├── FlyerFlipper.Imaging/      SkiaSharp-backed loader + processor implementations
│   └── FlyerFlipper.Infrastructure/  File I/O, settings persistence
└── tests/
    └── FlyerFlipper.Tests/        xUnit + Moq
```

**UI** depends on **Core**; **Imaging** and **Infrastructure** implement **Core** interfaces; **App** is the composition root wiring everything through `IServiceCollection`. This gives clean seams for AOT (no reflection-based resolution) and per-slice testability.

### Key abstractions (in `FlyerFlipper.Core`)

- `ImageSourceQuery` — record: `{ RootPath, Recursive, FormatWildcard, NameWildcard }`.
- `IImageSource` — takes `ImageSourceQuery`, enumerates matching image file references. MVP UI supplies only `RootPath` with defaults for the rest.
- `IImageLoader` — reads a source reference into an in-memory `SourceImage` (SkiaSharp `SKBitmap` + metadata) using SkiaSharp codecs.
- `ProcessedImage` — plain record: pixel buffer (`ReadOnlyMemory<byte>`), width/height, stride, pixel format enum, metadata dictionary. No dependency on Avalonia or SkiaSharp — kept blittable / ABI-friendly in anticipation of the future native-COM plugin boundary.
- `IImageProcessor` — a single processing step. `ProcessedImage Process(ProcessedImage input, CancellationToken ct)`. Ordered by an `Order` property. In-process MVP processors implement this directly; future native plugins will be wrapped in a `ComWrappers`-backed adapter satisfying this same interface.
- `IImageProcessingPipeline` — receives injected `IEnumerable<IImageProcessor>`, executes them in `Order` sequence per image, returns final `ProcessedImage`.
- `IProcessorControlProvider` — each processor tab's UI is discovered via DI. Each processor registers a paired provider that yields the processor's control ViewModel + view type. The UI layer looks these up when building the tab strip.
- `IThumbnailService` — produces thumbnail bitmaps from `ProcessedImage` for the grid view.
- `IViewportModeService` — publishes current viewport mode (grid vs single) and current image index.
- `ILayoutModeService` — publishes vertical vs horizontal main-window layout state, toggled from the View menu.
- `ISettingsStore` — persists last folder, layout mode, active tab, viewport mode, viewed image index.

### AOT considerations

- `<PublishAot>true</PublishAot>` on the App csproj from slice 1. Each slice's done-check runs `dotnet publish -c Release -r win-x64` with no AOT warnings.
- Compiled bindings on all XAML (matches `AvaloniaControls.csproj` pattern: `<AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>`).
- No `Activator.CreateInstance` with runtime-only types, no `System.Reflection.Emit`, no assembly scanning — all DI registrations are explicit in the composition root.
- `CommunityToolkit.Mvvm` source generators produce `INotifyPropertyChanged` plumbing at compile time.
- `System.Text.Json` source generators for settings serialization.

### DI composition

`FlyerFlipper.App/Program.cs` builds an `IHost` via `Microsoft.Extensions.Hosting` that:

1. Registers Core services (`ILayoutModeService`, `IViewportModeService`, `IImageSource`, `IImageLoader`, `IThumbnailService`, `IImageProcessingPipeline`, `ISettingsStore`).
2. Registers each `IImageProcessor` with a matching `IProcessorControlProvider` in the desired MVP order (grayscale first, resize second).
3. Registers ViewModels and top-level Views.
4. Resolves `MainWindow` and hands it to Avalonia's `ClassicDesktopStyleApplicationLifetime.MainWindow`.

Future extensibility: additional processors are added by registering more `IImageProcessor` + `IProcessorControlProvider` pairs. A later `IPipelineOrderingService` can rearrange them at runtime without touching `IImageProcessingPipeline`.

### Preparing for the future plugin story (no MVP work, just interface hygiene)

The plugin architecture (decisions 10–12) does not add MVP code, but it does shape a few MVP choices so the future native-plugin adapter plugs in cleanly:

- **`IImageProcessor` is the host-side processor abstraction.** In-process MVP processors implement it directly. Future native plugins will be wrapped by a `NativePluginProcessorAdapter` (in a future `FlyerFlipper.PluginHost` project) that owns the loaded COM object and calls its methods through the `ComWrappers`-generated managed proxy, satisfying `IImageProcessor` on this side.
- **`ProcessedImage` must be ABI-friendly.** Its shape is a plain record — pixel buffer (`ReadOnlyMemory<byte>` or a pinned `byte[]`), width, height, stride, pixel-format enum, metadata. No Avalonia types, no SkiaSharp types. This keeps it representable across the ComWrappers boundary later without a redesign. Conversion to/from `SKBitmap` happens only inside `FlyerFlipper.Imaging`.
- **`IProcessorControlProvider` returns an Avalonia `Control` (or factory).** For in-process processors, it is a normal Avalonia view. For future native plugins, it is a `NativeControlHost`-derived adapter that wraps the native window handle the plugin returned. Same interface either way — the host doesn't distinguish.
- **Contracts stay dependency-light.** `IImageProcessor`, `IProcessorControlProvider`, and `ProcessedImage` live in `FlyerFlipper.Core` and have no dependency on Avalonia or SkiaSharp. This is what allows their eventual mirror in a `FlyerFlipper.PluginContracts` project to be `[GeneratedComInterface]` types — pure C# interfaces with primitive/blittable parameter shapes.

---

## Vertical slices

Each slice compiles, runs, and demonstrates observable behavior. Each ends with an AOT publish sanity check on `win-x64` **and a manual verification gate** — after the automated checks pass, I stop and wait for the user to build/run/evaluate the slice themselves. The next slice begins only after explicit user approval. This gives the user hands-on time with the design at every increment.

### Slice 1 — Skeleton app + main window with layout toggle
- Create solution + all projects per layout above.
- `App.axaml` with Fluent theme + Inter font (mirrors `AvaloniaControls`).
- `MainWindow` with `ILayoutModeService` driving a `Grid` that swaps between vertical (rows: controls / viewport) and horizontal (cols: controls / viewport) layouts.
- Menu bar with **View → Toggle Orientation** command.
- Placeholder panels for control region and viewport region.
- AOT publish passes.
- **Automated verification:** `dotnet build -c Release` clean; `dotnet publish -c Release -r win-x64` with `<PublishAot>true</PublishAot>` — no AOT warnings; new xUnit tests pass.
- **Manual verification (STOP — wait for user):** User builds and runs the app themselves. User verifies orientation toggle behavior in-app, reviews the code + project layout, and explicitly approves before Slice 2 begins.

### Slice 2 — Folder input + image enumeration + thumbnail grid
- Add a folder-path `TextBox` (MVP UI) in the controls region.
- Implement `IImageSource` (`ImageSourceQuery`-driven, MVP wires only `RootPath`), `IImageLoader` (SkiaSharp), `IThumbnailService`.
- Viewport shows a scrollable grid of thumbnails; scroll direction bound to `ILayoutModeService` (vertical scroll when app is vertical; horizontal scroll when app is horizontal).
- No pipeline yet — thumbnails are of *originals*.
- **Automated verification:** Build + AOT publish clean; xUnit tests pass (including tests for `IImageSource` enumeration).
- **Manual verification (STOP — wait for user):** User builds and runs; points at a folder of images; verifies thumbnails render and grid reflows on orientation toggle. User reviews the imaging abstractions before Slice 3 begins.

### Slice 3 — Single-image viewport mode
- Menu **View → Viewport Mode** cycles/toggles between grid and single.
- Single-image view: image fills viewport, left/right chevron controls overlay to cycle previous/next.
- Keyboard: Left/Right arrows navigate, Escape returns to grid.
- **Automated verification:** Build + AOT publish clean; tests pass.
- **Manual verification (STOP — wait for user):** User runs the app, switches between viewport modes, navigates images, verifies Photos-app-like feel. Explicit approval before Slice 4.

### Slice 4 — Pipeline architecture (pass-through)
- Define `IImageProcessor`, `IImageProcessingPipeline`, `ProcessedImage`.
- Implement default pipeline that runs the injected `IEnumerable<IImageProcessor>` in `Order` sequence.
- Wire pipeline between loader and thumbnail service. With zero processors registered, pipeline is pass-through and behavior is unchanged.
- Add a debug-only logging processor and verify it fires per image.
- **Automated verification:** Build + AOT publish clean; xUnit tests (including pipeline ordering + cancellation) pass.
- **Manual verification (STOP — wait for user):** User runs, confirms UX unchanged, observes log output proving the pipeline runs. User reviews pipeline abstractions before Slice 5.

### Slice 5 — Processor tabs + grayscale + resize processors
- Controls region becomes a `TabControl` populated from `IProcessorControlProvider` implementations resolved from DI.
- Implement `GrayscaleProcessor` (SkiaSharp `SKColorFilter`) with an enable checkbox on its tab.
- Implement `ResizeProcessor` (SkiaSharp `SKBitmap.Resize`) with target-dimension inputs on its tab.
- Processor setting changes trigger re-processing (with cancellation of in-flight work per image).
- **Automated verification:** Build + AOT publish clean; tests pass.
- **Manual verification (STOP — wait for user):** User runs, toggles grayscale, adjusts resize, verifies grid + single view refresh correctly. User reviews the processor + control-provider seam before Slice 6.

### Slice 6 — Settings persistence
- Implement `ISettingsStore` (JSON file at OS-appropriate path, `System.Text.Json` source generators).
- Persist and restore: last folder, layout mode, active tab, viewport mode, viewed image index.
- **Automated verification:** Build + AOT publish clean; tests pass (including round-trip serialization).
- **Manual verification (STOP — wait for user):** User closes and relaunches the app, verifies state is restored. Explicit approval before Slice 7.

### Slice 7 — Polish + Linux verification
- Cross-platform verification on Linux (WSL2 or VM — user to confirm).
- Final AOT publish check on `win-x64` and `linux-x64`.
- Unit test pass across `FlyerFlipper.Core` and `FlyerFlipper.Imaging`.
- **Automated verification:** Full test suite green on Windows; both `win-x64` and `linux-x64` AOT publish clean.
- **Manual verification (STOP — MVP acceptance):** User runs on Windows and (optionally) Linux, exercises the whole MVP flow end-to-end, formally accepts MVP.

---

## Critical files

- `flyer_flipper/flyer_flipper.sln`
- `src/FlyerFlipper.App/Program.cs` — composition root, `IHost` build
- `src/FlyerFlipper.App/App.axaml[.cs]` — Avalonia bootstrap (Fluent + Inter, compiled bindings)
- `src/FlyerFlipper.UI/Views/MainWindow.axaml[.cs]` — layout Grid + menu bar
- `src/FlyerFlipper.UI/ViewModels/MainWindowViewModel.cs`
- `src/FlyerFlipper.UI/Views/ProcessorTabHost.axaml[.cs]` — resolves and hosts processor tabs
- `src/FlyerFlipper.Core/Pipeline/IImageProcessor.cs`
- `src/FlyerFlipper.Core/Pipeline/IImageProcessingPipeline.cs`
- `src/FlyerFlipper.Core/Pipeline/IProcessorControlProvider.cs`
- `src/FlyerFlipper.Core/Source/ImageSourceQuery.cs`
- `src/FlyerFlipper.Core/Source/IImageSource.cs`
- `src/FlyerFlipper.Core/Layout/ILayoutModeService.cs`
- `src/FlyerFlipper.Core/Viewport/IViewportModeService.cs`
- `src/FlyerFlipper.Infrastructure/Settings/ISettingsStore.cs`
- `src/FlyerFlipper.Infrastructure/Settings/JsonSettingsStore.cs`
- `src/FlyerFlipper.Imaging/SkiaImageLoader.cs`
- `src/FlyerFlipper.Imaging/SkiaThumbnailService.cs`
- `src/FlyerFlipper.Imaging/Processors/GrayscaleProcessor.cs`
- `src/FlyerFlipper.Imaging/Processors/ResizeProcessor.cs`

---

## Future enhancements

Items discussed during planning but explicitly deferred out of MVP. Captured here so nothing is lost.

- **Rich source-selection UI.** Replace the MVP folder-path textbox with: a **Browse** button (native folder picker), a **Recursive** checkbox, a **format wildcard** input (e.g. `*.jpg;*.png`), and a **filename wildcard** input (e.g. `IMG_*`, `PIC_*`). The `IImageSource` interface already accepts these — pure UI + ViewModel work.
- **Save/export processed images.** Two flavors: (a) *Export all* — user picks output folder; every processed image is written. (b) *Save current* — save the currently-viewed single-view image. Requires: format selection, filename convention (suffix vs sibling folder), overwrite policy.
- **Configurable / reorderable pipeline UI.** Expose per-user processor reordering and enable/disable in the UI. Architecture already supports this (processors resolved as `IEnumerable<IImageProcessor>` with an `Order` property); MVP just uses static composition-root ordering.
- **User-installable processor plugins.** Full plugin architecture per decisions 10–12:
  - New `FlyerFlipper.PluginContracts` project with `[GeneratedComInterface]`-decorated interfaces (`IImageProcessorPlugin`, `IProcessorControlPlugin`, and a root factory `IPluginFactory` returned by the DLL entry point). ComWrappers source generators produce the marshaling. AOT-safe.
  - **Plugin DLL packaging.** Plugins are **native unmanaged libraries** — `.dll` on Windows, `.so` on Linux, `.dylib` on macOS. They can be authored in any language capable of producing a native library and a COM vtable (C, C++, Rust with `windows`/`com` crates, etc.). Each DLL exports a single C entry point (proposed name `FlyerFlipperCreatePlugin`) with a signature like `HRESULT FlyerFlipperCreatePlugin(REFIID iid, void** ppv)`.
  - New `FlyerFlipper.PluginHost` project in the main host — provides `NativePluginProcessorAdapter : IImageProcessor` and `NativePluginControlProvider : IProcessorControlProvider`. Loading flow: `NativeLibrary.Load(path)` → `NativeLibrary.GetExport(handle, "FlyerFlipperCreatePlugin")` → invoke the function pointer to receive an `IUnknown*` → hand to `ComWrappers.GetOrCreateObjectForComInstance` → cast to `IPluginFactory` → enumerate the processors + control providers the plugin advertises → register each in the DI container as adapter instances satisfying `IImageProcessor` / `IProcessorControlProvider`.
  - **Native UI handle wiring.** Plugin's control-provider COM object exposes a method returning a native window handle. `NativePluginControlProvider` wraps that handle in a `NativeControlHost`-derived Avalonia control and returns it to the host as any other processor control. All in-process — the plugin owns its native window, the host embeds it.
  - **AOT compatibility.** The host still publishes with `<PublishAot>true</PublishAot>`. All plugin code is *native*, so no managed IL is loaded at runtime — the AOT constraint is not violated. `ComWrappers` and `NativeLibrary` are both AOT-supported.
  - Plugin discovery folder location TBD (candidates: alongside the executable, `%APPDATA%/FlyerFlipper/plugins` on Windows, `~/.local/share/FlyerFlipper/plugins` on Linux).
- **Wider format support.** TIFF, HEIC, RAW — would need Magick.NET or platform-specific codecs beyond SkiaSharp's native set.

---

## Verification

**Per slice (all steps required before the next slice begins):**
1. `dotnet build -c Release` clean.
2. `dotnet publish -c Release -r win-x64 --self-contained` with `<PublishAot>true</PublishAot>` — no AOT warnings.
3. New unit tests pass (`dotnet test`).
4. **Manual developer verification (STOP gate).** I hand off; the user builds and runs the slice themselves, exercises the new behavior, reviews the code and design as it stands, and explicitly approves. I do not begin the next slice until this approval is given.

**At MVP:**
- All per-slice steps above on Windows for the final slice.
- `dotnet publish -c Release -r linux-x64` AOT publish.
- Smoke test on Linux (WSL2 or VM).
- Full test suite green.
- **Final manual developer verification.** User exercises the whole MVP flow end-to-end and formally accepts MVP.

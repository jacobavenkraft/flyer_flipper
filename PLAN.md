# Flyer Flipper — Cross-Platform Image Processing App (Plan)

## Context

Build a new standalone desktop application at `D:\001_source\flyer_flipper` for cross-platform (Windows + Linux) image processing. Written in C# on Avalonia, architected around SOLID principles, `Microsoft.Extensions.DependencyInjection`, and AOT-compatible from day one. The MVP focuses on: a two-mode main window (vertical/horizontal layout), a tabbed control section, a dual-mode viewport (thumbnail grid vs. Photos-style single view), and a DI-injected image-processing pipeline architected to be extensible and reorderable — even though MVP does not expose reordering UI.

The pre-existing repo `D:\001_source\AvaloniaControls` establishes the user's baseline stack (Avalonia 11.2.4, .NET 9, Fluent theme, Inter font, compiled bindings). This project started from that baseline, then moved to **.NET 10 + Avalonia 11.3.22** after Slice 2 (see decision 13).

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
| 13 | **Runtime + UI framework versions (after Slice 2).** Target **.NET 10** (`net10.0`, SDK pinned via `global.json` to `10.0.100` + `latestFeature`); Microsoft.Extensions packages 10.0.12. Avalonia upgraded **11.2.4 → 11.3.22** because the .NET 10 SDK's transitive NuGet audit flagged `Tmds.DBus.Protocol` 0.20.0 (CVE-2026-39959, Linux D-Bus); 11.3.22 depends on the patched 0.21.3. Avalonia.Skia 11.3.22 still uses SkiaSharp 2.88.9, so the Imaging project's SkiaSharp pin is unchanged. |
| 14 | **Image data flow (decided before Slice 4).** A **hybrid image store**: one Core service loads (and, from Slice 4b, processes) images for both views. It retains **thumbnails for every image** and **full-size images only for a sliding window of the current image ± 1**, and that window is loaded **only in single-view mode**. Memory is bounded by design (N thumbnails + 3 full-size images) — no byte budget or LRU. Opening an image outside the window re-decodes it. Alternatives considered: separate per-view load paths (initially chosen, then replaced by this), and a byte-budget LRU store (deferred — see Future enhancements). Original Slice 4 split into **4a** (store refactor, no behavior change) and **4b** (pipeline pass-through inside the store). |
| 15 | **Slice 5 design (decided before Slice 5).** (a) The folder input stays **above** the processor tab strip. (b) Processor setting changes apply **on commit** — checkboxes instantly; number fields on Enter, leaving the field, or spinner arrows. (c) `IProcessorControlProvider` lives in **`FlyerFlipper.UI`** and returns a typed Avalonia `Control` (resolves the plan's conflict between "returns an Avalonia control" and "Core stays Avalonia-free"); processor *settings* are plain Core types shared by the processor (Imaging) and its tab (UI). (d) A **viewport display scaling mode** — fit to window, fit without enlarging, stretch to fill, actual size — selected under **View → Image Scaling**; display only, pixels unchanged; applies to single view (thumbnails always fit their cells). (e) The **Resize processor** fits images inside its max width × height, keeping aspect ratio and **never enlarging**. (f) Decoded originals are **not** kept for the full-size window yet: setting changes re-decode (cheap for ~1080 px flyers); see Future enhancements. |

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
- `IImageSource` — takes `ImageSourceQuery`, enumerates matching image file references (`ImageReference`). MVP UI supplies only `RootPath` with defaults for the rest. Implemented by `FileSystemImageSource` in Infrastructure.
- `IImageCatalog` — *(added in Slice 2)* holds the current image set (`Query`, `Images`, `ImagesChanged`). Inputs (folder box, later restored settings) call `LoadAsync`; views (thumbnail grid, later single view) observe it. Keeps the input and viewport view models decoupled.
- `ImageBuffer` — *(added in Slice 2)* plain pixel buffer record: `ReadOnlyMemory<byte>` pixels, width, height, stride, `ImagePixelFormat` (MVP: `Bgra8888Premultiplied`). No Avalonia/SkiaSharp types. The pixel payload that `ProcessedImage` will carry in Slice 4.
- `IImageLoader` — reads a source reference into an in-memory `SourceImage` (`ImageReference` + `ImageBuffer`) using SkiaSharp codecs, with EXIF orientation applied. *(Slice 2 change: originally sketched as wrapping an `SKBitmap`, but that would put SkiaSharp into Core; the SkiaSharp ↔ `ImageBuffer` conversion lives only in `FlyerFlipper.Imaging`.)*
- `ProcessedImage` — plain record: pixel buffer (`ReadOnlyMemory<byte>`), width/height, stride, pixel format enum, metadata dictionary. No dependency on Avalonia or SkiaSharp — kept blittable / ABI-friendly in anticipation of the future native-COM plugin boundary. *(Slice 4b: `ImageBuffer Buffer` + `IReadOnlyDictionary<string, string> Metadata`; `FromSource` records `source.path` / `source.fileName` (`ImageMetadataKeys`).)*
- `IImageProcessor` — a single processing step. `ProcessedImage Process(ProcessedImage input, CancellationToken ct)`. Ordered by an `Order` property. In-process MVP processors implement this directly; future native plugins will be wrapped in a `ComWrappers`-backed adapter satisfying this same interface.
- `IImageProcessingPipeline` — receives injected `IEnumerable<IImageProcessor>`, executes them in `Order` sequence per image, returns final `ProcessedImage`. *(Slice 4b: `ImageProcessingPipeline` — stable sort (ties keep registration order), cancellation checked between steps, thread-safe; invoked only inside `ImageStore`, once per decode, so thumbnails and full-size images share processing.)*
- `IImageStore<TImage>` — *(added in Slice 4a)* the single source of display images; see decision 14.
- `IProcessorControlProvider` — each processor tab's UI is discovered via DI. Each processor registers a paired provider that yields the processor's control ViewModel + view type. The UI layer looks these up when building the tab strip.
- `IThumbnailService` — produces thumbnail `ImageBuffer`s (fit within a square, never upscaled) for the grid view. Input becomes the `ProcessedImage` buffer once the pipeline exists (Slice 4). The UI converts buffers to Avalonia bitmaps.
- `IViewportModeService` — publishes current viewport mode (grid vs single) and current image index. *(Slice 3: implemented by `ViewportModeService` over `IImageCatalog`; owns navigation — `ShowSingle(index)`, `ShowGrid`, `ToggleMode`, `MoveNext/Previous` (no wrap-around) — and resets to the first image when the catalog is replaced.)*
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

### Slice 4a — Image store (sliding window) refactor
*(Split from the original Slice 4 — see decision 14.)*
- Add `IImageStore<TImage>` / `ImageStore<TImage>` in Core: the single place images are loaded for display.
  - **Thumbnails** for every catalog image, generated in the background when a folder is entered and always retained.
  - **Full-size window**: current image ± 1, loaded **only while in single view**; images leaving the window are released; returning to the grid releases all full-size images.
  - Window loads take priority over background thumbnail generation.
  - A full-size load whose thumbnail isn't ready yet also produces the thumbnail (no second decode).
  - Holds display-ready images created by an injected `IDisplayImageFactory<TImage>` (UI implements it for Avalonia `Bitmap`), so there is exactly one copy of each thumbnail/full image.
  - UI-thread-affine: reconciles its state with `IImageCatalog` + `IViewportModeService` on their events; always raises its change event *before* disposing a replaced image.
- `ThumbnailGridViewModel` and `SingleImageViewModel` become pure observers of the store (their loading/caching logic moves into it).
- No pipeline yet — **behavior must be unchanged** from Slice 3.
- **Automated verification:** Build + AOT publish clean; store unit tests (window slide, mode gating, priority, cancellation on folder change, thumbnail reuse, disposal) + existing headless tests pass.
- **Manual verification (STOP — wait for user):** User confirms nothing changed in the running app and reviews the store design before Slice 4b.

### Slice 4b — Pipeline architecture (pass-through)
- Define `IImageProcessor`, `IImageProcessingPipeline`, `ProcessedImage`.
- Implement default pipeline that runs the injected `IEnumerable<IImageProcessor>` in `Order` sequence.
- Wire the pipeline inside the image store, between decode and display-image creation (so both views get processed images). With zero processors registered, pipeline is pass-through and behavior is unchanged.
- Add a debug-only logging processor and verify it fires per load.
- **Automated verification:** Build + AOT publish clean; xUnit tests (including pipeline ordering + cancellation) pass.
- **Manual verification (STOP — wait for user):** User runs, confirms UX unchanged, observes log output proving the pipeline runs. User reviews pipeline abstractions before Slice 5.

### Slice 5 — Processor tabs + grayscale + resize processors
- Controls region gains a `TabControl` (below the folder input) populated from `IProcessorControlProvider` implementations resolved from DI (decision 15).
- Implement `GrayscaleProcessor` (SkiaSharp `SKColorFilter`) with an enable checkbox on its tab.
- Implement `ResizeProcessor` (SkiaSharp `SKBitmap.Resize`) with enable + max width/height inputs on its tab; fit inside, keep aspect, never enlarge.
- Processor setting changes trigger re-processing (with cancellation of in-flight work per image): the image store regenerates thumbnails and the full-size window, keeping the previous image visible until its replacement is ready.
- **View → Image Scaling** submenu: fit to window (default) / fit without enlarging / stretch to fill / actual size — display-only scaling of the single-image view.
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
- `src/FlyerFlipper.Core/Store/ImageStore.cs`
- `src/FlyerFlipper.Core/Pipeline/ProcessedImage.cs`
- `src/FlyerFlipper.Core/Pipeline/ImageProcessingPipeline.cs`
- `src/FlyerFlipper.Core/Pipeline/IImageProcessor.cs`
- `src/FlyerFlipper.Core/Pipeline/IImageProcessingPipeline.cs`
- `src/FlyerFlipper.UI/Processors/IProcessorControlProvider.cs` *(moved from Core — decision 15)*
- `src/FlyerFlipper.Core/Source/ImageSourceQuery.cs`
- `src/FlyerFlipper.Core/Source/IImageSource.cs`
- `src/FlyerFlipper.Core/Source/IImageCatalog.cs`
- `src/FlyerFlipper.Core/Imaging/ImageBuffer.cs`
- `src/FlyerFlipper.Infrastructure/Source/FileSystemImageSource.cs`
- `src/FlyerFlipper.UI/ViewModels/ThumbnailGridViewModel.cs`
- `src/FlyerFlipper.Core/Layout/ILayoutModeService.cs`
- `src/FlyerFlipper.Core/Viewport/IViewportModeService.cs`
- `src/FlyerFlipper.UI/ViewModels/SingleImageViewModel.cs`
- `src/FlyerFlipper.UI/Views/SingleImageView.axaml[.cs]`
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
- **Large-folder thumbnail performance.** *(Deferred in Slice 2.)* The MVP grid is an `ItemsControl` + `WrapPanel` (not virtualized) and generates every thumbnail up front (bounded to ≤4 concurrent decodes). For folders with thousands of images: a virtualizing wrap layout, generating thumbnails only for visible/near-visible cells, and optionally a reduced-resolution decode path (SkiaSharp `SKCodec` scaled decode for JPEG) — the last must be reconciled with the pipeline running on full-resolution originals.
- **Byte-budget retention for the image store.** *(Deferred before Slice 4 — decision 14.)* Replace the store's fixed "current ± 1 in single view" window with a memory budget (proposed default ~25% of RAM, capped at 4 GB, exposed as a setting) and LRU eviction, never evicting current ± 1 — so folders that fit stay fully decoded and reopening any image is instant. Memory reference: 12 MP photo ≈ 46 MB BGRA; 600 dpi letter scan ≈ 128 MB. Should be a change to the store's retention policy only; views are unaffected.
- **Configurable resize output modes.** *(Deferred in Slice 5 — decision 15.)* Let the user choose how processing resizes pixels (fit without enlarging / fit allowing enlargement / exact W×H stretch), distinct from the viewport's display scaling. Might be best delivered simply as an additional "Resizing" processor (or plugin) with its own mode setting rather than by extending the MVP `ResizeProcessor`.
- **Keep decoded originals for the full-size window.** *(Raised before Slice 4; decide in Slice 5.)* Retaining the unprocessed decode of the 3 window images lets processor-setting changes re-process single view instantly without re-decoding, at ~2× window memory. Thumbnails would still re-decode in the background.
- **Keyboard navigation inside the thumbnail grid.** *(Deferred in Slice 3.)* Thumbnails are opened by double-click or via View → Toggle Viewport Mode (opens the current image). Arrow-key movement between thumbnails and Enter-to-open would need focusable/selectable items (e.g. a `ListBox` with a wrap panel).
- **Display-sized decoding in single view.** *(Deferred in Slice 3.)* Single view shows full-resolution bitmaps (current + both neighbours kept decoded). Very large scans (e.g. 50 MP ≈ 200 MB each) could be decoded/downscaled to the viewport size instead; would need re-decoding on window resize/zoom, and must be reconciled with the pipeline output.
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

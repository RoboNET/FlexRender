# FlexRender WASM Playground — Design Document

## Goal

A fully client-side, IDE-like browser application for authoring and previewing FlexRender YAML templates. Runs entirely in the browser via .NET WebAssembly — no backend required.

## Technology Stack

| Layer | Technology |
|-------|-----------|
| .NET WASM | `wasmbrowser` template, `[JSExport]`/`[JSImport]` interop |
| Rendering | FlexRender.Core + FlexRender.Yaml + FlexRender.Skia.Render |
| Native WASM | SkiaSharp.NativeAssets.WebAssembly |
| Code editor | Monaco Editor + monaco-yaml (JSON Schema autocomplete) |
| Hosting | GitHub Pages (static) |
| CI/CD | GitHub Actions |

## Architecture

```
Browser (Static HTML/JS/CSS)
┌─────────────────────────────────────────────────────────┐
│  ┌──────────────┐  ┌──────────────┐  ┌───────────────┐  │
│  │ Monaco YAML  │  │ Monaco JSON  │  │ Preview Panel │  │
│  │ (template)   │  │ (data)       │  │ PNG/SVG/canvas│  │
│  └──────┬───────┘  └──────┬───────┘  └───────▲───────┘  │
│         └────────┬────────┘                   │          │
│           debounce 300ms                      │          │
│         ┌────────▼────────────────────────────┘          │
│         │  Web Worker (.NET WASM)                        │
│         │  FlexRender.Core + Yaml + Skia.Render          │
│         │  SkiaSharp.NativeAssets.WebAssembly             │
│         │                                                │
│         │  [JSExport] RenderToPng(yaml, json) → byte[]   │
│         │  [JSExport] RenderToSvg(yaml, json) → string   │
│         │  [JSExport] Validate(yaml) → string            │
│         │  [JSExport] DebugLayout(yaml, json) → string   │
│         │  [JSExport] LoadFont(name, data)               │
│         │  [JSExport] LoadImage(path, data)              │
│         │  [JSExport] LoadContent(path, data)            │
│         └────────────────────────────────────────────────┘
└─────────────────────────────────────────────────────────┘
```

## JSExport API (C# → JS)

```csharp
partial class PlaygroundApi
{
    // Rendering
    [JSExport]
    public static byte[] RenderToPng(string yaml, string? dataJson);

    [JSExport]
    public static string RenderToSvg(string yaml, string? dataJson);

    // Validation — returns JSON array of error objects
    [JSExport]
    public static string Validate(string yaml);

    // Debug — returns JSON layout tree
    [JSExport]
    public static string DebugLayout(string yaml, string? dataJson);

    // Resource loading (drag & drop)
    [JSExport]
    public static void LoadFont(string name, byte[] data);    // .ttf, .otf, .woff2

    [JSExport]
    public static void LoadImage(string path, byte[] data);   // .png, .jpg, .svg

    [JSExport]
    public static void LoadContent(string path, byte[] data); // .ndc, .txt
}
```

All resources are loaded into an in-memory `IResourceLoader` on the WASM side. Templates reference them by filename (e.g., `src: "logo.png"`, `src: "receipt.ndc"`).

## UI Layout

```
┌─────────────────────────────────────────────────────┐
│  Logo   [Examples ▾]  [Export PNG] [Export SVG] [☀/🌙] │
├────────────────────────┬────────────────────────────┤
│  Monaco YAML Editor    │  Preview                   │
│  (template.yaml)       │  ┌──────────────────────┐  │
│                        │  │                      │  │
│                        │  │    Rendered Image     │  │
│                        │  │    (zoom/pan)         │  │
│                        │  │                      │  │
│                        │  └──────────────────────┘  │
├────────────────────────┤  [Preview] [Layout] [Errors]│
│  Monaco JSON Editor    ├────────────────────────────┤
│  (data.json)           │  Layout tree / Error list  │
│                        │                            │
└────────────────────────┴────────────────────────────┘
│  Ready · 400×600 · Rendered in 45ms · 0 errors      │
└─────────────────────────────────────────────────────┘
```

### Panels

- **Top bar**: Logo, example gallery dropdown, export buttons (PNG/SVG), light/dark theme toggle
- **Left top**: Monaco YAML editor with monaco-yaml plugin (JSON Schema autocomplete for FlexRender properties)
- **Left bottom**: Monaco JSON editor for template data
- **Right top**: Rendered image preview with zoom (mouse wheel) and pan (drag)
- **Right bottom tabs**:
  - Preview (default) — rendered image
  - Layout — debug layout tree visualization (from `DebugLayout`)
  - Errors — validation errors list (from `Validate`)
- **Status bar**: Render status, canvas dimensions, render time, error count

### Key interactions

- **Debounce 300ms**: After the user stops typing, call `Validate()` + `RenderToPng()` via Web Worker
- **Drag & drop**: Drop font files (.ttf/.otf/.woff2), images (.png/.jpg/.svg), or NDC content (.ndc/.txt) anywhere → calls `LoadFont`/`LoadImage`/`LoadContent` → re-renders
- **Example gallery**: Built-in examples from `examples/` directory, loads YAML + JSON into editors on click
- **Zoom/Pan**: CSS transform on `<img>`, mouse wheel for zoom, drag to pan
- **Export**: Download rendered result as PNG or SVG file

## Monaco YAML Integration

- **monaco-yaml** plugin provides YAML validation, autocomplete, and hover
- **JSON Schema** generated from `KnownProperties.cs` — covers all element types, properties, enums (FlexDirection, JustifyContent, AlignItems, etc.)
- Schema file: `wwwroot/schemas/flexrender-template.json`
- Autocomplete for: element `type` values, all properties per element type, enum values, CSS-like values

## Project Structure

```
src/FlexRender.Playground/
  FlexRender.Playground.csproj        # wasmbrowser SDK project
  Program.cs                          # Entry point + [JSExport] API
  PlaygroundApi.cs                    # Render/Validate/Debug/Load methods
  MemoryResourceLoader.cs            # In-memory IResourceLoader for drag&drop
  wwwroot/
    index.html                        # Main page
    main.js                           # .NET WASM init + Monaco + UI wiring
    style.css                         # Layout and theming
    schemas/
      flexrender-template.json        # JSON Schema for autocomplete
    examples/                         # Built-in example templates
      receipt.yaml / receipt.json
      card.yaml / card.json
      ...
```

## .csproj Configuration

```xml
<Project Sdk="Microsoft.NET.Sdk.WebAssembly">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <InvariantGlobalization>true</InvariantGlobalization>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="../FlexRender.Core/FlexRender.Core.csproj" />
    <ProjectReference Include="../FlexRender.Yaml/FlexRender.Yaml.csproj" />
    <ProjectReference Include="../FlexRender.Skia.Render/FlexRender.Skia.Render.csproj" />
    <ProjectReference Include="../FlexRender.Content.Ndc/FlexRender.Content.Ndc.csproj" />
    <PackageReference Include="SkiaSharp.NativeAssets.WebAssembly" Version="3.119.1" />
  </ItemGroup>
</Project>
```

## Build & Deploy

### Local development
```bash
dotnet run --project src/FlexRender.Playground
```
Launches dev server with hot-reload at `https://localhost:5xxx`.

### Production build
```bash
dotnet publish src/FlexRender.Playground -c Release
```
Output: `bin/Release/net10.0/publish/wwwroot/` — static files ready for deployment.

### Bundle size (estimated)
| Component | Gzip size |
|-----------|-----------|
| .NET WASM runtime | ~3 MB |
| SkiaSharp native WASM | ~2 MB |
| FlexRender assemblies | ~500 KB |
| Monaco Editor (CDN) | 0 (loaded externally) |
| **Total first load** | **~5.5 MB** |

All assets are cached by the browser after first load.

### Optimization
- `InvariantGlobalization=true` — removes ICU data (~30% savings)
- `PublishTrimmed=true` — tree-shakes unused code
- Brotli pre-compression in CI for all static files
- Monaco Editor loaded from CDN (not bundled)

### GitHub Actions CI/CD
- **Trigger**: push to `main` or tag `v*`
- **Steps**: `dotnet publish` → copy `wwwroot/` → deploy to GitHub Pages
- **URL**: `https://robonet.github.io/FlexRender/` (or custom domain)

## Embedded Fonts

Two fonts bundled as embedded resources:
- **Inter** (sans-serif) — default for text elements
- **Roboto Mono** (monospace) — for code/receipt templates

Additional fonts loaded via drag & drop at runtime.

## Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| SkiaSharp 3.119.2 vs NativeAssets.WebAssembly 3.119.1 version mismatch | Build/runtime failure | Pin both to 3.119.1 or wait for 3.119.2 WASM release |
| Large bundle size (~5.5 MB) | Slow first load | Brotli compression, loading spinner, cache headers |
| HarfBuzz WASM not available | Complex text shaping broken | Start without HarfBuzz, add later if HarfBuzzSharp.NativeAssets.WebAssembly exists |
| Web Worker + .NET WASM interop complexity | Technical risk | Prototype Web Worker integration first as spike |
| YamlDotNet AOT compatibility in WASM | Possible trimming issues | Test early, configure trimmer roots if needed |

## Out of Scope (Future)

- URL sharing (gzip+base64 or server-side storage)
- Collaborative editing
- Template marketplace / community gallery
- PWA / offline support
- Mobile-optimized layout

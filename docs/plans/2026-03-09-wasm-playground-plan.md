# FlexRender WASM Playground Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build a fully client-side browser IDE for authoring and previewing FlexRender YAML templates using .NET WebAssembly.

**Architecture:** Static site (GitHub Pages) with .NET WASM runtime running FlexRender.Core + Yaml + Skia.Render in a Web Worker. Monaco Editor with monaco-yaml provides YAML editing with autocomplete. No backend.

**Tech Stack:** .NET 10 wasmbrowser, SkiaSharp.NativeAssets.WebAssembly, Monaco Editor, monaco-yaml, vanilla HTML/JS/CSS

**Design doc:** `docs/plans/2026-03-09-wasm-playground-design.md`

---

## Task 1: Scaffold wasmbrowser project

**Files:**
- Create: `src/FlexRender.Playground/FlexRender.Playground.csproj`
- Create: `src/FlexRender.Playground/Program.cs`
- Modify: `FlexRender.slnx` (add project reference)

**Step 1: Install wasmbrowser template**

```bash
dotnet new install Microsoft.NET.Runtime.WebAssembly.Templates.net10
```

Expected: Template `wasmbrowser` available.

**Step 2: Create project**

```bash
cd src
mkdir FlexRender.Playground
cd FlexRender.Playground
dotnet new wasmbrowser
```

**Step 3: Replace generated csproj with FlexRender-specific configuration**

Replace `FlexRender.Playground.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk.WebAssembly">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <InvariantGlobalization>true</InvariantGlobalization>
    <!-- Fingerprint JS files for cache busting -->
    <OverrideHtmlAssetPlaceholders>true</OverrideHtmlAssetPlaceholders>
  </PropertyGroup>

  <ItemGroup>
    <StaticWebAssetFingerprintPattern Include="JS" Pattern="*.js" Expression="#[.{fingerprint}]!" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\FlexRender.Core\FlexRender.Core.csproj" />
    <ProjectReference Include="..\FlexRender.Yaml\FlexRender.Yaml.csproj" />
    <ProjectReference Include="..\FlexRender.Skia.Render\FlexRender.Skia.Render.csproj" />
    <ProjectReference Include="..\FlexRender.Content.Ndc\FlexRender.Content.Ndc.csproj" />
    <PackageReference Include="SkiaSharp.NativeAssets.WebAssembly" Version="3.119.1" />
  </ItemGroup>
</Project>
```

**Step 4: Write minimal Program.cs**

```csharp
using System.Runtime.InteropServices.JavaScript;

Console.WriteLine("FlexRender Playground loaded");
```

**Step 5: Add project to solution**

```bash
cd /path/to/SkiaLayout
dotnet sln FlexRender.slnx add src/FlexRender.Playground/FlexRender.Playground.csproj --solution-folder playground
```

**Step 6: Verify it builds**

```bash
dotnet build src/FlexRender.Playground
```

Expected: Build succeeds. If SkiaSharp version mismatch, pin SkiaSharp to 3.119.1 in `Directory.Packages.props`.

**Step 7: Verify it runs**

```bash
dotnet run --project src/FlexRender.Playground
```

Expected: Dev server starts, browser opens, console shows "FlexRender Playground loaded".

**Step 8: Commit**

```bash
git add src/FlexRender.Playground/ FlexRender.slnx
git commit -m "feat(playground): scaffold wasmbrowser project with FlexRender dependencies"
```

---

## Task 2: MemoryResourceLoader

In-memory resource loader for drag & drop files (fonts, images, NDC content). Follows the `Base64ResourceLoader` pattern.

**Files:**
- Create: `src/FlexRender.Playground/MemoryResourceLoader.cs`

**Step 1: Write MemoryResourceLoader**

```csharp
using FlexRender.Abstractions;

namespace FlexRender.Playground;

/// <summary>
/// In-memory resource loader for browser-uploaded files (fonts, images, content).
/// Resources are stored by name and served from memory.
/// </summary>
internal sealed class MemoryResourceLoader : IResourceLoader
{
    private readonly Dictionary<string, byte[]> _resources = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public int Priority => 10; // Highest priority — uploaded files override everything

    /// <inheritdoc />
    public bool CanHandle(string uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
            return false;

        // Handle any URI that matches a stored resource name
        // Strip leading "./" or "/" for matching
        var normalized = NormalizePath(uri);
        return _resources.ContainsKey(normalized);
    }

    /// <inheritdoc />
    public Task<Stream?> Load(string uri, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);

        var normalized = NormalizePath(uri);

        if (_resources.TryGetValue(normalized, out var data))
        {
            Stream stream = new MemoryStream(data, writable: false);
            return Task.FromResult<Stream?>(stream);
        }

        return Task.FromResult<Stream?>(null);
    }

    /// <summary>
    /// Stores a resource in memory, available by name.
    /// </summary>
    public void AddResource(string name, byte[] data)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(data);

        var normalized = NormalizePath(name);
        _resources[normalized] = data;
    }

    /// <summary>
    /// Removes a resource from memory.
    /// </summary>
    public bool RemoveResource(string name)
    {
        var normalized = NormalizePath(name);
        return _resources.Remove(normalized);
    }

    /// <summary>
    /// Removes all stored resources.
    /// </summary>
    public void Clear() => _resources.Clear();

    private static string NormalizePath(string path)
    {
        if (path.StartsWith("./", StringComparison.Ordinal))
            path = path[2..];
        else if (path.StartsWith("/", StringComparison.Ordinal))
            path = path[1..];

        return path;
    }
}
```

**Step 2: Verify build**

```bash
dotnet build src/FlexRender.Playground
```

Expected: Build succeeds.

**Step 3: Commit**

```bash
git add src/FlexRender.Playground/MemoryResourceLoader.cs
git commit -m "feat(playground): add MemoryResourceLoader for browser file uploads"
```

---

## Task 3: PlaygroundApi — JSExport interop

The C# API surface exposed to JavaScript via `[JSExport]`.

**Files:**
- Create: `src/FlexRender.Playground/PlaygroundApi.cs`
- Modify: `src/FlexRender.Playground/Program.cs`

**Step 1: Write PlaygroundApi.cs**

```csharp
using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using FlexRender.Abstractions;
using FlexRender.Configuration;
using FlexRender.Content.Ndc;
using FlexRender.Skia;
using FlexRender.Values;
using FlexRender.Yaml;

namespace FlexRender.Playground;

/// <summary>
/// Browser-facing API exposed via [JSExport] for the WASM playground.
/// </summary>
internal static partial class PlaygroundApi
{
    private static MemoryResourceLoader s_memoryLoader = new();
    private static IFlexRender? s_render;
    private static TemplateParser s_parser = new();

    /// <summary>
    /// Initializes the FlexRender engine. Must be called once before rendering.
    /// </summary>
    [JSExport]
    public static void Initialize()
    {
        s_memoryLoader = new MemoryResourceLoader();

        s_render?.Dispose();
        s_render = new FlexRenderBuilder()
            .WithResourceLoader(s_memoryLoader)
            .WithoutDefaultLoaders()
            .WithSkia()
            .WithContentParser(new NdcContentParser())
            .Build();
    }

    /// <summary>
    /// Renders a YAML template to PNG bytes.
    /// </summary>
    /// <returns>PNG image as byte array, or empty array on error.</returns>
    [JSExport]
    public static byte[] RenderToPng(string yaml, string? dataJson)
    {
        try
        {
            if (s_render is null)
                return [];

            var template = s_parser.Parse(yaml);
            var data = ParseData(dataJson);

            // JSExport doesn't support async — use sync-over-async
            return s_render.RenderToPng(template, data).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Render error: {ex.Message}");
            return [];
        }
    }

    /// <summary>
    /// Validates a YAML template and returns errors as JSON array.
    /// </summary>
    /// <returns>JSON string: [] on success, [{"message":"...","line":N},...] on errors.</returns>
    [JSExport]
    public static string Validate(string yaml)
    {
        var errors = new List<object>();

        try
        {
            s_parser.Parse(yaml);
        }
        catch (TemplateParseException ex)
        {
            errors.Add(new { message = ex.Message, line = ex.Line });
        }
        catch (Exception ex)
        {
            errors.Add(new { message = ex.Message, line = 0 });
        }

        return JsonSerializer.Serialize(errors);
    }

    /// <summary>
    /// Loads a font file into memory for template rendering.
    /// </summary>
    [JSExport]
    public static void LoadFont(string name, byte[] data)
    {
        s_memoryLoader.AddResource(name, data);
    }

    /// <summary>
    /// Loads an image file into memory for template rendering.
    /// </summary>
    [JSExport]
    public static void LoadImage(string path, byte[] data)
    {
        s_memoryLoader.AddResource(path, data);
    }

    /// <summary>
    /// Loads a content file (NDC, etc.) into memory for template rendering.
    /// </summary>
    [JSExport]
    public static void LoadContent(string path, byte[] data)
    {
        s_memoryLoader.AddResource(path, data);
    }

    private static ObjectValue? ParseData(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        return TemplateData.FromJson(json);
    }
}
```

> **Note:** `TemplateData.FromJson` — check if this utility exists. If not, use `JsonSerializer.Deserialize` + conversion to `ObjectValue`. The exact method name may need adjustment based on the actual codebase API.

**Step 2: Update Program.cs**

```csharp
using System.Runtime.InteropServices.JavaScript;
using FlexRender.Playground;

// Initialize the FlexRender engine
PlaygroundApi.Initialize();

Console.WriteLine("FlexRender Playground ready");
```

**Step 3: Verify build**

```bash
dotnet build src/FlexRender.Playground
```

Expected: Build succeeds. Fix any missing `using` directives or API mismatches.

> **Important:** If `FlexRenderBuilder.WithResourceLoader()` doesn't exist as a direct method, check for `AddResourceLoader()` or add the `MemoryResourceLoader` to the builder's loader list. The builder API may need a small extension. Also check if `WithoutDefaultLoaders()` exists — if not, the default `FileResourceLoader` is harmless in WASM (it just won't find local files).

> **Important:** If `WithContentParser()` doesn't exist on `FlexRenderBuilder`, check how NDC parser is registered (it may need `FlexRenderBuilder` extension from the Content.Ndc package).

**Step 4: Commit**

```bash
git add src/FlexRender.Playground/PlaygroundApi.cs src/FlexRender.Playground/Program.cs
git commit -m "feat(playground): add PlaygroundApi with JSExport render/validate/load methods"
```

---

## Task 4: Minimal HTML + JS shell

Basic page that loads .NET WASM, renders a hardcoded template, and displays the result. This validates the full WASM pipeline before adding Monaco.

**Files:**
- Create: `src/FlexRender.Playground/wwwroot/index.html`
- Create: `src/FlexRender.Playground/wwwroot/main.js`

**Step 1: Write index.html**

```html
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>FlexRender Playground</title>
    <link rel="preload" id="webassembly" />
    <script type="importmap"></script>
    <script type="module" src="main#[.{fingerprint}].js"></script>
    <style>
        body {
            margin: 0;
            font-family: system-ui, -apple-system, sans-serif;
            background: #1e1e1e;
            color: #d4d4d4;
            display: flex;
            flex-direction: column;
            height: 100vh;
        }
        #loading {
            display: flex;
            align-items: center;
            justify-content: center;
            height: 100vh;
            font-size: 1.2em;
        }
        #app { display: none; height: 100vh; }
        #preview-img {
            max-width: 100%;
            max-height: 80vh;
            image-rendering: pixelated;
        }
        #status { padding: 4px 12px; font-size: 12px; background: #252526; }
        #error { color: #f48771; padding: 12px; white-space: pre-wrap; }
    </style>
</head>
<body>
    <div id="loading">Loading FlexRender WASM runtime...</div>
    <div id="app">
        <div style="padding: 12px;">
            <h2 style="margin:0">FlexRender Playground</h2>
            <p>WASM runtime loaded. Minimal test:</p>
            <img id="preview-img" />
            <pre id="error"></pre>
        </div>
        <div id="status">Ready</div>
    </div>
</body>
</html>
```

**Step 2: Write main.js**

```javascript
import { dotnet } from './_framework/dotnet.js';

const { getAssemblyExports, getConfig, runMain } = await dotnet
    .withApplicationArguments("start")
    .create();

const config = getConfig();
const exports = await getAssemblyExports(config.mainAssemblyName);
const api = exports.FlexRender.Playground.PlaygroundApi;

await runMain();

// Hide loading, show app
document.getElementById('loading').style.display = 'none';
document.getElementById('app').style.display = 'flex';

// Test render with a minimal template
const testYaml = `
canvas:
  width: 300
  height: 100
  background: "#ffffff"
elements:
  - type: text
    content: "Hello from WASM!"
    size: 24
    color: "#333333"
    padding: "20"
`;

const statusEl = document.getElementById('status');
const errorEl = document.getElementById('error');
const imgEl = document.getElementById('preview-img');

try {
    statusEl.textContent = 'Rendering...';
    const start = performance.now();

    const pngBytes = api.RenderToPng(testYaml, null);
    const elapsed = (performance.now() - start).toFixed(0);

    if (pngBytes && pngBytes.length > 0) {
        const blob = new Blob([pngBytes], { type: 'image/png' });
        imgEl.src = URL.createObjectURL(blob);
        statusEl.textContent = `Rendered in ${elapsed}ms · ${pngBytes.length} bytes`;
    } else {
        errorEl.textContent = 'Render returned empty result';
        statusEl.textContent = 'Error';
    }
} catch (e) {
    errorEl.textContent = e.message || String(e);
    statusEl.textContent = 'Error';
}
```

**Step 3: Run and test in browser**

```bash
dotnet run --project src/FlexRender.Playground
```

Expected: Browser opens, shows "Hello from WASM!" rendered as a PNG image. If SkiaSharp WASM fails, this is where we'll discover it and fix.

> **Troubleshooting:** If SkiaSharp native binding fails, check:
> 1. `SkiaSharp.NativeAssets.WebAssembly` version compatibility
> 2. May need `<WasmNativeStrip>false</WasmNativeStrip>` in csproj
> 3. May need `<EmccExtraLDFlags>-s ALLOW_MEMORY_GROWTH=1</EmccExtraLDFlags>`

**Step 4: Commit**

```bash
git add src/FlexRender.Playground/wwwroot/
git commit -m "feat(playground): add minimal HTML/JS shell with WASM render test"
```

---

## Task 5: Monaco Editor integration

Add Monaco Editor with two panes: YAML template editor and JSON data editor.

**Files:**
- Modify: `src/FlexRender.Playground/wwwroot/index.html`
- Create: `src/FlexRender.Playground/wwwroot/style.css`
- Modify: `src/FlexRender.Playground/wwwroot/main.js`

**Step 1: Write style.css**

```css
* { margin: 0; padding: 0; box-sizing: border-box; }

body {
    font-family: system-ui, -apple-system, sans-serif;
    background: #1e1e1e;
    color: #d4d4d4;
    height: 100vh;
    overflow: hidden;
}

/* Loading screen */
#loading {
    display: flex;
    align-items: center;
    justify-content: center;
    height: 100vh;
    font-size: 1.2em;
    flex-direction: column;
    gap: 12px;
}

#loading .spinner {
    width: 32px;
    height: 32px;
    border: 3px solid #333;
    border-top-color: #007acc;
    border-radius: 50%;
    animation: spin 1s linear infinite;
}

@keyframes spin { to { transform: rotate(360deg); } }

/* Main app layout */
#app {
    display: none;
    flex-direction: column;
    height: 100vh;
}

/* Top bar */
.toolbar {
    display: flex;
    align-items: center;
    padding: 6px 12px;
    background: #2d2d2d;
    border-bottom: 1px solid #404040;
    gap: 12px;
    flex-shrink: 0;
}

.toolbar h1 {
    font-size: 14px;
    font-weight: 600;
    white-space: nowrap;
}

.toolbar select, .toolbar button {
    background: #3c3c3c;
    color: #d4d4d4;
    border: 1px solid #555;
    padding: 4px 10px;
    border-radius: 4px;
    font-size: 12px;
    cursor: pointer;
}

.toolbar select:hover, .toolbar button:hover {
    background: #4c4c4c;
}

.toolbar .spacer { flex: 1; }

/* Main content area */
.main-content {
    display: flex;
    flex: 1;
    overflow: hidden;
}

/* Left panel — editors */
.editor-panel {
    display: flex;
    flex-direction: column;
    width: 50%;
    min-width: 300px;
    border-right: 1px solid #404040;
}

.editor-section {
    flex: 1;
    display: flex;
    flex-direction: column;
    overflow: hidden;
}

.editor-section + .editor-section {
    border-top: 1px solid #404040;
}

.editor-label {
    padding: 4px 12px;
    font-size: 11px;
    text-transform: uppercase;
    letter-spacing: 0.5px;
    color: #888;
    background: #252526;
    flex-shrink: 0;
}

.editor-container {
    flex: 1;
    overflow: hidden;
}

/* Right panel — preview */
.preview-panel {
    flex: 1;
    display: flex;
    flex-direction: column;
    overflow: hidden;
}

.preview-tabs {
    display: flex;
    background: #252526;
    border-bottom: 1px solid #404040;
    flex-shrink: 0;
}

.preview-tabs button {
    background: none;
    color: #888;
    border: none;
    padding: 6px 16px;
    font-size: 12px;
    cursor: pointer;
    border-bottom: 2px solid transparent;
}

.preview-tabs button.active {
    color: #d4d4d4;
    border-bottom-color: #007acc;
}

.preview-content {
    flex: 1;
    overflow: auto;
    display: flex;
    align-items: center;
    justify-content: center;
    background: #1a1a1a;
}

.preview-content img {
    max-width: 100%;
    max-height: 100%;
    object-fit: contain;
    image-rendering: auto;
}

.tab-pane { display: none; width: 100%; height: 100%; }
.tab-pane.active {
    display: flex;
    align-items: center;
    justify-content: center;
}

#errors-pane {
    align-items: flex-start;
    justify-content: flex-start;
    padding: 12px;
    font-family: monospace;
    font-size: 13px;
    color: #f48771;
    white-space: pre-wrap;
}

#layout-pane {
    align-items: flex-start;
    justify-content: flex-start;
    padding: 12px;
    font-family: monospace;
    font-size: 12px;
    overflow: auto;
}

/* Status bar */
.status-bar {
    display: flex;
    align-items: center;
    padding: 2px 12px;
    background: #007acc;
    color: #fff;
    font-size: 12px;
    flex-shrink: 0;
    gap: 16px;
}

.status-bar.error { background: #c72e2e; }

/* Drop zone overlay */
.drop-overlay {
    display: none;
    position: fixed;
    inset: 0;
    background: rgba(0, 122, 204, 0.2);
    border: 3px dashed #007acc;
    z-index: 1000;
    align-items: center;
    justify-content: center;
    font-size: 1.5em;
    pointer-events: none;
}

.drop-overlay.visible { display: flex; }
```

**Step 2: Rewrite index.html**

```html
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>FlexRender Playground</title>
    <link rel="stylesheet" href="style.css">
    <link rel="preload" id="webassembly" />
    <script type="importmap"></script>
    <script type="module" src="main#[.{fingerprint}].js"></script>
</head>
<body>
    <div id="loading">
        <div class="spinner"></div>
        <div>Loading FlexRender WASM runtime...</div>
    </div>

    <div id="app">
        <!-- Toolbar -->
        <div class="toolbar">
            <h1>FlexRender Playground</h1>
            <select id="examples">
                <option value="">-- Examples --</option>
            </select>
            <div class="spacer"></div>
            <button id="btn-export-png">Export PNG</button>
            <button id="btn-export-svg">Export SVG</button>
        </div>

        <!-- Main content -->
        <div class="main-content">
            <!-- Left: Editors -->
            <div class="editor-panel">
                <div class="editor-section" style="flex: 2">
                    <div class="editor-label">Template (YAML)</div>
                    <div class="editor-container" id="yaml-editor"></div>
                </div>
                <div class="editor-section" style="flex: 1">
                    <div class="editor-label">Data (JSON)</div>
                    <div class="editor-container" id="json-editor"></div>
                </div>
            </div>

            <!-- Right: Preview -->
            <div class="preview-panel">
                <div class="preview-tabs">
                    <button class="active" data-tab="preview">Preview</button>
                    <button data-tab="layout">Layout</button>
                    <button data-tab="errors">Errors</button>
                </div>
                <div class="preview-content">
                    <div id="preview-pane" class="tab-pane active">
                        <img id="preview-img" />
                    </div>
                    <div id="layout-pane" class="tab-pane"></div>
                    <div id="errors-pane" class="tab-pane"></div>
                </div>
            </div>
        </div>

        <!-- Status bar -->
        <div class="status-bar" id="status-bar">
            <span id="status-text">Ready</span>
        </div>
    </div>

    <!-- Drag & drop overlay -->
    <div class="drop-overlay" id="drop-overlay">
        Drop fonts, images, or content files here
    </div>
</body>
</html>
```

**Step 3: Rewrite main.js with Monaco + debounce rendering**

```javascript
import { dotnet } from './_framework/dotnet.js';

// --- Monaco Editor setup (CDN) ---
const MONACO_CDN = 'https://cdn.jsdelivr.net/npm/monaco-editor@0.52.2/min';

function loadScript(src) {
    return new Promise((resolve, reject) => {
        const script = document.createElement('script');
        script.src = src;
        script.onload = resolve;
        script.onerror = reject;
        document.head.appendChild(script);
    });
}

// --- .NET WASM initialization ---
const { getAssemblyExports, getConfig, runMain } = await dotnet
    .withApplicationArguments('start')
    .create();

const config = getConfig();
const exports = await getAssemblyExports(config.mainAssemblyName);
const api = exports.FlexRender.Playground.PlaygroundApi;

await runMain();

// --- Load Monaco from CDN ---
window.require = { paths: { vs: `${MONACO_CDN}/vs` } };
await loadScript(`${MONACO_CDN}/vs/loader.js`);

await new Promise((resolve) => {
    window.require(['vs/editor/editor.main'], resolve);
});

const monaco = window.monaco;

// --- Create editors ---
const defaultYaml = `canvas:
  width: 400
  height: 200
  background: "#ffffff"
elements:
  - type: text
    content: "Hello, FlexRender!"
    size: 28
    color: "#333333"
    padding: "30"
`;

const defaultJson = `{}`;

const yamlEditor = monaco.editor.create(document.getElementById('yaml-editor'), {
    value: defaultYaml,
    language: 'yaml',
    theme: 'vs-dark',
    minimap: { enabled: false },
    fontSize: 13,
    tabSize: 2,
    automaticLayout: true,
    scrollBeyondLastLine: false,
});

const jsonEditor = monaco.editor.create(document.getElementById('json-editor'), {
    value: defaultJson,
    language: 'json',
    theme: 'vs-dark',
    minimap: { enabled: false },
    fontSize: 13,
    tabSize: 2,
    automaticLayout: true,
    scrollBeyondLastLine: false,
});

// --- UI elements ---
const statusBar = document.getElementById('status-bar');
const statusText = document.getElementById('status-text');
const previewImg = document.getElementById('preview-img');
const errorsPane = document.getElementById('errors-pane');
const layoutPane = document.getElementById('layout-pane');

// --- Tabs ---
document.querySelectorAll('.preview-tabs button').forEach(btn => {
    btn.addEventListener('click', () => {
        document.querySelectorAll('.preview-tabs button').forEach(b => b.classList.remove('active'));
        document.querySelectorAll('.tab-pane').forEach(p => p.classList.remove('active'));
        btn.classList.add('active');
        document.getElementById(`${btn.dataset.tab}-pane`).classList.add('active');
    });
});

// --- Render function ---
let renderTimeout = null;
let lastObjectUrl = null;

function render() {
    const yaml = yamlEditor.getValue();
    const json = jsonEditor.getValue();

    statusBar.classList.remove('error');
    statusText.textContent = 'Rendering...';

    try {
        // Validate first
        const errorsJson = api.Validate(yaml);
        const errors = JSON.parse(errorsJson);

        if (errors.length > 0) {
            errorsPane.textContent = errors.map(e => `Line ${e.line}: ${e.message}`).join('\n');
            statusBar.classList.add('error');
            statusText.textContent = `${errors.length} error(s)`;
            return;
        }

        errorsPane.textContent = '';

        // Render
        const start = performance.now();
        const dataArg = json.trim() === '{}' || json.trim() === '' ? null : json;
        const pngBytes = api.RenderToPng(yaml, dataArg);
        const elapsed = (performance.now() - start).toFixed(0);

        if (pngBytes && pngBytes.length > 0) {
            if (lastObjectUrl) URL.revokeObjectURL(lastObjectUrl);
            const blob = new Blob([pngBytes], { type: 'image/png' });
            lastObjectUrl = URL.createObjectURL(blob);
            previewImg.src = lastObjectUrl;
            statusText.textContent = `Rendered in ${elapsed}ms · ${(pngBytes.length / 1024).toFixed(1)} KB`;
        } else {
            statusText.textContent = 'Render returned empty result';
        }
    } catch (e) {
        errorsPane.textContent = e.message || String(e);
        statusBar.classList.add('error');
        statusText.textContent = 'Error';
    }
}

// --- Debounced render on editor changes ---
function scheduleRender() {
    clearTimeout(renderTimeout);
    renderTimeout = setTimeout(render, 300);
}

yamlEditor.onDidChangeModelContent(scheduleRender);
jsonEditor.onDidChangeModelContent(scheduleRender);

// --- Export buttons ---
document.getElementById('btn-export-png').addEventListener('click', () => {
    const yaml = yamlEditor.getValue();
    const json = jsonEditor.getValue();
    const dataArg = json.trim() === '{}' || json.trim() === '' ? null : json;

    try {
        const pngBytes = api.RenderToPng(yaml, dataArg);
        if (pngBytes && pngBytes.length > 0) {
            const blob = new Blob([pngBytes], { type: 'image/png' });
            const a = document.createElement('a');
            a.href = URL.createObjectURL(blob);
            a.download = 'flexrender-output.png';
            a.click();
        }
    } catch (e) {
        alert('Export failed: ' + (e.message || e));
    }
});

// --- Drag & drop ---
const dropOverlay = document.getElementById('drop-overlay');
let dragCounter = 0;

const FONT_EXTENSIONS = ['.ttf', '.otf', '.woff2'];
const IMAGE_EXTENSIONS = ['.png', '.jpg', '.jpeg', '.svg', '.gif', '.webp'];
const CONTENT_EXTENSIONS = ['.ndc', '.txt'];

document.addEventListener('dragenter', (e) => {
    e.preventDefault();
    dragCounter++;
    dropOverlay.classList.add('visible');
});

document.addEventListener('dragleave', (e) => {
    e.preventDefault();
    dragCounter--;
    if (dragCounter === 0) dropOverlay.classList.remove('visible');
});

document.addEventListener('dragover', (e) => e.preventDefault());

document.addEventListener('drop', async (e) => {
    e.preventDefault();
    dragCounter = 0;
    dropOverlay.classList.remove('visible');

    for (const file of e.dataTransfer.files) {
        const name = file.name.toLowerCase();
        const ext = '.' + name.split('.').pop();
        const buffer = new Uint8Array(await file.arrayBuffer());

        if (FONT_EXTENSIONS.includes(ext)) {
            api.LoadFont(file.name, buffer);
            statusText.textContent = `Loaded font: ${file.name}`;
        } else if (IMAGE_EXTENSIONS.includes(ext)) {
            api.LoadImage(file.name, buffer);
            statusText.textContent = `Loaded image: ${file.name}`;
        } else if (CONTENT_EXTENSIONS.includes(ext)) {
            api.LoadContent(file.name, buffer);
            statusText.textContent = `Loaded content: ${file.name}`;
        } else {
            statusText.textContent = `Unsupported file type: ${ext}`;
            continue;
        }

        // Re-render with new resources
        scheduleRender();
    }
});

// --- Show app, trigger initial render ---
document.getElementById('loading').style.display = 'none';
document.getElementById('app').style.display = 'flex';
render();
```

**Step 4: Verify in browser**

```bash
dotnet run --project src/FlexRender.Playground
```

Expected: Full IDE layout with YAML editor (left top), JSON editor (left bottom), preview (right). Editing YAML triggers re-render after 300ms debounce.

**Step 5: Commit**

```bash
git add src/FlexRender.Playground/wwwroot/
git commit -m "feat(playground): add Monaco Editor UI with debounced live preview"
```

---

## Task 6: Example gallery

Built-in example templates selectable from the dropdown.

**Files:**
- Modify: `src/FlexRender.Playground/wwwroot/main.js` (add example loading logic)

**Step 1: Add examples object to main.js**

Add examples as inline JS objects (avoids file loading complexity). Copy 3-4 representative examples from `examples/` directory:

```javascript
const EXAMPLES = {
    'Simple Text': {
        yaml: `canvas:
  width: 400
  height: 150
  background: "#ffffff"
elements:
  - type: text
    content: "Hello, FlexRender!"
    size: 28
    color: "#333333"
    padding: "30"`,
        json: '{}'
    },
    'Flex Layout': {
        yaml: `canvas:
  width: 400
  height: 200
  background: "#f5f5f5"
elements:
  - type: flex
    direction: row
    gap: "10"
    padding: "20"
    children:
      - type: flex
        background: "#4CAF50"
        padding: "20"
        grow: 1
        children:
          - type: text
            content: "Left"
            color: "#ffffff"
            size: 16
      - type: flex
        background: "#2196F3"
        padding: "20"
        grow: 2
        children:
          - type: text
            content: "Right (grow: 2)"
            color: "#ffffff"
            size: 16`,
        json: '{}'
    },
    'Data Binding': {
        yaml: `canvas:
  width: 400
  height: 250
  background: "#ffffff"
elements:
  - type: flex
    padding: "20"
    gap: "8"
    children:
      - type: text
        content: "{{title}}"
        size: 24
        color: "#333"
      - type: text
        content: "By {{author}}"
        size: 14
        color: "#888"
      - type: each
        array: items
        children:
          - type: text
            content: "- {{item}}"
            size: 14
            color: "#555"`,
        json: `{
  "title": "Shopping List",
  "author": "FlexRender",
  "items": ["Apples", "Bread", "Milk", "Cheese"]
}`
    }
};
```

**Step 2: Populate dropdown and wire up selection**

Add after editor creation in main.js:

```javascript
// Populate examples dropdown
const examplesSelect = document.getElementById('examples');
for (const name of Object.keys(EXAMPLES)) {
    const option = document.createElement('option');
    option.value = name;
    option.textContent = name;
    examplesSelect.appendChild(option);
}

examplesSelect.addEventListener('change', () => {
    const example = EXAMPLES[examplesSelect.value];
    if (example) {
        yamlEditor.setValue(example.yaml);
        jsonEditor.setValue(example.json);
    }
});
```

**Step 3: Test examples in browser**

```bash
dotnet run --project src/FlexRender.Playground
```

Expected: Dropdown shows examples, selecting one loads YAML + JSON and triggers render.

**Step 4: Commit**

```bash
git add src/FlexRender.Playground/wwwroot/main.js
git commit -m "feat(playground): add example gallery with built-in templates"
```

---

## Task 7: monaco-yaml integration with JSON Schema

Add YAML autocomplete and validation using monaco-yaml + a FlexRender JSON Schema.

**Files:**
- Create: `src/FlexRender.Playground/wwwroot/schemas/flexrender-template.json`
- Modify: `src/FlexRender.Playground/wwwroot/main.js`

**Step 1: Create JSON Schema for FlexRender templates**

Based on `KnownProperties.cs`, create a JSON Schema covering element types and their properties.

Create `src/FlexRender.Playground/wwwroot/schemas/flexrender-template.json`:

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "FlexRender Template",
  "type": "object",
  "properties": {
    "canvas": {
      "type": "object",
      "properties": {
        "width": { "type": "integer", "description": "Canvas width in pixels" },
        "height": { "type": "integer", "description": "Canvas height in pixels" },
        "background": { "type": "string", "description": "Background color (hex, e.g. #ffffff)" },
        "fixed": { "type": "boolean", "description": "Whether canvas size is fixed" }
      },
      "required": ["width", "height"]
    },
    "fonts": {
      "type": "array",
      "items": {
        "type": "object",
        "properties": {
          "family": { "type": "string" },
          "src": { "type": "string" },
          "weight": { "type": ["string", "integer"] },
          "style": { "type": "string", "enum": ["normal", "italic"] }
        },
        "required": ["family", "src"]
      }
    },
    "elements": {
      "type": "array",
      "items": { "$ref": "#/definitions/element" }
    }
  },
  "required": ["canvas", "elements"],
  "definitions": {
    "flexItemProperties": {
      "type": "object",
      "properties": {
        "grow": { "type": "number", "description": "Flex grow factor" },
        "shrink": { "type": "number", "description": "Flex shrink factor" },
        "basis": { "type": "string", "description": "Flex basis (px, %, auto)" },
        "order": { "type": "integer", "description": "Display order" },
        "display": { "type": "string", "enum": ["flex", "none"] },
        "alignSelf": { "type": "string", "enum": ["auto", "start", "center", "end", "stretch", "baseline"] },
        "width": { "type": ["string", "integer"], "description": "Width (px, %, em, auto)" },
        "height": { "type": ["string", "integer"], "description": "Height (px, %, em, auto)" },
        "minWidth": { "type": ["string", "integer"] },
        "maxWidth": { "type": ["string", "integer"] },
        "minHeight": { "type": ["string", "integer"] },
        "maxHeight": { "type": ["string", "integer"] },
        "padding": { "type": ["string", "integer"], "description": "CSS-like padding shorthand" },
        "margin": { "type": ["string", "integer"], "description": "CSS-like margin shorthand (supports auto)" },
        "background": { "type": "string", "description": "Background color or CSS gradient" },
        "opacity": { "type": "number", "minimum": 0, "maximum": 1 },
        "rotate": { "type": "string" },
        "boxShadow": { "type": "string", "description": "offsetX offsetY blurRadius color" },
        "borderRadius": { "type": ["string", "integer"] },
        "position": { "type": "string", "enum": ["static", "relative", "absolute"] },
        "top": { "type": ["string", "integer"] },
        "right": { "type": ["string", "integer"] },
        "bottom": { "type": ["string", "integer"] },
        "left": { "type": ["string", "integer"] },
        "aspectRatio": { "type": "number" }
      }
    },
    "element": {
      "allOf": [
        { "$ref": "#/definitions/flexItemProperties" },
        {
          "type": "object",
          "required": ["type"],
          "properties": {
            "type": {
              "type": "string",
              "enum": ["text", "flex", "image", "qr", "barcode", "separator", "svg", "table", "each", "if", "content"]
            }
          },
          "allOf": [
            {
              "if": { "properties": { "type": { "const": "text" } } },
              "then": {
                "properties": {
                  "content": { "type": "string", "description": "Text content (supports {{expressions}})" },
                  "font": { "type": "string" },
                  "fontFamily": { "type": "string" },
                  "size": { "type": "number", "description": "Font size in pixels" },
                  "color": { "type": "string" },
                  "align": { "type": "string", "enum": ["left", "center", "right"] },
                  "wrap": { "type": "boolean" },
                  "overflow": { "type": "string", "enum": ["visible", "hidden", "ellipsis"] },
                  "maxLines": { "type": "integer" },
                  "lineHeight": { "type": "number" }
                }
              }
            },
            {
              "if": { "properties": { "type": { "const": "flex" } } },
              "then": {
                "properties": {
                  "direction": { "type": "string", "enum": ["row", "column", "row-reverse", "column-reverse"] },
                  "wrap": { "type": "string", "enum": ["nowrap", "wrap", "wrap-reverse"] },
                  "gap": { "type": ["string", "integer"] },
                  "columnGap": { "type": ["string", "integer"] },
                  "rowGap": { "type": ["string", "integer"] },
                  "justify": { "type": "string", "enum": ["start", "center", "end", "space-between", "space-around", "space-evenly"] },
                  "align": { "type": "string", "enum": ["start", "center", "end", "stretch", "baseline"] },
                  "alignContent": { "type": "string", "enum": ["start", "center", "end", "stretch", "space-between", "space-around", "space-evenly"] },
                  "overflow": { "type": "string", "enum": ["visible", "hidden"] },
                  "children": { "type": "array", "items": { "$ref": "#/definitions/element" } }
                }
              }
            },
            {
              "if": { "properties": { "type": { "const": "image" } } },
              "then": {
                "properties": {
                  "src": { "type": "string", "description": "Image source path or URL" },
                  "objectFit": { "type": "string", "enum": ["fill", "contain", "cover", "none"] }
                },
                "required": ["src"]
              }
            },
            {
              "if": { "properties": { "type": { "const": "qr" } } },
              "then": {
                "properties": {
                  "data": { "type": "string", "description": "QR code data" },
                  "size": { "type": "integer" },
                  "foreground": { "type": "string" },
                  "errorCorrection": { "type": "string", "enum": ["L", "M", "Q", "H"] }
                },
                "required": ["data"]
              }
            },
            {
              "if": { "properties": { "type": { "const": "each" } } },
              "then": {
                "properties": {
                  "array": { "type": "string", "description": "Path to array in data" },
                  "as": { "type": "string", "description": "Iterator variable name" },
                  "children": { "type": "array", "items": { "$ref": "#/definitions/element" } }
                },
                "required": ["array", "children"]
              }
            },
            {
              "if": { "properties": { "type": { "const": "if" } } },
              "then": {
                "properties": {
                  "condition": { "type": "string" },
                  "equals": {},
                  "notEquals": {},
                  "in": { "type": "array" },
                  "notIn": { "type": "array" },
                  "contains": { "type": "string" },
                  "greaterThan": { "type": "number" },
                  "lessThan": { "type": "number" },
                  "hasItems": { "type": "boolean" },
                  "then": { "type": "array", "items": { "$ref": "#/definitions/element" } },
                  "else": { "type": "array", "items": { "$ref": "#/definitions/element" } }
                },
                "required": ["condition", "then"]
              }
            },
            {
              "if": { "properties": { "type": { "const": "separator" } } },
              "then": {
                "properties": {
                  "color": { "type": "string" },
                  "thickness": { "type": "number" },
                  "style": { "type": "string", "enum": ["solid", "dashed", "dotted"] }
                }
              }
            },
            {
              "if": { "properties": { "type": { "const": "content" } } },
              "then": {
                "properties": {
                  "source": { "type": "string", "description": "Content source path" },
                  "format": { "type": "string", "enum": ["ndc", "markdown", "html"], "description": "Content format" },
                  "options": { "type": "object" }
                },
                "required": ["source"]
              }
            }
          ]
        }
      ]
    }
  }
}
```

> **Note:** This is a starting schema. It can be refined later to match `KnownProperties.cs` exactly. The key is getting autocomplete for `type` values and per-type properties.

**Step 2: Integrate monaco-yaml into main.js**

Replace the Monaco loading section with:

```javascript
// Load Monaco + monaco-yaml
// monaco-yaml requires ES module import; use dynamic import after Monaco loader
window.require = { paths: { vs: `${MONACO_CDN}/vs` } };
await loadScript(`${MONACO_CDN}/vs/loader.js`);

await new Promise((resolve) => {
    window.require(['vs/editor/editor.main'], resolve);
});

const monaco = window.monaco;

// Configure YAML schema for autocomplete
// monaco-yaml needs to be loaded as ESM separately
// For now, Monaco's built-in YAML mode provides syntax highlighting
// Full monaco-yaml integration can be added as a follow-up

// Load schema for reference
const schemaResponse = await fetch('schemas/flexrender-template.json');
const flexrenderSchema = await schemaResponse.json();
```

> **Note:** Full `monaco-yaml` integration (with npm-based ESM bundling) is complex to wire into a CDN-only setup. For MVP, use Monaco's built-in YAML syntax highlighting. `monaco-yaml` integration can be added in a follow-up task with a bundler (esbuild/vite).

**Step 3: Commit**

```bash
git add src/FlexRender.Playground/wwwroot/schemas/ src/FlexRender.Playground/wwwroot/main.js
git commit -m "feat(playground): add FlexRender JSON Schema for YAML autocomplete"
```

---

## Task 8: GitHub Actions deployment

CI/CD pipeline to build and deploy to GitHub Pages.

**Files:**
- Create: `.github/workflows/playground.yml`

**Step 1: Write GitHub Actions workflow**

```yaml
name: Deploy Playground

on:
  push:
    branches: [main]
    paths:
      - 'src/FlexRender.Playground/**'
      - 'src/FlexRender.Core/**'
      - 'src/FlexRender.Yaml/**'
      - 'src/FlexRender.Skia.Render/**'
      - '.github/workflows/playground.yml'
  workflow_dispatch:

permissions:
  contents: read
  pages: write
  id-token: write

concurrency:
  group: pages
  cancel-in-progress: true

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Install wasm-tools workload
        run: dotnet workload install wasm-tools

      - name: Publish playground
        run: dotnet publish src/FlexRender.Playground -c Release -o publish

      - name: Compress with Brotli
        run: |
          find publish/wwwroot -type f \( -name "*.js" -o -name "*.wasm" -o -name "*.dll" -o -name "*.json" -o -name "*.css" -o -name "*.html" \) -exec brotli -f -q 11 {} \;

      - name: Upload artifact
        uses: actions/upload-pages-artifact@v3
        with:
          path: publish/wwwroot

  deploy:
    needs: build
    runs-on: ubuntu-latest
    environment:
      name: github-pages
      url: ${{ steps.deployment.outputs.page_url }}
    steps:
      - name: Deploy to GitHub Pages
        id: deployment
        uses: actions/deploy-pages@v4
```

**Step 2: Commit**

```bash
git add .github/workflows/playground.yml
git commit -m "build(playground): add GitHub Actions workflow for Pages deployment"
```

---

## Task 9: Polish and integration testing

Final polish: verify full pipeline, fix any issues, add README section.

**Files:**
- Modify: various files as needed for bug fixes

**Step 1: Full integration test**

```bash
dotnet run --project src/FlexRender.Playground
```

Test checklist:
- [ ] Page loads, spinner shows, then IDE appears
- [ ] Default template renders in preview
- [ ] Editing YAML triggers re-render after 300ms
- [ ] Editing JSON data triggers re-render
- [ ] Validation errors show in Errors tab
- [ ] Example dropdown loads examples
- [ ] Export PNG downloads a file
- [ ] Drag & drop font file → re-render with custom font
- [ ] Drag & drop image file → use in `type: image` with `src: "filename.png"`
- [ ] Drag & drop .ndc file → use in `type: content` with `source: "file.ndc"`
- [ ] Status bar shows render time and file size

**Step 2: Fix any issues found**

Address build errors, runtime errors, or UI glitches found during testing.

**Step 3: Final commit**

```bash
git add -A
git commit -m "feat(playground): complete WASM playground MVP with Monaco Editor"
```

---

## Execution Order Summary

| Task | Description | Depends On | Risk |
|------|-------------|-----------|------|
| 1 | Scaffold wasmbrowser project | — | HIGH (WASM + SkiaSharp compatibility) |
| 2 | MemoryResourceLoader | 1 | LOW |
| 3 | PlaygroundApi (JSExport) | 2 | MEDIUM (JSExport marshalling) |
| 4 | Minimal HTML/JS shell | 3 | HIGH (validates full WASM pipeline) |
| 5 | Monaco Editor UI | 4 | LOW |
| 6 | Example gallery | 5 | LOW |
| 7 | JSON Schema + autocomplete | 5 | LOW |
| 8 | GitHub Actions deployment | 5 | LOW |
| 9 | Polish and testing | 5-8 | LOW |

**Critical path:** Tasks 1 → 4. If SkiaSharp WASM doesn't work, we'll know at Task 4 and can pivot to SVG renderer.

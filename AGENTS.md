# FlexRender

A modular .NET library for rendering images from YAML templates with flexbox layout. Render-backend agnostic with SkiaSharp as the default backend. AOT-compatible, no reflection.

## LLM Documentation

- `llms.txt` -- concise project overview for LLM context windows (~450 lines)
- `llms-full.txt` -- comprehensive reference with all YAML properties, API details, and conventions (~1250 lines)

## Build & Test

```bash
dotnet build FlexRender.slnx          # Build entire solution
dotnet test FlexRender.slnx           # Run all tests
dotnet test --filter "ClassName"       # Filter by test class
dotnet test --filter "MethodName"      # Filter by test method
UPDATE_SNAPSHOTS=true dotnet test      # Regenerate golden snapshot images
```

## Project Structure

```
src/FlexRender.Core/            # Core library (0 external dependencies)
  Abstractions/                 # ILayoutRenderer<T>, ITemplateParser, IResourceLoader
  Configuration/                # ResourceLimits, FlexRenderOptions
  Layout/                       # Two-pass flexbox layout engine (LayoutEngine, LayoutNode, LayoutSize)
    Units/                      # Unit, UnitParser, PaddingValues, PaddingParser, MarginValue, MarginValues
  Loaders/                      # FileResourceLoader, Base64ResourceLoader, EmbeddedResourceLoader
  Parsing/Ast/                  # Template, CanvasSettings, TemplateElement, TextElement, FlexElement, TableElement, TableColumn, TableRow, etc.
  TemplateEngine/               # TemplateProcessor, ExpressionLexer, ExpressionEvaluator, InlineExpressionParser, InlineExpressionEvaluator, FilterRegistry
    Filters/                    # ITemplateFilter, CurrencyFilter, NumberFilter, UpperFilter, LowerFilter, TrimFilter, TruncateFilter, FormatFilter
  Values/                       # TemplateValue hierarchy (StringValue, NumberValue, etc.)

src/FlexRender.Yaml/            # YAML template parser (-> Core + YamlDotNet)
  Parsing/                      # TemplateParser
src/FlexRender.Http/            # HTTP resource loader (-> Core)
src/FlexRender.Skia.Render/     # SkiaSharp renderer (-> Core + SkiaSharp)
  Abstractions/                 # ISkiaRenderer, IFontLoader, IImageLoader, IFontManager
  Rendering/                    # SkiaRenderer, TextRenderer, FontManager, ColorParser, RotationHelper, BmpEncoder, BoxShadowParser, GradientParser
  Loaders/                      # FontLoader, ImageLoader
  Providers/                    # IContentProvider<T,O>, ImageProvider
src/FlexRender.Skia/            # Skia backend meta-package (renderer + providers)
src/FlexRender.QrCode.Skia.Render/     # QR provider for Skia (-> Skia + QRCoder)
src/FlexRender.QrCode.Svg.Render/      # QR provider for SVG (-> Svg)
src/FlexRender.QrCode.ImageSharp.Render/ # QR provider for ImageSharp (-> ImageSharp + QRCoder)
src/FlexRender.QrCode/          # QR meta-package (references all renderers)
src/FlexRender.Barcode.Skia.Render/    # Barcode provider for Skia
src/FlexRender.Barcode.Svg.Render/     # Barcode provider for SVG
src/FlexRender.Barcode.ImageSharp.Render/ # Barcode provider for ImageSharp
src/FlexRender.Barcode/         # Barcode meta-package (references all renderers)
src/FlexRender.SvgElement.Skia.Render/ # SvgElement provider for Skia (-> Svg.Skia)
src/FlexRender.SvgElement.Svg.Render/  # SvgElement provider for SVG (native)
src/FlexRender.SvgElement/      # SvgElement meta-package (references all renderers)
src/FlexRender.ImageSharp.Render/ # ImageSharp renderer (-> Core + SixLabors.ImageSharp)
  Rendering/                    # ImageSharpRenderingEngine, ImageSharpTextRenderer, ImageSharpFontManager
src/FlexRender.ImageSharp/      # ImageSharp backend meta-package (renderer + providers)
src/FlexRender.Svg.Render/      # SVG output renderer (-> Core)
src/FlexRender.Svg/             # SVG backend meta-package (renderer + providers)
src/FlexRender.DependencyInjection/  # Microsoft.Extensions.DI integration
src/FlexRender.MetaPackage/     # Meta-package (core + all backends + DI)

src/FlexRender.Cli/             # CLI tool (System.CommandLine, uses all packages)
  Commands/                     # render, validate, info, watch, debug-layout

tests/FlexRender.Tests/         # Unit + snapshot tests
tests/FlexRender.Cli.Tests/     # CLI integration tests
tests/FlexRender.ImageSharp.Tests/ # ImageSharp visual snapshot tests
examples/                       # Example YAML templates
```

## NuGet Package Structure

```
FlexRender.Core          (0 external deps)
  ^          ^        ^        ^
  |          |        |        |
FlexRender.Yaml  FlexRender.Http  FlexRender.Skia.Render  FlexRender.ImageSharp.Render  FlexRender.Svg.Render
                                    ^           ^                     ^                     ^
                                    |           |                     |                     |
                      Qr/Bar/SvgElement providers per renderer (Skia/Svg/ImageSharp)
                                    |           |                     |
                     FlexRender.QrCode / FlexRender.Barcode / FlexRender.SvgElement (meta)
                                    |           |
                     FlexRender.Skia / FlexRender.ImageSharp / FlexRender.Svg (backend meta)
                                    |
               FlexRender.DependencyInjection  (Microsoft.Extensions.DI)
                                    |
                           FlexRender.MetaPackage  (references all)
```

## SkiaSharp Native Assets on Linux

SkiaSharp is a managed C# binding over the native Skia engine. On Linux (including CI runners and Docker), the native `libSkiaSharp.so` library is not bundled automatically and must be provided via a separate NuGet package.

**When to add:** If the project runs on Linux and SkiaSharp is not installed as a system-wide dependency (e.g., CI, Docker, serverless).

| Package | Use Case |
|---------|----------|
| `SkiaSharp.NativeAssets.Linux` | Standard Linux environments with system libs (fontconfig, freetype) |
| `SkiaSharp.NativeAssets.Linux.NoDependencies` | Minimal/Docker containers without system libs |

The native assets package is added only to **executable projects** (CLI, tests, examples) -- not to library projects like `FlexRender.Skia.Render`. Library consumers bring their own native assets for their target platform.

Similarly, `FlexRender.HarfBuzz` requires `HarfBuzzSharp.NativeAssets.Linux` on Linux. Add it to executable projects that use HarfBuzz text shaping.

**Diagnostics:** if `DllNotFoundException: libSkiaSharp` or `DllNotFoundException: libHarfBuzzSharp` occurs, verify the native library and its dependencies with `ldd /path/to/lib*.so`.

## Architecture

The rendering pipeline:

```
YAML Template
  -> TemplateParser           (YAML -> AST: Template with CanvasSettings + TemplateElement tree)
  -> TemplateExpander         (expand EachElement/IfElement to concrete elements based on data)
  -> TemplateProcessor        (resolve {{variable}} expressions in element properties)
  -> LayoutEngine             (two-pass: MeasureAllIntrinsics -> ComputeLayout -> LayoutNode tree)
  -> SkiaRenderer             (traverse LayoutNode tree -> draw to SKBitmap via SkiaSharp)
     OR ImageSharpRenderer    (traverse LayoutNode tree -> draw via SixLabors.ImageSharp)
```

### FlexRenderBuilder API

The builder pattern provides modular configuration without mandatory DI:

```csharp
// Without DI
var render = new FlexRenderBuilder()
    .WithHttpLoader(configure: opts => {
        opts.Timeout = TimeSpan.FromSeconds(60);
        opts.MaxResourceSize = 20 * 1024 * 1024;
    })
    .WithBasePath("./templates")
    .WithSkia(skia => skia
        .WithQr()
        .WithBarcode())
    .Build();

byte[] png = await render.RenderFile("receipt.yaml", data);

// With DI
services.AddFlexRender(builder => builder
    .WithSkia(skia => skia.WithQr().WithBarcode()));
```

### Template Caching

Templates can be parsed once and cached, then rendered with different data:

```csharp
// Parse once (at startup)
var parser = new TemplateParser();
_templates["receipt"] = await parser.ParseFileAsync("receipt.yaml");

// Render many times (per request)
byte[] png = await render.Render(_templates["receipt"], data);
```

### Two-Pass Layout Engine

1. **Pass 1 -- Intrinsic Measurement** (`MeasureAllIntrinsics`): Bottom-up traversal computes `IntrinsicSize` (MinWidth, MaxWidth, MinHeight, MaxHeight) for every element. Uses `TextMeasurer` delegate for content-based text sizing.
2. **Pass 2 -- Layout** (`ComputeLayout`): Top-down traversal assigns positions and sizes, producing a `LayoutNode` tree with (X, Y, Width, Height).

### Key Classes by Stage

| Stage | Key Classes |
|-------|------------|
| Configuration | `FlexRenderBuilder`, `SkiaBuilder`, `FlexRenderOptions`, `ResourceLimits` |
| Abstractions | `IFlexRender`, `IResourceLoader` |
| Parsing | `TemplateParser`, `Template`, `CanvasSettings`, `TextElement`, `FlexElement`, `QrElement`, `BarcodeElement`, `ImageElement`, `SeparatorElement`, `TableElement`, `TableColumn`, `TableRow`, `EachElement`, `IfElement` |
| Template Engine | `TemplateExpander`, `TemplateProcessor`, `ExpressionLexer`, `ExpressionEvaluator`, `TemplateContext`, `InlineExpressionParser`, `InlineExpressionEvaluator`, `FilterRegistry`, `ITemplateFilter` |
| Layout | `LayoutEngine`, `LayoutNode`, `LayoutContext`, `LayoutSize`, `IntrinsicSize`, `Unit`, `UnitParser`, `MarginValue`, `MarginValues`, `PaddingParser.ParseMargin` |
| Rendering (Skia) | `SkiaRender` (IFlexRender impl), `SkiaRenderer`, `TextRenderer`, `FontManager`, `ColorParser`, `RotationHelper`, `BmpEncoder`, `BoxShadowParser`, `GradientParser` |
| Rendering (ImageSharp) | `ImageSharpRender` (IFlexRender impl), `ImageSharpRenderingEngine`, `ImageSharpTextRenderer`, `ImageSharpFontManager` |
| Providers | `IContentProvider<T,O>`, `QrProvider`, `BarcodeProvider`, `ImageProvider` |
| Loaders | `FileResourceLoader`, `Base64ResourceLoader`, `EmbeddedResourceLoader`, `HttpResourceLoader` |
| DI | `ServiceCollectionExtensions.AddFlexRender()` |
| Values | `TemplateValue` (abstract), `StringValue`, `NumberValue`, `BoolValue`, `NullValue`, `ArrayValue`, `ObjectValue` |

## Coding Conventions

- **.NET 10**, C# latest, `Nullable=enable`, `TreatWarningsAsErrors=true`
- **AOT compatible** -- `IsAotCompatible=true` on library and CLI. No reflection anywhere. Use pattern matching (`switch` on concrete types) for type dispatch
- **`GeneratedRegex`** -- source-generated regex for AOT compatibility
- **`sealed` classes** -- all leaf/value/concrete classes must be `sealed`
- **`sealed record`** -- for token types and small immutable data
- **`readonly record struct`** -- for small value types (`IntrinsicSize`, `LayoutRect`, `PaddingValues`)
- **File-scoped namespaces** -- `namespace Foo.Bar;`
- **XML documentation** -- `<summary>`, `<param>`, `<returns>`, `<exception>` on all public APIs
- **Guard clauses** -- `ArgumentNullException.ThrowIfNull()`, `ArgumentException.ThrowIfNullOrWhiteSpace()`, `ObjectDisposedException.ThrowIf()`
- **Immutability** -- value types are `readonly`, collections exposed as `IReadOnlyList<T>`
- **Thread safety** -- `ConcurrentDictionary` where concurrent access is expected (e.g., `FontManager`)
- **String comparison** -- `StringComparer.OrdinalIgnoreCase` for dictionaries keyed by names

## Resource Limits

All security limits are centralized in the `ResourceLimits` class (`Configuration/ResourceLimits.cs`) and configurable via the `FlexRenderBuilder.WithLimits()` method. Each property validates that values are positive and throws `ArgumentOutOfRangeException` on invalid input.

| Property | Default | Purpose |
|----------|---------|---------|
| `MaxTemplateFileSize` | 1 MB | YAML template file size |
| `MaxDataFileSize` | 10 MB | JSON data file size |
| `MaxTemplateNestingDepth` | 100 | Expression nesting |
| `MaxRenderDepth` | 100 | Render tree recursion |
| `MaxImageSize` | 10 MB | Image loading |
| `MaxFlexLines` | 1000 | Maximum flex lines when wrapping |

Configure limits via builder:

```csharp
services.AddFlexRender(builder => builder
    .WithLimits(limits =>
    {
        limits.MaxRenderDepth = 200;
        limits.MaxTemplateFileSize = 2 * 1024 * 1024;
    }));
```

Or directly when constructing a renderer:

```csharp
var limits = new ResourceLimits { MaxRenderDepth = 200 };
using var renderer = new SkiaRenderer(limits);
```

These limits exist to prevent abuse and resource exhaustion. Never remove or weaken them without explicit justification.

## Test Conventions

- **Total tests**: 1264+ (unit + snapshot + integration, including ImageSharp snapshot tests)
- **Framework**: xUnit with `[Fact]` and `[Theory]`/`[InlineData]`
- **Assertions**: `Assert.*` from xUnit; `FluentAssertions` in some tests
- **Naming**: `MethodUnderTest_Scenario_ExpectedResult` (e.g., `Parse_SimpleTextElement_ParsesCorrectly`)
- **Class naming**: `{ClassName}Tests`
- **Organization**: Mirrors source structure -- `Tests/Values/`, `Tests/Layout/`, `Tests/Parsing/`, etc.
- **Snapshot testing**: `SnapshotTestBase` with golden images in `golden/`. Pixel-by-pixel comparison with `colorThreshold` and `maxDifferencePercent` (0.0 = exact match). Set `UPDATE_SNAPSHOTS=true` to regenerate
- **Pattern**: Arrange-Act-Assert

## Git Conventions

### Branching

All new features and non-trivial changes must be developed in separate branches. Never commit feature work directly to `main`.

- **Branch naming**: `type/short-description` (e.g., `feat/qr-provider`, `fix/layout-overflow`, `refactor/font-manager`)
- **Types**: `feat`, `fix`, `refactor`, `build`, `test`, `docs`, `chore`
- **Do NOT merge into `main`** -- leave the feature branch as-is after completing work. Merging is done manually by the maintainer or via GitHub PR
- **Do NOT use git worktrees** -- work directly in the repository checkout. Worktrees add unnecessary complexity and cause issues with stash conflicts and asset path resolution

### Git LFS & Image URLs

All binary assets (PNG images, fonts) are stored in Git LFS. When referencing images in README or documentation:

- **Use `media.githubusercontent.com`** for LFS-tracked files:
  ```
  https://media.githubusercontent.com/media/RoboNET/FlexRender/main/examples/output/receipt.png
  ```
- **Do NOT use `raw.githubusercontent.com`** -- it returns the LFS pointer file (text), not the actual image content
- LFS-tracked paths: `examples/output/*.png`, `examples/assets/fonts/*.ttf`, `examples/assets/placeholder/*.png`, `examples/visual-docs/output/*.png`

### Commits

Conventional Commits: `type(scope): description`

- **Types**: `feat`, `fix`, `refactor`, `build`, `test`, `docs`, `chore`
- **Scopes** (optional): `parser`, `layout`, `renderer`, `ast`, `examples`, `cli`
- **Description**: lowercase, imperative mood

## CLI Tool

### Install as dotnet tool

```bash
dotnet tool install -g flexrender-cli
```

After installation the `flexrender` command is available globally:

```bash
flexrender render template.yaml -d data.json -o output.png
flexrender validate template.yaml
flexrender info template.yaml
flexrender watch template.yaml -d data.json -o preview.png
flexrender debug-layout template.yaml -d data.json
```

### Native AOT binary

The CLI is fully AOT-compatible. To build a standalone native binary (no .NET runtime required):

```bash
dotnet publish src/FlexRender.Cli -c Release -r <RID> /p:PublishAot=true
```

Common RIDs: `osx-arm64`, `osx-x64`, `linux-x64`, `linux-arm64`, `win-x64`.

### Distribution channels

| Channel | Install | Requires .NET? | Use case |
|---------|---------|---------------|----------|
| dotnet tool | `dotnet tool install -g flexrender-cli` | Yes | .NET developers |
| GitHub Release | Download binary from Releases | No | Everyone else (CI, Docker, non-.NET) |
| Build from source | `dotnet publish /p:PublishAot=true -r <RID>` | Build-time only | Custom builds |

Native AOT binaries are attached to every GitHub Release for: osx-arm64, linux-x64, win-x64.

### NuGet Trusted Publishing (OIDC)

The release workflow uses NuGet trusted publishing via OIDC instead of long-lived API keys. Setup:

1. Go to nuget.org -> username -> Trusted Publishing
2. Add policy: owner/repo = `<owner>/SkiaLayout`, workflow = `release.yml`, tag pattern = `v*`
3. Set `NUGET_USER` secret in GitHub repo settings (nuget.org username, not email)
4. Remove old `NUGET_API_KEY` secret after verifying trusted publishing works

### Run from source (development)

```bash
dotnet run --project src/FlexRender.Cli -- render template.yaml -d data.json -o output.png
dotnet run --project src/FlexRender.Cli -- validate template.yaml
dotnet run --project src/FlexRender.Cli -- info template.yaml
dotnet run --project src/FlexRender.Cli -- watch template.yaml -d data.json -o preview.png
dotnet run --project src/FlexRender.Cli -- debug-layout template.yaml -d data.json
```

Global options: `-v`/`--verbose`, `--fonts <dir>`, `--scale <float>`, `-b`/`--backend <skia|imagesharp>`

The `--backend` option selects the rendering backend: `skia` (default, full features) or `imagesharp` (pure .NET, no native deps, no QR/barcode/SVG element support).

**Working directory matters:** CLI resolves all relative paths (template, data, fonts, image `src`) from the current working directory. When running examples, `cd` into `examples/` first, otherwise assets like `assets/fonts/Inter-Regular.ttf` won't be found.

## Performance & Memory

### Memory Allocation Best Practices

- **Prefer `Span<T>` and `ReadOnlySpan<T>`** -- for slicing strings and arrays without allocation. Use `AsSpan()` instead of `Substring()`
- **Use `Memory<T>` and `ReadOnlyMemory<T>`** -- when span needs to be stored or passed asynchronously
- **`ArrayPool<T>.Shared`** -- for temporary large arrays (>1KB). Always return via `try/finally`
- **`ObjectPool<T>`** -- for expensive objects like `StringBuilder`, `SKPaint`, `SKFont`
- **Avoid LINQ in hot paths** -- `foreach` loops over arrays/lists are faster than `.Where()`, `.Select()`, `.ToList()`
- **Collection capacity hints** -- `new List<T>(capacity)`, `new Dictionary<K,V>(capacity)` when size is known
- **`StringBuilder`** -- for string building in loops instead of `string.Replace()` or `+=`
- **Struct enumerators** -- prefer `foreach` over `List<T>` (uses struct enumerator) vs `IEnumerable<T>` (boxes)

### Hot Paths (Avoid Allocations)

| Component | Hot Path |
|-----------|----------|
| `LayoutEngine` | `ComputeLayout`, `MeasureAllIntrinsics`, `LayoutColumnFlex`, `LayoutRowFlex` |
| `ExpressionLexer` | `Tokenize` -- called per `{{expression}}` |
| `ExpressionEvaluator` | `Resolve` -- called per variable reference |
| `TemplateProcessor` | `ProcessText`, `ProcessEachBlock` |
| `TextRenderer` | `MeasureText`, `WrapText` |

### Pooling Patterns

```csharp
// ArrayPool for temporary buffers
var buffer = ArrayPool<byte>.Shared.Rent(minLength);
try
{
    // use buffer[0..actualLength]
}
finally
{
    ArrayPool<byte>.Shared.Return(buffer);
}

// StringBuilder pooling (consider ObjectPool for high frequency)
var sb = new StringBuilder(estimatedCapacity);
```

### Code Review Checklist (Memory)

- [ ] No LINQ in hot paths (`foreach` instead of `.Where()/.Select()/.ToList()`)
- [ ] Collections have capacity hints where size is predictable
- [ ] Large temporary arrays use `ArrayPool<T>.Shared`
- [ ] String building uses `StringBuilder`, not concatenation in loops
- [ ] `Span<T>` used for string/array slicing instead of `Substring()`/`Array.Copy()`
- [ ] SKPaint/SKFont objects are reused or pooled, not created per-element

## Common Tasks

### Add new element type

1. Create AST model in `Parsing/Ast/` (sealed class extending `TemplateElement`)
2. Add parser function in `TemplateParser.cs` -- register in `_elementParsers` dictionary
3. Add flex-item property support via `switch` pattern matching in layout engine
4. Add rendering in `SkiaRenderer.RenderNode()` or create a provider
5. Write tests for each step

### Add new template expression

1. Add token type as `sealed record` in `ExpressionToken.cs`
2. Add lexer support in `ExpressionLexer.cs`
3. Add evaluation in `TemplateProcessor.cs`
4. Write tests

### Add new property to TemplateElement

1. Add property to `TemplateElement` in `Parsing/Ast/TemplateElement.cs`
2. Parse the property in `ElementParsers.cs`
3. Add the property to `TemplateElement.CopyBaseProperties()` (single source of truth in `Parsing/Ast/TemplateElement.cs`)
4. If the property contains expressions (e.g., `{{variable}}`), also add `ProcessExpression()` calls in the preprocessors
5. Write tests to verify the property survives the full rendering pipeline

## Important Patterns

- **TextMeasurer delegate** -- `Func<TextElement, float, float, LayoutSize>?` on `LayoutEngine` (element, fontSize, maxWidth), wired up by `SkiaRenderer` for content-based text sizing with wrap-aware height measurement
- **Content providers** -- `IContentProvider<TElement,TOutput>` returns rendered content for QR, barcode, image elements
- **Resource loader chain** -- `IResourceLoader` with `CanHandle()`, `Load()`, `Priority` -- chain of responsibility pattern
- **Flex-item properties** -- declared per concrete element class, dispatched via `switch` pattern matching (not on base class)
- **Template processing layers** -- AST-level (`TemplateExpander` for `type: each`/`type: if`) and inline (`TemplateProcessor` for `{{variable}}`)

## AST Element Properties

### TemplateElement (base class)

All elements inherit these properties:

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Rotate` | string | `"none"` | Rotation of the element |
| `Background` | string? | null | Background color in hex or CSS gradient |
| `Opacity` | float | 1.0f | Element opacity (0.0-1.0), affects element and children |
| `BoxShadow` | string? | null | Box shadow: `"offsetX offsetY blurRadius color"` |
| `Padding` | string | `"0"` | Padding inside (px, %, em, CSS shorthand) |
| `Margin` | string | `"0"` | Margin outside (px, %, em, auto, CSS shorthand) |
| `Display` | Display | `Flex` | Display mode (Flex, None) |
| `Grow` | float | 0 | Flex grow factor |
| `Shrink` | float | 1 | Flex shrink factor |
| `Basis` | string | `"auto"` | Flex basis (px, %, em, auto) |
| `AlignSelf` | AlignSelf | `Auto` | Self alignment override |
| `Order` | int | 0 | Display order |
| `Width` | string? | null | Width (px, %, em, auto) |
| `Height` | string? | null | Height (px, %, em, auto) |
| `MinWidth` | string? | null | Minimum width constraint |
| `MaxWidth` | string? | null | Maximum width constraint |
| `MinHeight` | string? | null | Minimum height constraint |
| `MaxHeight` | string? | null | Maximum height constraint |
| `Position` | Position | `Static` | Positioning mode (Static, Relative, Absolute) |
| `Top` | string? | null | Top inset for positioned elements |
| `Right` | string? | null | Right inset for positioned elements |
| `Bottom` | string? | null | Bottom inset for positioned elements |
| `Left` | string? | null | Left inset for positioned elements |
| `AspectRatio` | float? | null | Width/height ratio; when one dimension is known, the other is computed |

### FlexElement (container)

Additional container-only properties:

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Direction` | FlexDirection | `Column` | Main axis direction |
| `Wrap` | FlexWrap | `NoWrap` | Whether items wrap to new lines |
| `Gap` | string | `"0"` | Gap shorthand (sets both row-gap and column-gap) |
| `ColumnGap` | string? | null | Gap between items along main axis |
| `RowGap` | string? | null | Gap between wrapped lines |
| `Justify` | JustifyContent | `Start` | Main axis alignment |
| `Align` | AlignItems | `Stretch` | Cross axis alignment |
| `AlignContent` | AlignContent | `Start` | Alignment of wrapped lines (note: CSS default is Stretch) |
| `Overflow` | Overflow | `Visible` | Content overflow behavior (Visible, Hidden) |

## Layout Enums

All enums in `FlexRender.Layout.FlexEnums`:

| Enum | Values | Description |
|------|--------|-------------|
| `FlexDirection` | Row, Column, RowReverse, ColumnReverse | Main axis direction |
| `FlexWrap` | NoWrap, Wrap, WrapReverse | Line wrapping behavior |
| `JustifyContent` | Start, Center, End, SpaceBetween, SpaceAround, SpaceEvenly | Main axis alignment |
| `AlignItems` | Start, Center, End, Stretch, Baseline | Cross axis alignment |
| `AlignContent` | Start, Center, End, Stretch, SpaceBetween, SpaceAround, SpaceEvenly | Multi-line alignment |
| `AlignSelf` | Auto, Start, Center, End, Stretch, Baseline | Per-item alignment override |
| `Display` | Flex, None | Element visibility in layout |
| `Position` | Static, Relative, Absolute | CSS positioning mode |
| `Overflow` | Visible, Hidden | Content overflow handling |

## Margin Types

`MarginValue` -- readonly record struct representing a single margin side:
- `MarginValue.Fixed(float px)` -- fixed pixel value
- `MarginValue.Auto` -- auto margin that consumes free space
- `ResolvedPixels` -- resolved value (0 for unresolved auto)

`MarginValues` -- readonly record struct for all four sides:
- `Top`, `Right`, `Bottom`, `Left` -- individual `MarginValue` sides
- `HasAuto` -- whether any side is auto
- `MainAxisAutoCount(bool isColumn)` -- count of auto margins on main axis (0, 1, or 2)
- `CrossAxisAutoCount(bool isColumn)` -- count of auto margins on cross axis (0, 1, or 2)
- `MarginValues.Zero` -- all sides zero, no auto

`PaddingParser.ParseMargin(string, float, float)` -- parses CSS margin shorthand with auto support, returns `MarginValues`.

## Control Flow Elements

### Each Element (Iteration)

```yaml
- type: each
  array: items              # path to array in data (required)
  as: item                  # variable name (optional)
  children:
    - type: text
      content: "{{item.name}}: {{item.price}}"
```

Loop variables available inside `each`:
- `{{@index}}` -- zero-based index
- `{{@first}}` -- true for first item
- `{{@last}}` -- true for last item

### If Element (Conditional)

Supports 12 operators: truthy (no key), `equals`, `notEquals`, `in`, `notIn`, `contains`, `greaterThan`, `greaterThanOrEqual`, `lessThan`, `lessThanOrEqual`, `hasItems`, `countEquals`, `countGreaterThan`.

```yaml
# Truthy check
- type: if
  condition: isPremium
  then:
    - type: text
      content: "Premium"

# Equality (strings, numbers, bool, arrays, null)
- type: if
  condition: status
  equals: "paid"
  then:
    - type: text
      content: "Paid"

# In list
- type: if
  condition: role
  in: ["admin", "moderator"]
  then:
    - type: text
      content: "Staff"

# Numeric comparison
- type: if
  condition: total
  greaterThan: 1000
  then:
    - type: text
      content: "Large order"

# Array checks
- type: if
  condition: items
  hasItems: true
  then:
    - type: each
      array: items
      children:
        - type: text
          content: "{{item.name}}"

# Else-if chain
- type: if
  condition: status
  equals: "paid"
  then:
    - type: text
      content: "Paid"
  elseIf:
    condition: status
    equals: "pending"
    then:
      - type: text
        content: "Pending"
  else:
    - type: text
      content: "Unknown"
```

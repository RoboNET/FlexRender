# FlexRender

A modular .NET library for rendering images from YAML templates with flexbox layout. Render-backend agnostic with SkiaSharp as the default backend. AOT-compatible, no reflection.

## LLM Documentation

- `llms.txt` -- concise project overview for LLM context windows (~200 lines)
- `llms-full.txt` -- comprehensive reference with all YAML properties, API details, and conventions (~580 lines)

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
    Units/                      # Unit, UnitParser, PaddingValues, PaddingParser
  Loaders/                      # FileResourceLoader, Base64ResourceLoader, EmbeddedResourceLoader, HttpResourceLoader
  Parsing/Ast/                  # Template, CanvasSettings, TemplateElement, TextElement, FlexElement, etc.
  TemplateEngine/               # TemplateProcessor, ExpressionLexer, ExpressionEvaluator
  Values/                       # TemplateValue hierarchy (StringValue, NumberValue, etc.)

src/FlexRender.Yaml/            # YAML template parser (-> Core + YamlDotNet)
  Parsing/                      # TemplateParser, YamlPreprocessor
src/FlexRender.Skia/            # SkiaSharp renderer (-> Core + SkiaSharp)
  Abstractions/                 # IFlexRenderer, IFontLoader, IImageLoader, IFontManager
  Rendering/                    # SkiaRenderer, TextRenderer, FontManager, ColorParser, RotationHelper
  Loaders/                      # FontLoader, ImageLoader
  Providers/                    # IContentProvider<T,O>, ImageProvider
src/FlexRender.QrCode/          # QR code provider (-> Skia + QRCoder)
src/FlexRender.Barcode/         # Barcode provider (-> Skia)
src/FlexRender.DependencyInjection/  # Microsoft.Extensions.DI integration
src/FlexRender.MetaPackage/     # Meta-package (references all sub-packages)

src/FlexRender.Cli/             # CLI tool (System.CommandLine, uses all packages)
  Commands/                     # render, validate, info, watch, debug-layout

tests/FlexRender.Tests/         # Unit + snapshot tests
tests/FlexRender.Cli.Tests/     # CLI integration tests
examples/                       # Example YAML templates
```

## NuGet Package Structure

```
FlexRender.Core          (0 external deps)
  ^          ^
  |          |
FlexRender.Yaml    FlexRender.Skia       (YamlDotNet)  (SkiaSharp)
                     ^        ^
                     |        |
            FlexRender.QrCode  FlexRender.Barcode   (QRCoder)
                     |        |
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

The native assets package is added only to **executable projects** (CLI, tests, examples) -- not to library projects like `FlexRender.Skia`. Library consumers bring their own native assets for their target platform.

**Diagnostics:** if `DllNotFoundException: libSkiaSharp` occurs, verify the native library and its dependencies with `ldd /path/to/libSkiaSharp.so`.

## Architecture

The rendering pipeline:

```
YAML Template
  -> YamlPreprocessor        (expand {{#each}}, {{#if}} at YAML level)
  -> TemplateParser           (YAML -> AST: Template with CanvasSettings + TemplateElement tree)
  -> TemplateProcessor        (resolve {{variable}} expressions in element properties)
  -> LayoutEngine             (two-pass: MeasureAllIntrinsics -> ComputeLayout -> LayoutNode tree)
  -> SkiaRenderer             (traverse LayoutNode tree -> draw to SKBitmap via SkiaSharp)
```

### Two-Pass Layout Engine

1. **Pass 1 -- Intrinsic Measurement** (`MeasureAllIntrinsics`): Bottom-up traversal computes `IntrinsicSize` (MinWidth, MaxWidth, MinHeight, MaxHeight) for every element. Uses `TextMeasurer` delegate for content-based text sizing.
2. **Pass 2 -- Layout** (`ComputeLayout`): Top-down traversal assigns positions and sizes, producing a `LayoutNode` tree with (X, Y, Width, Height).

### Key Classes by Stage

| Stage | Key Classes |
|-------|------------|
| Preprocessing | `YamlPreprocessor` |
| Parsing | `TemplateParser`, `Template`, `CanvasSettings`, `TextElement`, `FlexElement`, `QrElement`, `BarcodeElement`, `ImageElement`, `SeparatorElement` |
| Template Engine | `TemplateProcessor`, `ExpressionLexer`, `ExpressionEvaluator`, `TemplateContext` |
| Layout | `LayoutEngine`, `LayoutNode`, `LayoutContext`, `LayoutSize`, `IntrinsicSize`, `Unit`, `UnitParser` |
| Rendering | `SkiaRenderer`, `TextRenderer`, `FontManager`, `ColorParser`, `RotationHelper` |
| Providers | `IContentProvider<T,O>`, `QrProvider`, `BarcodeProvider`, `ImageProvider` |
| DI | `ServiceCollectionExtensions.AddFlexRender()`, `FlexRenderBuilder`, `FlexRenderOptions` |
| Abstractions | `IFlexRenderer`, `ILayoutRenderer<T>`, `ITemplateParser` |
| Values | `TemplateValue` (abstract), `StringValue`, `NumberValue`, `BoolValue`, `NullValue`, `ArrayValue`, `ObjectValue` |

## Coding Conventions

- **.NET 10**, C# latest, `Nullable=enable`, `TreatWarningsAsErrors=true`
- **AOT compatible** -- `IsAotCompatible=true` on library and CLI. No reflection anywhere. Use pattern matching (`switch` on concrete types) for type dispatch
- **`GeneratedRegex`** -- source-generated regex for AOT compatibility
- **`sealed` classes** -- all leaf/value/concrete classes must be `sealed`
- **`sealed record`** -- for token types and small immutable data
- **`readonly record struct`** -- for small value types (`IntrinsicSize`, `LayoutRect`, `FlexItemProperties`)
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
| `MaxPreprocessorNestingDepth` | 50 | Preprocessing block nesting |
| `MaxPreprocessorInputSize` | 1 MB | Preprocessor input |
| `MaxTemplateNestingDepth` | 100 | Expression nesting |
| `MaxRenderDepth` | 100 | Render tree recursion |
| `MaxImageSize` | 10 MB | Image loading |
| `HttpTimeout` | 30s | Remote resource loading |

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
- **Before completing a branch** -- squash all commits into a single commit with a comprehensive message. This keeps the main branch history clean:
  ```bash
  git reset --soft origin/main        # Squash: soft reset to main
  git commit -m "type(scope): description"  # Single commit with full message
  ```

### Commits

Conventional Commits: `type(scope): description`

- **Types**: `feat`, `fix`, `refactor`, `build`, `test`, `docs`, `chore`
- **Scopes** (optional): `parser`, `layout`, `renderer`, `ast`, `examples`, `cli`
- **Description**: lowercase, imperative mood

## CLI Tool

```bash
dotnet run --project src/FlexRender.Cli -- render template.yaml -d data.json -o output.png
dotnet run --project src/FlexRender.Cli -- validate template.yaml
dotnet run --project src/FlexRender.Cli -- info template.yaml
dotnet run --project src/FlexRender.Cli -- watch template.yaml -d data.json -o preview.png
dotnet run --project src/FlexRender.Cli -- debug-layout template.yaml -d data.json
```

Global options: `-v`/`--verbose`, `--fonts <dir>`, `--scale <float>`

**Working directory matters:** CLI resolves all relative paths (template, data, fonts, image `src`) from the current working directory. When running examples, `cd` into `examples/` first, otherwise assets like `assets/fonts/Inter-Regular.ttf` won't be found.

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

## Important Patterns

- **TextMeasurer delegate** -- `Func<TextElement, float, float, LayoutSize>?` on `LayoutEngine` (element, fontSize, maxWidth), wired up by `SkiaRenderer` for content-based text sizing with wrap-aware height measurement
- **Content providers** -- `IContentProvider<TElement,TOutput>` returns rendered content for QR, barcode, image elements
- **Resource loader chain** -- `IResourceLoader` with `CanHandle()`, `Load()`, `Priority` -- chain of responsibility pattern
- **Flex-item properties** -- declared per concrete element class, dispatched via `switch` pattern matching (not on base class)
- **Template processing layers** -- structural (`YamlPreprocessor` for `{{#each}}`/`{{#if}}`) and inline (`TemplateProcessor` for `{{variable}}`)

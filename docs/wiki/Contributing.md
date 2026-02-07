# Contributing

This guide covers building, testing, architecture, and coding conventions for FlexRender contributors.

## Build & Test

```bash
dotnet build FlexRender.slnx          # Build entire solution
dotnet test FlexRender.slnx           # Run all tests
dotnet test --filter "ClassName"       # Filter by test class
dotnet test --filter "MethodName"      # Filter by test method
UPDATE_SNAPSHOTS=true dotnet test      # Regenerate golden snapshot images
```

**Requirements:** .NET 10 SDK

---

## Project Structure

```
src/FlexRender.Core/            # Core library (0 external dependencies)
  Abstractions/                 # IFlexRender, IResourceLoader, ILayoutRenderer<T>
  Configuration/                # FlexRenderBuilder, FlexRenderOptions, ResourceLimits
  Layout/                       # Two-pass flexbox layout engine (LayoutEngine facade + strategy classes)
    Units/                      # Unit, UnitParser, PaddingValues, PaddingParser
  Loaders/                      # FileResourceLoader, Base64ResourceLoader, EmbeddedResourceLoader
  Parsing/Ast/                  # Template, CanvasSettings, TemplateElement tree
  TemplateEngine/               # TemplateProcessor, ExpressionLexer, ExpressionEvaluator
  Values/                       # TemplateValue hierarchy

src/FlexRender.Yaml/            # YAML template parser (-> Core + YamlDotNet)
  Parsing/                      # TemplateParser facade, ElementParsers, YamlPropertyHelpers
src/FlexRender.Http/            # HTTP resource loader (-> Core)
src/FlexRender.Skia/            # SkiaSharp renderer (-> Core + SkiaSharp)
  Rendering/                    # SkiaRenderer facade, RenderingEngine, TemplatePreprocessor, TextRenderer, FontManager, BmpEncoder
  Providers/                    # ImageProvider, IContentProvider<T>
src/FlexRender.QrCode/          # QR code provider (-> Skia + QRCoder)
src/FlexRender.Barcode/         # Barcode provider (-> Skia)
src/FlexRender.DependencyInjection/  # Microsoft DI integration
src/FlexRender.MetaPackage/     # Meta-package (references all)
src/FlexRender.Cli/             # CLI tool (System.CommandLine)

tests/FlexRender.Tests/         # Unit + snapshot tests
tests/FlexRender.Cli.Tests/     # CLI integration tests
examples/                       # Example YAML templates
```

---

## Architecture Pipeline

```
YAML Template
  -> TemplateParser           (YAML -> AST: Template with CanvasSettings + TemplateElement tree)
  -> TemplateExpander         (expand EachElement/IfElement to concrete elements based on data)
  -> TemplateProcessor        (resolve {{variable}} expressions in element properties)
  -> LayoutEngine             (two-pass: MeasureAllIntrinsics -> ComputeLayout -> LayoutNode tree)
  -> SkiaRenderer             (traverse LayoutNode tree -> draw to SKBitmap via SkiaSharp)
```

### Key Classes by Stage

| Stage | Key Classes |
|-------|-------------|
| Configuration | `FlexRenderBuilder`, `SkiaBuilder`, `FlexRenderOptions`, `ResourceLimits` |
| Abstractions | `IFlexRender`, `IResourceLoader` |
| Parsing | `TemplateParser` (facade), `ElementParsers`, `YamlPropertyHelpers`, `Template`, `CanvasSettings`, `TextElement`, `FlexElement`, `QrElement`, `BarcodeElement`, `ImageElement`, `SeparatorElement`, `EachElement`, `IfElement` |
| Template Engine | `TemplateExpander`, `TemplateProcessor`, `ExpressionLexer`, `ExpressionEvaluator` |
| Layout | `LayoutEngine` (facade), `IntrinsicMeasurer`, `ColumnFlexLayoutStrategy`, `RowFlexLayoutStrategy`, `WrappedFlexLayoutStrategy`, `LayoutHelpers`, `LayoutNode`, `LayoutContext`, `LayoutSize`, `IntrinsicSize` |
| Rendering | `SkiaRender` (IFlexRender impl), `SkiaRenderer` (facade), `RenderingEngine`, `TemplatePreprocessor`, `TextRenderer`, `FontManager`, `BmpEncoder` |
| Providers | `IContentProvider<T>`, `QrProvider`, `BarcodeProvider`, `ImageProvider` |
| Values | `TemplateValue`, `StringValue`, `NumberValue`, `BoolValue`, `NullValue`, `ArrayValue`, `ObjectValue` |

### Important Patterns

- **TextMeasurer delegate** -- `Func<TextElement, float, float, LayoutSize>?` on `LayoutEngine`, wired up by `SkiaRenderer` for content-based text sizing
- **Content providers** -- `IContentProvider<TElement>` returns rendered content for QR, barcode, image elements
- **Resource loader chain** -- `IResourceLoader` with `CanHandle()`, `Load()`, `Priority` (chain of responsibility)
- **Switch-based dispatch** -- element type dispatch uses `switch` on concrete types, not base class properties
- **Template processing layers** -- AST-level (`TemplateExpander`) and inline (`TemplateProcessor`)

---

## Coding Conventions

### Language & Framework

- **.NET 10**, C# latest, `Nullable=enable`, `TreatWarningsAsErrors=true`
- **File-scoped namespaces** -- `namespace Foo.Bar;`

### AOT Compatibility

- `IsAotCompatible=true` on all library and CLI projects
- **No reflection anywhere** -- no `Type.GetType()`, no `dynamic`
- **`GeneratedRegex`** for all regex patterns
- Pattern matching (`switch` on concrete types) for type dispatch

### Type Conventions

- **`sealed` classes** -- all leaf/value/concrete classes must be `sealed`
- **`sealed record`** -- for token types and small immutable data
- **`readonly record struct`** -- for small value types (`IntrinsicSize`, `LayoutRect`, `PaddingValues`)
- **Immutability** -- value types are `readonly`, collections exposed as `IReadOnlyList<T>`
- **Thread safety** -- `ConcurrentDictionary` where concurrent access is expected

### API Surface

- **XML documentation** -- `<summary>`, `<param>`, `<returns>`, `<exception>` on all public APIs
- **Guard clauses** -- `ArgumentNullException.ThrowIfNull()`, `ArgumentException.ThrowIfNullOrWhiteSpace()`
- **String comparison** -- `StringComparer.OrdinalIgnoreCase` for dictionaries keyed by names

### Resource Limits

All security limits are in `ResourceLimits`. Never remove or weaken them without explicit justification.

---

## Test Conventions

- **Framework:** xUnit with `[Fact]` and `[Theory]`/`[InlineData]`
- **Assertions:** `Assert.*` from xUnit; `FluentAssertions` in some tests
- **Naming:** `MethodUnderTest_Scenario_ExpectedResult`
- **Class naming:** `{ClassName}Tests`
- **Organization:** mirrors source structure
- **Pattern:** Arrange-Act-Assert

### Snapshot Testing

`SnapshotTestBase` with golden images in `golden/`. Pixel-by-pixel comparison with configurable `colorThreshold` and `maxDifferencePercent`.

```bash
# Regenerate golden snapshot images
UPDATE_SNAPSHOTS=true dotnet test
```

---

## Git Conventions

### Branching

- All features and non-trivial changes in separate branches
- **Branch naming:** `type/short-description` (e.g., `feat/qr-provider`, `fix/layout-overflow`)
- **Types:** `feat`, `fix`, `refactor`, `build`, `test`, `docs`, `chore`
- Do NOT merge into `main` -- leave feature branches for PR review

### Commits

Conventional Commits: `type(scope): description`

- **Types:** `feat`, `fix`, `refactor`, `build`, `test`, `docs`, `chore`
- **Scopes (optional):** `parser`, `layout`, `renderer`, `ast`, `examples`, `cli`
- **Description:** lowercase, imperative mood

Examples:
```
feat(parser): add barcode element support
fix(layout): correct flex-basis resolution for percentage values
refactor(renderer): extract text measurement into delegate
test(layout): add wrap-reverse alignment tests
```

---

## How to Add a New Element Type

1. Create a `sealed` AST class in `Parsing/Ast/` extending `TemplateElement`
2. Add to `ElementType` enum
3. Add parser function in `TemplateParser.cs` -- register in `_elementParsers` dictionary
4. Add flex-item property support via `switch` pattern matching in the layout engine
5. Add rendering in `SkiaRenderer.RenderNode()` or create a provider implementing `IContentProvider<T>`
6. Write tests for each step

---

## How to Add a New Template Expression

1. Add token type as `sealed record` in `ExpressionToken.cs`
2. Add lexer support in `ExpressionLexer.cs`
3. Add evaluation in `TemplateProcessor.cs`
4. Write tests

---

## How to Add a New Barcode Format

1. Add format to `BarcodeFormat` enum in `Parsing/Ast/BarcodeElement.cs`
2. Add parsing case in `TemplateParser.ParseBarcodeElement()`
3. Add generation in `BarcodeProvider`
4. Write tests

---

## Code Review Checklist

When reviewing code changes, verify:

- [ ] AOT safe -- no reflection, no `dynamic`, no `Type.GetType()`. Use `GeneratedRegex` for regex
- [ ] Classes are `sealed` unless designed for inheritance
- [ ] Null checks via `ArgumentNullException.ThrowIfNull()` -- not manual `if (x == null)`
- [ ] Resource limits preserved -- never remove or weaken `MaxFileSize`, `MaxNestingDepth`, `MaxRenderDepth`
- [ ] New element types follow switch-based dispatch pattern
- [ ] XML docs on all public API surface
- [ ] Snapshot tests added/updated for visual changes
- [ ] No LINQ in hot paths (`foreach` instead of `.Where()`/`.Select()`/`.ToList()`)
- [ ] Collections have capacity hints where size is predictable
- [ ] Large temporary arrays use `ArrayPool<T>.Shared`

---

## Native Assets on Linux

SkiaSharp and HarfBuzzSharp require native libraries on Linux. Add to executable projects (not libraries):

| Package | Use Case |
|---------|----------|
| `SkiaSharp.NativeAssets.Linux` | Standard Linux with fontconfig/freetype |
| `SkiaSharp.NativeAssets.Linux.NoDependencies` | Minimal/Docker containers |
| `HarfBuzzSharp.NativeAssets.Linux` | Required when using `FlexRender.HarfBuzz` |

If `DllNotFoundException: libSkiaSharp` or `libHarfBuzzSharp` occurs, verify with `ldd /path/to/lib*.so`.

## See Also

- [[API-Reference]] -- full API documentation
- [[Template-Syntax]] -- YAML template reference
- [AGENTS.md](../../AGENTS.md) -- additional contributor guidelines

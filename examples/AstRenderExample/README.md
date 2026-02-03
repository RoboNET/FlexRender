# AST Render Example

Demonstrates building and rendering an image entirely from code, without YAML templates.

## What this example shows

- Constructing a `Template` with `CanvasSettings` and elements programmatically
- Using `TextElement`, `FlexElement`, and `SeparatorElement` AST nodes directly
- Rendering with `SkiaRenderer` without dependency injection
- Zero YAML dependency -- only `FlexRender.Core` and `FlexRender.Skia`

## Packages used

- `FlexRender.Core` -- AST types, layout engine, template values
- `FlexRender.Skia` -- SkiaSharp rendering

## Run

```bash
dotnet run --project examples/AstRenderExample
```

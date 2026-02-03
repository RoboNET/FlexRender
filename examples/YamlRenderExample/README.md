# YAML Render Example

Demonstrates rendering an image from a YAML template using FlexRender with dependency injection.

## What this example shows

- Parsing a YAML template file with `TemplateParser`
- Setting up FlexRender via `Microsoft.Extensions.DependencyInjection`
- Rendering to a PNG file using `IFlexRenderer`
- Passing template data with `ObjectValue`

## Packages used

- `FlexRender.Core` -- template values and abstractions
- `FlexRender.Yaml` -- YAML template parsing
- `FlexRender.Skia` -- SkiaSharp rendering
- `FlexRender.DependencyInjection` -- DI integration

## Run

```bash
dotnet run --project examples/YamlRenderExample
```

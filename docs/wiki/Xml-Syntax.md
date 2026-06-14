# XML Syntax

FlexRender templates can be authored in **XML** as an alternative to YAML. The XML parser (`FlexRender.Xml`) produces the **same `Template` AST** as the YAML parser -- same element types, same properties, same validation, same resource limits. Nothing downstream of parsing changes.

XML is offered because LLMs and code generators often emit XML attributes more reliably than YAML indentation. Pick whichever syntax suits your tooling; both render identically.

For the YAML syntax and the full property reference, see [[Template-Syntax]] and [[Element-Reference]]. For template expressions (variables, loops, conditionals), see [[Template-Expressions]].

## At a Glance

```xml
<flexrender>
  <canvas width="300"/>
  <text size="1.5em">Hello</text>
</flexrender>
```

```csharp
var render = new FlexRenderBuilder()
    .WithSkia(skia => skia.WithQr().WithBarcode())
    .Build();

byte[] png = await render.RenderXml(xml, data);
```

The XML parser does **not** reimplement element parsing. It converts the XML tree into the equivalent structure the YAML parser produces and reuses the existing element parsers. The mapping rules below define that conversion. They are designed to be LLM-friendly (attributes over indentation) while round-tripping cleanly to the same AST.

## Document Shape

| YAML | XML |
| --- | --- |
| top-level `template:`, `canvas:`, `fonts:`, `layout:` | a single root element `<flexrender>` |
| `canvas: { width: 300, ... }` | `<canvas width="300" .../>` child of `<flexrender>` |
| `template: { name, version, culture }` | attributes on `<flexrender>`: `name`, `version`, `culture` |
| `layout: [ ...elements ]` | every **other** child element of `<flexrender>` (in order) is a layout element |
| `fonts: { default: "x.ttf" }` | `<fonts>` child containing `<font name="default" path="x.ttf" fallback="Arial"/>` entries |

## Element Shape

- **Element type = XML local element name.** `<text/>` -> `type: text`, `<flex>` -> `type: flex`, `<chart/>`, `<rect/>`, `<each>`, `<if>`, `<table>`, etc. The set of names is exactly the YAML element types.
- **Scalar properties = XML attributes.** `<text size="1em" color="#ff0000"/>` -> `size: 1em`, `color: "#ff0000"`. Attribute names are identical to YAML property names (kebab-case and camelCase both pass through unchanged, e.g. `chart-type`, `stroke-width`, `min-width`, `borderColor`). Validation against `KnownProperties` is unchanged.
- **`text` content** may be given either as the `content` attribute **or** as the element's inner text: `<text>Hello</text>` is equivalent to `<text content="Hello"/>`. If both are present, the `content` attribute wins. (svg `content` follows the same rule.)
- **Child-element containers** map to list properties:
  - `flex` children -> child layout elements directly inside `<flex>`.
  - `each` body -> child layout elements directly inside `<each>` (YAML `children`).
  - `if` -> `<then>` and `<else>` wrapper children, each holding layout elements; optional `<else-if>` wrapper holds a single nested `if` (YAML `elseIf`). Comparison operators (`equals`, `in`, `greaterThan`, ...) remain attributes on `<if>`.
  - `table` -> `<columns>` wrapper holding `<column .../>` entries; optional `<rows>` wrapper holding `<row .../>` entries. Column/row fields are attributes.
  - `chart` -> `<series .../>` entries (each a child of `<chart>`), `<categories>` wrapper holding `<item>value</item>` entries, `<x-labels>`/`<y-labels>` wrappers holding `<item>` entries, `<palette>` wrapper holding `<color>#fff</color>` entries (or a `palette="ocean"` attribute for a named palette).
  - `draw` -> `<shapes>` wrapper holding one-shape-per-child elements `<line/>`, `<polyline/>`, `<rect/>`, `<circle/>`, `<path/>`.
- **Scalar lists** (chart `series.data`, `categories`, polyline `points`):
  - `series` numeric data: `data="12,30,22,48"` attribute (comma-separated) -> YAML `data: [12,30,22,48]`. An expression `data="{{ sales }}"` passes through as a scalar string.
  - tuple data (scatter/bubble): `points="1,2; 3,4; 5,6"` attribute on `<series>` -> YAML `data: [[1,2],[3,4],[5,6]]`. (Semicolon separates points, comma separates components.)
  - `draw polyline` `points="10,10; 50,50"` -> YAML `points: [[10,10],[50,50]]`.

## How the Converter Decides Attribute vs. List-Container

The converter is generic and data-driven. For each XML element it produces a mapping with:

1. `type` = the element local-name (except the special wrappers below).
2. one scalar entry per attribute (name -> value), with comma/semicolon list attributes expanded into a sequence when the attribute name is a known list property (`data`, `points`, `categories`, `palette`, `x-labels`, `y-labels`).
3. one entry per recognised **child wrapper**: child elements named `then`/`else`/`else-if`/`columns`/`rows`/`series`/`categories`/`x-labels`/`y-labels`/`palette`/`shapes` become the corresponding sequence/mapping; all **other** child elements are collected into the container's natural list key (`children` for `flex`/`each`, `layout` for the root, the shape list for `draw`, etc.).

## Side-by-Side Example (flex + chart)

YAML:

```yaml
canvas:
  width: 400
layout:
  - type: flex
    direction: row
    gap: 8
    children:
      - type: text
        content: "Quarterly sales"
        size: 1.2em
      - type: chart
        chart-type: bar
        categories: [Q1, Q2, Q3, Q4]
        series:
          - label: "2024"
            data: [12, 30, 22, 48]
          - label: "2025"
            data: [18, 26, 31, 40]
```

XML:

```xml
<flexrender>
  <canvas width="400"/>
  <flex direction="row" gap="8">
    <text size="1.2em">Quarterly sales</text>
    <chart chart-type="bar">
      <categories>
        <item>Q1</item><item>Q2</item><item>Q3</item><item>Q4</item>
      </categories>
      <series label="2024" data="12,30,22,48"/>
      <series label="2025" data="18,26,31,40"/>
    </chart>
  </flex>
</flexrender>
```

Both parse to the identical `Template` AST.

## Rendering XML

Use the `RenderXml` extension method, which mirrors `RenderYaml`: it parses the XML string into the shared `Template` AST via `XmlTemplateParser` and hands it to the same render pipeline.

```csharp
var render = new FlexRenderBuilder()
    .WithSkia()
    .Build();

var xml = """
<flexrender>
  <canvas width="300" height="200"/>
  <text content="Hello, {{name}}!"/>
</flexrender>
""";

var data = new ObjectValue { ["name"] = new StringValue("World") };
byte[] png = await render.RenderXml(xml, data);
```

To parse once and reuse, create an `XmlTemplateParser` and pass it via the optional `parser` argument (the same pattern as `TemplateParser` for YAML):

```csharp
var parser = new XmlTemplateParser();
byte[] png = await render.RenderXml(xml, data, parser: parser);
```

## Validation and Limits Are Identical to YAML

Because XML lowers to the same intermediate structure and reuses the YAML element parsers, everything that applies to YAML applies to XML unchanged:

- **`KnownProperties` validation** -- unknown attributes are flagged with the same "Did you mean?" typo suggestions.
- **`ResourceLimits`** -- `MaxFileSize`, `MaxNestingDepth`, `MaxRenderDepth` are enforced exactly as for YAML.
- **Expressions** -- `{{variable}}`, inline expressions, loops (`<each>`), and conditionals (`<if>`) behave identically.
- **Malformed input** -- malformed XML throws `TemplateParseException`, just as malformed YAML does.

## See Also

- [[Template-Syntax]] -- YAML template structure, canvas, units
- [[Element-Reference]] -- complete property reference for every element type
- [[Template-Expressions]] -- variables, loops, conditionals
- [[API-Reference]] -- `IFlexRender`, builder, extension methods

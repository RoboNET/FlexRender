# Charts and Shape Primitives for LLM-Generated Graphics

Enable LLM agents to generate polished charts and custom graphics through declarative YAML templates, rendered via Skia. No hand-written SVG markup required. Two layers: high-level `chart` elements (agent describes data, library draws it beautifully) and low-level shape primitives (`rect`, `circle`, `ellipse`, `draw`) for custom decoration and free-form drawing. (The free-form element is named `draw`, not `canvas`, to avoid confusion with the template root `canvas:` settings section.)

## Goals

- An agent picks a chart type, supplies data arrays, and optionally one theme/palette word — the output looks professional with zero styling decisions.
- Shape primitives cover dashboard decoration (status dots, badges, dividers) and arbitrary line/path drawing.
- Zero new external dependencies: all math and drawing use existing Core + SkiaSharp.
- Follows existing patterns: AST in `FlexRender.Core`, rendering in `FlexRender.Skia`, switch-based element dispatch, `KnownProperties.cs` registration, resource limits.

## Architecture

Charts and shapes have no external dependencies, so — unlike QrCode/Barcode — no new packages are needed:

- **AST**: new element classes in `src/FlexRender.Core/Parsing/Ast/` (`RectElement`, `CircleElement`, `EllipseElement`, `DrawElement`, `ChartElement`).
- **Chart layout math** (axis scales, nice ticks, legend measurement): `src/FlexRender.Core/Charts/` — pure, testable, renderer-agnostic.
- **Drawing**: `FlexRender.Skia` render visitors, same dispatch pattern as `TableElement`.
- **Parsing**: `FlexRender.Yaml` `TemplateParser` extended for new element types; all new properties registered in `KnownProperties.cs` for validation and typo suggestions.

## Layer 1: Shape Primitives

### Box shapes (participate in flex layout)

`rect`, `circle`, `ellipse` — regular flex boxes with `width`/`height` (or `size` shorthand for circle).

| Property | Type | Description |
|---|---|---|
| `fill` | string or object | Solid color (`"#4A90D9"`) or gradient |
| `stroke` | string | Stroke color |
| `stroke-width` | number | Stroke width in px |
| `opacity` | number | 0..1 |
| `radius` | unit | Corner radius (rect only) |

Gradient object form:

```yaml
fill:
  gradient: linear        # linear | radial
  colors: ["#f00", "#00f"]
  angle: 45               # linear only, degrees
```

### `draw` — free-form drawing

A flex box in the flow; inside, a `shapes` list in absolute coordinates relative to the element box:

```yaml
- type: draw
  width: 400
  height: 200
  shapes:
    - line: {x1: 0, y1: 100, x2: 400, y2: 50, stroke: "#333", stroke-width: 2}
    - polyline: {points: [[0, 10], [50, 40], [100, 20]], stroke: "#4A90D9"}
    - rect: {x: 10, y: 10, width: 80, height: 40, fill: "#eee", radius: 4}
    - circle: {cx: 200, cy: 75, r: 30, fill: "#e74c3c"}
    - path: {d: "M 0 0 L 100 50 Q 150 0 200 50 Z", fill: "#2ecc71"}
```

- `path.d` supports commands `M`, `L`, `Q`, `C`, `Z` (absolute coordinates only). Parsed with a hand-written tokenizer — no regex backtracking, AOT-safe.
- Shapes draw in list order (painter's algorithm); overlap is allowed.
- New resource limit: `ResourceLimits.MaxShapesPerDraw` (default 1000). Existing `MaxNestingDepth` applies to element tree as usual.

## Layer 2: Chart Element

```yaml
- type: chart
  chart-type: bar         # see list below
  width: 600
  height: 300
  categories: [Q1, Q2, Q3, Q4]
  series:
    - label: "2024"
      data: "{{ sales }}"   # array from data context, like table rows
    - label: "2025"
      data: [12, 30, 22, 48]
  palette: ocean          # optional, default palette otherwise
  legend: bottom          # top | bottom | left | right | none (default: bottom when >1 series, else none)
  title: "Revenue"        # optional
```

The library computes everything visual: axis ranges, nice tick values, grid lines, labels, legend layout, padding, bar widths, point radii. The agent supplies only data and (optionally) one theme/palette word.

### Chart types and phasing

| Phase | Types |
|---|---|
| Charts base | `bar` (vertical + `horizontal: true`), `line`, `area`, `pie`, `donut` |
| Charts extension | `scatter`, `bubble`, `gauge`, `progress`, `sparkline` |
| Charts advanced | `heatmap`, `radar` |

Type-specific properties (registered per type in `KnownProperties.cs`):

- `bar`: `horizontal`, `stacked`
- `line`/`area`/`sparkline`: `smooth` (bool), `points` (bool, show markers)
- `pie`/`donut`: `labels` (percent | value | none)
- `bubble`: third value in data tuples for radius
- `gauge`/`progress`: `value`, `max`, `label`
- `heatmap`: 2D `data`, `x-labels`, `y-labels`
- `radar`: `categories` as spokes

### Data binding

`series[].data` accepts either an inline YAML array or a template expression resolving to an array from the data context — same `TemplateProcessor` path used by `table` and `each`. Numbers, or `[x, y]` / `[x, y, r]` tuples for scatter/bubble.

## Themes and Palettes

### Themes

Named presets controlling fonts, grid color, background, bar corner radius, line widths, label sizes: `light` (default), `dark`, `minimal`.

Set at template level with per-element override:

```yaml
canvas:
  width: 800
  theme: dark        # template-wide
elements:
  - type: chart
    theme: light     # override per chart
```

### Palettes

Named series-color ramps: `default`, `ocean`, `sunset`, `forest`, `mono`, `vivid`. Also accepts an explicit color list:

```yaml
palette: ["#264653", "#2a9d8f", "#e9c46a"]
```

With nothing specified, `light` theme + `default` palette produce a polished result — zero decisions required from the agent.

Themes and palettes live in `FlexRender.Core/Charts/` as static readonly data (AOT-safe, no config files).

## Error Handling

- Unknown property / typo → existing validation with suggestions (`chart-typ` → "did you mean chart-type?"). All new properties registered in `KnownProperties.cs`.
- Empty or missing series data → render a "no data" placeholder inside the chart box, not an exception. The agent sees the image and understands.
- Non-numeric data values → template error with element path, consistent with existing table errors.
- Malformed `path.d` → parse error naming the offending command and position.
- Shape count over `MaxShapesPerCanvas` → resource limit error, same family as existing limits. Limits must never be weakened.

## Testing

- **Unit tests**: axis scale math (nice ticks for ranges crossing zero, negative-only, single point, identical values), palette/theme resolution, path tokenizer edge cases, data binding from context.
- **Snapshot tests**: golden images for every chart type × theme, shape primitives with gradients/strokes, draw overlap ordering. Regenerated via `UPDATE_SNAPSHOTS=true`.
- **Validation tests**: typo suggestions for new properties, resource limit enforcement, empty-data placeholder.

## Documentation (every phase)

- `KnownProperties.cs` registration
- `llms.txt` / `llms-full.txt`
- Wiki: `Element-Reference.md`, `Visual-Reference.md`, `Cookbook.md`
- Template skill (`flexrender/skills/template/SKILL.md`)
- Playground JSON schema + autocomplete (`flexrender-template.json`)

## Implementation Phases

1. **Shapes**: `rect`/`circle`/`ellipse` box elements, gradients, `draw` with `line`/`polyline`/`rect`/`circle`/`path`.
2. **Charts base**: `bar`/`line`/`area`/`pie`/`donut`, themes, palettes, axes, legends, "no data" placeholder.
3. **Charts extension**: `scatter`/`bubble`, `gauge`/`progress`/`sparkline`.
4. **Charts advanced**: `heatmap`/`radar`.
5. **FlexRender.Xml**: alternative XML template parser via existing `ITemplateParser` abstraction (LLMs write XML attributes more reliably than YAML indentation). Same AST, same elements; parser-level work only.

Each phase ships independently with full tests and docs.

## Out of Scope

- SVG render backend for charts (Skia only initially; `FlexRender.Svg` backend support can follow later if needed).
- Animation, interactivity (static images by design).
- Arbitrary per-element style overrides beyond theme/palette (axis font sizes, custom grid styles) — themes are the styling surface; escape hatch is the `draw` element.
- MCP server (agents use YAML + existing CLI; revisit after element work lands).

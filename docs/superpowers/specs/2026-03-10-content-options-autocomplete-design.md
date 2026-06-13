# Content Options Autocomplete

Context-aware YAML autocomplete for `options:` block inside `type: content` elements, based on the `format:` value.

## Scope

Changes limited to two files:
- `src/FlexRender.Playground/wwwroot/schemas/flexrender-template.json` — add option definitions per format
- `src/FlexRender.Playground/wwwroot/yaml-autocomplete.mjs` — detect options/charsets context, suggest properties from schema

## Schema Changes

Add definitions to `flexrender-template.json`:

### `ndcOptions`
| Property | Type | Enum/Description |
|---|---|---|
| `input_encoding` | string | `latin1`, `iso-8859-1`, `utf-8`, `utf8`, `ascii` |
| `columns` | integer | Max characters per line (default 40) |
| `font_family` | string | Font family for all text |
| `char_width_ratio` | number | Character width as fraction of font size (default 0.6) |
| `charsets` | object | Map of charset designator to `charsetStyle` |

### `charsetStyle`
| Property | Type | Enum/Description |
|---|---|---|
| `font` | string | Font registration name |
| `font_family` | string | Explicit font family |
| `font_style` | string | `bold`, `italic`, `bold-italic` |
| `font_size` | integer | Font size in pixels |
| `color` | string | Text color (hex) |
| `encoding` | string | Character encoding |
| `uppercase` | boolean | Convert to uppercase |

### `markdownOptions` / `htmlOptions`
Empty object definitions — no options supported.

### Schema wiring
`contentElement.properties.options` gains `oneOf` referencing `ndcOptions`, `markdownOptions`, `htmlOptions`. The autocomplete code uses the `format` value to pick the right definition at runtime.

## Autocomplete Changes

### Context detection (`detectContext`)
Extend to recognize:
- **`content-options`** — cursor inside `options:` block of a content element. Carry the `format` value.
- **`content-charset-item`** — cursor inside a charset entry under `charsets:` (two levels deep inside options).

### Property suggestions
- In `content-options` context: look up `{format}Options` definition from schema, suggest its properties.
- In `content-charset-item` context: suggest `charsetStyle` properties from schema.
- After colon: suggest enum values where defined (`input_encoding`, `font_style`), booleans for `uppercase`.

### Value suggestions
- `format:` already suggests `ndc`, `markdown`, `html` from existing enum — no change needed.
- `input_encoding:` → enum values from schema
- `font_style:` → enum values from schema
- `uppercase:` → `true` / `false`

## Out of Scope
- No changes to C# code, parsers, or KnownProperties
- No changes to markdown/html parsers (they have no options)
- No validation/warning when wrong options are used for a format

# Playground Improvements Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Three Playground improvements: (1) context-aware autocomplete for content `options:` block, (2) enable QR code rendering, (3) surface render errors for missing images/resources.

**Architecture:** Extend JSON schema with NDC option definitions and wire autocomplete to use them. Add QR module reference and `.WithQr()` call. After render, check `GetLastError()` and display the message in the errors pane.

**Tech Stack:** JSON Schema, JavaScript (ES modules), Monaco Editor API, C# (.NET 10)

---

## File Structure

- **Modify:** `src/FlexRender.Playground/wwwroot/schemas/flexrender-template.json` — add `ndcOptions` and `charsetStyle` definitions
- **Modify:** `src/FlexRender.Playground/wwwroot/yaml-autocomplete.mjs` — extend context detection and suggestions for options/charsets
- **Modify:** `src/FlexRender.Playground/FlexRender.Playground.csproj` — add QR module reference
- **Modify:** `src/FlexRender.Playground/PlaygroundApi.cs` — add `.WithQr()` to builder
- **Modify:** `src/FlexRender.Playground/wwwroot/main.js` — check `GetLastError()` after empty render result

---

## Chunk 1: Schema Definitions

### Task 1: Add ndcOptions and charsetStyle definitions to JSON schema

**Files:**
- Modify: `src/FlexRender.Playground/wwwroot/schemas/flexrender-template.json`

- [ ] **Step 1: Add `charsetStyle` definition**

Add before the closing `}` of `"definitions"` (before line 900):

```json
"charsetStyle": {
  "description": "Per-charset style configuration for NDC content parser.",
  "type": "object",
  "properties": {
    "font": {
      "description": "Font registration name (e.g., \"bold\", \"default\").",
      "type": "string"
    },
    "font_family": {
      "description": "Explicit font family for this charset.",
      "type": "string"
    },
    "font_style": {
      "description": "Font style.",
      "type": "string",
      "enum": ["bold", "italic", "bold-italic"]
    },
    "font_size": {
      "description": "Explicit font size in pixels.",
      "type": "integer",
      "minimum": 1
    },
    "color": {
      "description": "Text color (hex format, e.g., \"#333\").",
      "type": "string"
    },
    "encoding": {
      "description": "Character encoding (e.g., \"none\", \"qwerty-jcuken\").",
      "type": "string"
    },
    "uppercase": {
      "description": "Convert text to uppercase.",
      "type": "boolean",
      "default": false
    }
  },
  "additionalProperties": false
}
```

- [ ] **Step 2: Add `ndcOptions` definition**

Add after `charsetStyle`:

```json
"ndcOptions": {
  "description": "Options for the NDC content parser.",
  "type": "object",
  "properties": {
    "input_encoding": {
      "description": "Byte encoding for binary data.",
      "type": "string",
      "enum": ["latin1", "iso-8859-1", "utf-8", "utf8", "ascii"],
      "default": "latin1"
    },
    "columns": {
      "description": "Max characters per line (receipt width).",
      "type": "integer",
      "minimum": 1,
      "default": 40
    },
    "font_family": {
      "description": "Font family for all text (e.g., \"JetBrains Mono\").",
      "type": "string"
    },
    "char_width_ratio": {
      "description": "Character width as fraction of font size for monospace fonts.",
      "type": "number",
      "minimum": 0.1,
      "default": 0.6
    },
    "charsets": {
      "description": "Per-charset style mappings keyed by designator character.",
      "type": "object",
      "additionalProperties": {
        "$ref": "#/definitions/charsetStyle"
      }
    }
  },
  "additionalProperties": false
}
```

- [ ] **Step 3: Update `contentElement.properties.options` to reference `ndcOptions`**

Replace the current `options` property in `contentElement` (line 894-897):

```json
"options": {
  "description": "Format-specific rendering options. Properties depend on the chosen format.",
  "oneOf": [
    { "$ref": "#/definitions/ndcOptions" },
    { "type": "object", "description": "No options for this format.", "properties": {}, "additionalProperties": false }
  ]
}
```

- [ ] **Step 4: Verify schema is valid JSON**

Run: `python3 -c "import json; json.load(open('src/FlexRender.Playground/wwwroot/schemas/flexrender-template.json'))"`
Expected: no output (valid JSON)

- [ ] **Step 5: Commit**

```bash
git add src/FlexRender.Playground/wwwroot/schemas/flexrender-template.json
git commit -m "feat: add ndcOptions and charsetStyle definitions to JSON schema"
```

---

## Chunk 2: Autocomplete Context Detection

### Task 2: Extend detectContext to recognize options and charset contexts

**Files:**
- Modify: `src/FlexRender.Playground/wwwroot/yaml-autocomplete.mjs`

The autocomplete must handle these YAML structures:

```yaml
- type: content
  format: ndc
  options:           # <-- context: content-options, format: ndc
    input_encoding: latin1
    columns: 44
    charsets:        # <-- still content-options (charsets is a key here)
      I:             # <-- charset designator key, not suggested
        font_style:  # <-- context: content-charset-item
        color: "#333"
```

- [ ] **Step 1: Extend `detectContext` to return `content-options` context**

Add detection for `options:` inside a content element. When walking backwards, if we encounter `options:` at a parent indent, continue walking to find `type: content` and `format:` value. Return `{ type: 'content-options', format: 'ndc' }`.

In `detectContext`, add this check after the `if (trimmed === 'canvas:')` block (around line 150):

```javascript
if (trimmed === 'options:') {
    // Walk further back to find the parent content element and its format
    const parentInfo = findContentParent(lines, i, lineIndent);
    if (parentInfo) {
        return { type: 'content-options', format: parentInfo.format };
    }
}
```

- [ ] **Step 2: Detect `content-charset-item` context**

When inside a charset entry (two indentation levels below `charsets:`), detect that we're editing charset style properties. Add to `detectContext` right after the `options:` check:

```javascript
// Check if we're inside a charsets > designator block
if (lineIndent < currentIndent) {
    // Look for pattern: grandparent is "charsets:", parent is a designator key
    const isDesignatorKey = trimmed.match(/^\w+:$/);
    if (isDesignatorKey) {
        // Check if grandparent is "charsets:" inside content options
        for (let k = i - 1; k >= 0; k--) {
            const prev = lines[k];
            const prevTrimmed = prev.trim();
            if (!prevTrimmed || prevTrimmed.startsWith('#')) continue;
            const prevIndent = prev.match(/^(\s*)/)[1].length;
            if (prevIndent < lineIndent) {
                if (prevTrimmed === 'charsets:') {
                    return { type: 'content-charset-item' };
                }
                break;
            }
        }
    }
}
```

- [ ] **Step 3: Add `findContentParent` helper function**

Add after `detectContext`:

```javascript
function findContentParent(lines, fromIndex, optionsIndent) {
    let format = null;
    for (let i = fromIndex - 1; i >= 0; i--) {
        const line = lines[i];
        const trimmed = line.trim();
        if (!trimmed || trimmed.startsWith('#')) continue;
        const indent = line.match(/^(\s*)/)[1].length;
        if (indent >= optionsIndent) {
            // Sibling or child of options — check for format:
            const formatMatch = trimmed.match(/^format:\s*(\w+)/);
            if (formatMatch) format = formatMatch[1];
            continue;
        }
        // Parent level — should be the element with type: content
        const typeMatch = trimmed.match(/^-?\s*type:\s*(\w+)/);
        if (typeMatch) {
            if (typeMatch[1] === 'content') return { format };
            return null; // Not a content element
        }
        // If we hit a sibling property at element level, keep scanning for type
        if (indent < optionsIndent) {
            const formatMatch = trimmed.match(/^-?\s*format:\s*(\w+)/);
            if (formatMatch) format = formatMatch[1];
        }
        if (indent === 0) break; // Reached root
    }
    return null;
}
```

- [ ] **Step 4: Commit**

```bash
git add src/FlexRender.Playground/wwwroot/yaml-autocomplete.mjs
git commit -m "feat: detect content-options and charset-item contexts in autocomplete"
```

---

## Chunk 3: Wire Suggestions to Schema Definitions

### Task 3: Suggest properties and values for content options

**Files:**
- Modify: `src/FlexRender.Playground/wwwroot/yaml-autocomplete.mjs`

- [ ] **Step 1: Handle `content-options` and `content-charset-item` in the completion provider switch**

In the `provideCompletionItems` switch block (after `case 'template':`, around line 129), add:

```javascript
case 'content-options': {
    const formatDef = context.format
        ? defs[context.format + 'Options']
        : null;
    const props = formatDef?.properties || {};
    return makeSuggestions(monaco, props, range, 'content-options');
}
case 'content-charset-item': {
    const props = defs.charsetStyle?.properties || {};
    return makeSuggestions(monaco, props, range, 'content-charset-item');
}
```

- [ ] **Step 2: Update `findPropertyDef` to resolve options properties**

In `findPropertyDef` (around line 295), add context detection for options and charset blocks. Before the flex item fallback (line 316), add:

```javascript
// Check if inside content options or charset item
const optionsContext = detectContentOptionsContext(lines);
if (optionsContext === 'charset-item') {
    const charsetDef = defs.charsetStyle;
    if (charsetDef?.properties?.[cleanKey]) return charsetDef.properties[cleanKey];
} else if (optionsContext) {
    // optionsContext is the format name
    const optDef = defs[optionsContext + 'Options'];
    if (optDef?.properties?.[cleanKey]) return optDef.properties[cleanKey];
}
```

- [ ] **Step 3: Add `detectContentOptionsContext` helper**

Add after `findPropertyDef`:

```javascript
function detectContentOptionsContext(lines) {
    let inOptions = false;
    let optionsIndent = -1;
    let charsetsIndent = -1;

    for (let i = lines.length - 2; i >= 0; i--) {
        const line = lines[i];
        const trimmed = line.trim();
        if (!trimmed || trimmed.startsWith('#')) continue;
        const indent = line.match(/^(\s*)/)[1].length;

        // Check if we're inside a charset designator block
        if (charsetsIndent < 0 && trimmed.match(/^\w+:$/) && !inOptions) {
            // Possible designator — check parent
            for (let k = i - 1; k >= 0; k--) {
                const prev = lines[k];
                const prevTrimmed = prev.trim();
                if (!prevTrimmed || prevTrimmed.startsWith('#')) continue;
                const prevIndent = prev.match(/^(\s*)/)[1].length;
                if (prevIndent < indent && prevTrimmed === 'charsets:') {
                    return 'charset-item';
                }
                if (prevIndent < indent) break;
            }
        }

        if (trimmed === 'options:') {
            // Find format from sibling properties
            const parentInfo = findContentParent(lines, i, indent);
            return parentInfo?.format || null;
        }

        if (indent === 0) break;
    }
    return null;
}
```

- [ ] **Step 4: Update `suggestValues` to resolve enum values for options properties**

The existing logic in `suggestValues` already calls `findPropertyDef` which will now return the correct property definition with enum values (e.g., `input_encoding` enum, `font_style` enum, `uppercase` boolean). No changes needed — just verify it works.

- [ ] **Step 5: Add sort order for content-options context**

In `getSortOrder` (line 363), add entries for the new contexts:

```javascript
'content-options': { input_encoding: '0', columns: '1', font_family: '2', char_width_ratio: '3', charsets: '4' },
'content-charset-item': { font: '0', font_family: '1', font_style: '2', font_size: '3', color: '4', encoding: '5', uppercase: '6' },
```

- [ ] **Step 6: Commit**

```bash
git add src/FlexRender.Playground/wwwroot/yaml-autocomplete.mjs
git commit -m "feat: wire content options and charset suggestions to schema definitions"
```

---

## Chunk 4: Manual Verification

### Task 4: Verify in browser

- [ ] **Step 1: Start dev server and test**

Run: `dotnet run --project src/FlexRender.Playground`

Open browser, create a template with:

```yaml
layout:
  - type: content
    source: "test.ndc"
    format: ndc
    options:
      |  <-- trigger autocomplete here, expect: input_encoding, columns, font_family, char_width_ratio, charsets
```

Verify:
1. Inside `options:` with `format: ndc` → suggests NDC options
2. After `input_encoding:` → suggests `latin1`, `iso-8859-1`, `utf-8`, `utf8`, `ascii`
3. Inside `charsets: > X:` → suggests charset style properties (`font`, `font_style`, etc.)
4. After `font_style:` → suggests `bold`, `italic`, `bold-italic`
5. After `uppercase:` → suggests `true`, `false`
6. Hovering over option keys shows descriptions
7. With `format: markdown` → `options:` suggests nothing (empty properties)
8. Without `format:` → `options:` suggests nothing

- [ ] **Step 2: Verify publish output**

Run: `dotnet publish src/FlexRender.Playground -c Release -o /tmp/playground-verify`
Expected: builds without errors, schema included in output

- [ ] **Step 3: Final commit if any fixes needed**

---

## Chunk 5: Enable QR Code Rendering

### Task 5: Add QR module to Playground

**Files:**
- Modify: `src/FlexRender.Playground/FlexRender.Playground.csproj`
- Modify: `src/FlexRender.Playground/PlaygroundApi.cs`

- [ ] **Step 1: Add QR project reference to Playground csproj**

In `FlexRender.Playground.csproj`, add to the `<ItemGroup>` with other ProjectReferences:

```xml
<ProjectReference Include="..\FlexRender.QrCode.Skia.Render\FlexRender.QrCode.Skia.Render.csproj" />
```

Note: Reference `FlexRender.QrCode.Skia.Render` directly (not the meta-package `FlexRender.QrCode` which also pulls in ImageSharp and Svg renderers that aren't needed in WASM).

- [ ] **Step 2: Add `using` and `.WithQr()` call in PlaygroundApi.cs**

Add using directive at top of `PlaygroundApi.cs`:

```csharp
using FlexRender.QrCode;
```

Change the builder initialization (line 43-45) from:

```csharp
var builder = new FlexRenderBuilder()
    .WithNdc()
    .WithSkia();
```

To:

```csharp
var builder = new FlexRenderBuilder()
    .WithNdc()
    .WithSkia(skia => skia.WithQr());
```

- [ ] **Step 3: Verify it builds**

Run: `dotnet build src/FlexRender.Playground/FlexRender.Playground.csproj`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add src/FlexRender.Playground/FlexRender.Playground.csproj src/FlexRender.Playground/PlaygroundApi.cs
git commit -m "feat: enable QR code rendering in Playground"
```

---

## Chunk 6: Surface Render Errors for Missing Images

### Task 6: Show GetLastError() in the errors pane when render returns empty

**Files:**
- Modify: `src/FlexRender.Playground/wwwroot/main.js`

Currently when `RenderToPng` catches an exception, it stores the error in `_lastError` and returns an empty byte array. The JS side shows "Render returned empty — check console" but never calls `GetLastError()`.

- [ ] **Step 1: After empty render result, check GetLastError()**

In `main.js`, find the block that handles empty render result (around line 1137-1139):

```javascript
} else {
    statusText.textContent = 'Render returned empty \u2014 check console';
}
```

Replace with:

```javascript
} else {
    const lastError = api.GetLastError();
    if (lastError) {
        errorsPane.textContent = lastError;
        statusBar.classList.add('error');
        statusText.textContent = 'Error';
        switchToTab('errors');
    } else {
        statusText.textContent = 'Render returned empty';
    }
}
```

- [ ] **Step 2: Verify locally**

Run: `dotnet run --project src/FlexRender.Playground`

Create a template referencing a non-existent image:

```yaml
canvas:
  width: 200
  height: 200
layout:
  - type: image
    src: "nonexistent.png"
    width: 100
    height: 100
```

Expected: Error message appears in the Errors tab instead of silent empty render.

- [ ] **Step 3: Commit**

```bash
git add src/FlexRender.Playground/wwwroot/main.js
git commit -m "feat: surface render errors for missing images in Playground"
```

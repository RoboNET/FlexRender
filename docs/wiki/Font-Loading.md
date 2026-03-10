# Font Loading

This page documents how FlexRender resolves, loads, and caches fonts at render time. Understanding the font pipeline is important when working with custom fonts, WASM deployments, or when debugging missing-glyph issues.

## Overview

`FontManager` is the central class responsible for all font operations in the Skia rendering backend. It manages:

- **Registration** -- mapping logical font names to file paths and optional system-font fallbacks.
- **Loading** -- reading `.ttf`/`.otf` files from disk or from `IResourceLoader` implementations (for WASM, embedded resources, HTTP, etc.).
- **Caching** -- storing loaded `SKTypeface` instances in thread-safe concurrent dictionaries so each font is loaded at most once.
- **Variant resolution** -- finding the best weight/style match (Bold, Italic, SemiBold, etc.) for a registered font family.
- **Disposal** -- deterministic cleanup of all native Skia typeface handles, including orphaned typefaces from re-registration.

### Thread Safety

All caches use `ConcurrentDictionary` with `GetOrAdd` for atomic, lock-free access. `FontManager` is safe to use from multiple threads and multiple render calls concurrently. The `Dispose()` method should only be called once, after all rendering is complete.

## Font Resolution Priority

When a template element requests a font, resolution follows a strict priority order depending on the lookup method.

### By Registered Name (`GetTypeface(fontName)`)

```
1. Registered file path --> File on disk --> SKTypeface.FromFile() --> Cache
2. Registered file path --> Resource loaders (WASM) --> SKTypeface.FromStream() --> Cache
3. Registered fallback name --> System font lookup (SKTypeface.FromFamilyName)
4. Default fallback ("Arial") --> SKTypeface.FromFamilyName
5. SKTypeface.Default (built-in blank typeface)
6. [WASM only] Any previously file-loaded typeface as last resort
```

### By Family Name (`GetTypefaceByFamily(familyName, weight, style)`)

```
1. Scan registered file-loaded fonts by FamilyName metadata
   a. Exact family match + Normal weight/style --> return immediately
   b. Exact family match + variant --> delegate to variant resolution
2. System font manager (SKFontManager.Default.MatchFamily) [desktop only]
   - Only accepted if FamilyName matches AND weight is within 100 units
3. Best registered match (even if weight is off)
4. Fallback to "main" font
```

### Variant Resolution (`GetTypeface(fontName, weight, style)`)

When a non-default weight or style is requested:

```
1. Fast path: Normal weight + Normal style --> delegates to base GetTypeface(fontName)
2. Search file-loaded typefaces with matching FamilyName + weight (within 100 units) + slant
3. System font manager (MatchFamily with SKFontStyle) [desktop only]
4. Sibling file scan: enumerate .ttf/.otf files in the same directory [desktop only]
   - Match by FamilyName, weight (within 100 units), and slant
   - Dispose rejected candidates immediately to prevent leaks
5. Fall back to the base typeface (Regular weight)
```

### Four-Parameter Overload (`GetTypeface(fontName, fontFamily, weight, style)`)

This is the primary entry point used by the rendering engine:

```
1. If fontName is NOT "main" and NOT empty --> resolve by registered name + variant
2. Else if fontFamily is NOT empty --> resolve by family name
3. Else --> resolve "main" font by registered name + variant
```

## Registration

### Template-Based Registration

The `TemplatePreprocessor.RegisterFontsAsync` method processes the `fonts:` section of a YAML template:

```yaml
fonts:
  default: "assets/fonts/Inter-Regular.ttf"
  bold: "assets/fonts/Inter-Bold.ttf"
  mono: "assets/fonts/JetBrainsMono-Regular.ttf"
```

For each font entry:

1. The path is resolved against `FlexRenderOptions.BasePath` (if set) or the current directory.
2. `RegisterFont(name, resolvedPath, fallback)` is called.
3. If the file does NOT exist on disk, `PreloadFontFromResourcesAsync` is called to try resource loaders.
4. If the resolved path fails with resource loaders, the original (unresolved) path is tried as a fallback.

The special font name `"default"` is automatically registered as both `"default"` and `"main"`, making it the fallback for all text elements without an explicit `font:` property.

### Programmatic Registration

```csharp
fontManager.RegisterFont("heading", "/fonts/Inter-Bold.ttf", fallback: "Arial");
```

Parameters:
- **name** -- logical name used in templates (`font: heading`).
- **path** -- absolute or relative path to the `.ttf`/`.otf` file.
- **fallback** -- optional system font family name used when the file is missing.

Returns `true` if the file exists on disk at registration time; `false` otherwise (the font may still load later via resource loaders).

### Pre-loading from Resource Loaders

```csharp
await fontManager.PreloadFontFromResourcesAsync("my-font", "fonts/MyFont.ttf");
```

Iterates resource loaders in priority order. The first loader that returns a valid stream wins. The loaded typeface is cached directly, bypassing the lazy file-load path. This is the recommended approach for WASM where the file system is unavailable.

## Re-Registration and Deferred Disposal

Calling `RegisterFont` with the same name a second time:

1. Updates the file path mapping.
2. Removes the old typeface from the base cache (`_typefaces`).
3. Clears ALL entries from the variant cache (`_variantTypefaces`) because variants may reference the old typeface.
4. Adds the removed typeface to an **orphaned typefaces** bag.

Orphaned typefaces cannot be disposed immediately because they may still be referenced by variant cache entries at the moment of removal (race condition window with concurrent reads). Instead, they are collected in a `ConcurrentBag` and disposed during `FontManager.Dispose()`.

The `Dispose()` method uses a `HashSet<SKTypeface>` with `ReferenceEqualityComparer` to deduplicate typefaces that appear in multiple caches (e.g., a base typeface that is also returned as its own Normal-weight variant). Each native handle is disposed exactly once.

## WASM Constraints

When `OperatingSystem.IsBrowser()` returns `true`, several code paths are disabled:

| Feature | Desktop | WASM |
|---------|---------|------|
| System font lookup (`SKTypeface.FromFamilyName`) | Yes | **No** -- returns objects with invalid native handles |
| Sibling file scan (`Directory.EnumerateFiles`) | Yes | **No** -- no local file system |
| `SKFontManager.Default.MatchFamily` | Yes | **No** -- same invalid handle issue |
| `SKTypeface.Default` | Reliable | **May have invalid handle** |
| `FamilyName`/`IsFixedPitch` on system typefaces | Safe | **Crashes with RuntimeError** |

### File-Loaded Tracking

The `_fileLoadedTypefaces` dictionary tracks which fonts were loaded from real files or resource loaders. Only these typefaces are safe to inspect for native properties (`FamilyName`, `IsFixedPitch`, `FontStyle`). The `IsFileLoaded(name)` and `GetTypefaceInfo(name)` methods use this tracking to prevent WASM crashes.

### WASM Fallback Chain

When all resolution paths fail in WASM:

1. Try to return any previously file-loaded typeface (`GetAnyFileLoadedTypeface()`).
2. Fall back to `SKTypeface.Default` (may be blank/broken but avoids null).

For WASM deployments, **always** pre-load fonts via resource loaders before rendering. Without pre-loaded fonts, text will render with the built-in blank typeface or fail silently.

## Font Size Parsing

`FontManager.ParseFontSize` handles CSS-like size strings:

| Format | Example | Resolution |
|--------|---------|------------|
| Bare number | `"16"` | 16 pixels |
| `px` suffix | `"48px"` | 48 pixels |
| `em` suffix | `"1.5em"` | 1.5 x base font size |
| `%` suffix | `"50%"` | 50% of parent size (or base size when equal) |
| Invalid/empty | `""`, `"abc"` | Returns base font size |

## Diagnostic API

| Method | Returns | Purpose |
|--------|---------|---------|
| `IsFileLoaded(name)` | `bool` | Whether the font was loaded from a real file/resource (safe to inspect in WASM) |
| `GetTypefaceInfo(name)` | `(FamilyName, IsFixedPitch)?` | Font metadata; `null` if not file-loaded |
| `RegisteredFontPaths` | `IReadOnlyDictionary<string, string>` | Snapshot of all registered name-to-path mappings |

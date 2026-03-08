# ExprValue BytesValue Unification Design

## Problem

BytesValue resolution is duplicated and inconsistent:

1. **ContentSourceResolver.TryResolveBytes()** -- resolves `{{variable}}` -> BytesValue -> BinaryContent for content elements
2. **TemplateExpander.TryResolveBytesAsDataUri()** -- resolves `{{variable}}` -> BytesValue -> base64 data: URI string for image elements

The image path has unnecessary base64 encode/decode overhead (BytesValue -> base64 string -> Base64ResourceLoader -> decode -> Stream -> SKBitmap). Content path bypasses loaders entirely. Neither approach is available to SVG elements.

## Design

Add `BytesValue? Bytes` property to `ExprValue<T>` so binary data from template variables flows directly through the pipeline without base64 round-trip.

### Changes

**ExprValue\<T\>** -- new property:
```csharp
public BytesValue? Bytes { get; private init; }
```

**TemplateExpander** -- unified resolution method replaces both `TryResolveBytesAsDataUri` and per-element BytesValue logic:
- When resolving `{{variable}}` for any `ExprValue<string>` (image.Src, svg.Src, content.Source), check if the variable resolves to BytesValue
- If yes, store BytesValue directly in `ExprValue.Bytes`
- Delete `TryResolveBytesAsDataUri()` method

**ContentSourceResolver** -- simplified:
- Delete `TryResolveBytes()` method
- Check `source.Bytes` property instead: if non-null, return `BinaryContent` directly

**ImageLoader** -- BytesValue-aware:
- Accept optional `BytesValue?` parameter (or check before calling loaders)
- If bytes available, decode directly via `SKBitmap.Decode(bytes.AsStream())`
- Skip IResourceLoader chain entirely

**SvgElement** -- gets BytesValue support for free (same ExprValue<string> Src property).

### What stays the same

- `IResourceLoader` interface -- unchanged, remains URI-based
- Loader chain for URI-based sources (file:, data:, http://, embedded://)
- `text:` prefix handling in ContentSourceResolver

### Benefits

- Zero unnecessary allocations -- bytes flow directly
- Single mechanism for image, svg, content
- IResourceLoader stays clean URI-based API
- Two duplicate methods removed

# Provider Restructuring Design

**Date:** 2026-02-08
**Status:** Draft

## Problem

Provider interfaces (`IContentProvider<T>`, `ISvgContentProvider<T>`) live in `FlexRender.Skia` and return `SKBitmap`. This forces every element provider project to depend on SkiaSharp, even when the rendering logic has nothing to do with Skia. Consequences:

1. **Hidden coupling.** `FlexRender.Svg` (SVG output renderer) depends on `FlexRender.Skia` solely to access `IContentProvider<T>` and `SKBitmap`-to-base64 conversion. This makes the dependency graph misleading.
2. **Code duplication.** The ImageSharp backend duplicates Code128 encoding tables (~95 entries), QR data capacity maps, ECC-level mapping, and barcode checksum logic because it cannot reference the Skia-dependent originals.
3. **No shared core.** Adding a new backend (e.g., PDF) would require copying the same encoding tables a third time.

## Goals

- Eliminate code duplication for encoding logic (Code128, QR data generation).
- Make dependencies explicit: each project depends only on what it actually uses.
- Allow `FlexRender.Svg` to operate without `FlexRender.Skia` when only SVG-native providers are configured.
- Keep the builder API ergonomic; avoid forcing users to register six packages where two sufficed.

## Non-Goals

- Changing the AST element types (they remain in `FlexRender.Core`).
- Rewriting the layout engine or rendering engines.
- Supporting new barcode formats (that is a separate effort).

---

## Architecture

### Layer 1: Backend-Neutral Provider Abstractions (in `FlexRender.Core`)

Move the provider interfaces out of `FlexRender.Skia` into `FlexRender.Core`, making them backend-neutral by using `byte[]` instead of `SKBitmap`:

```
namespace FlexRender.Providers;

// Raster content: returns raw RGBA pixel data or PNG bytes
public interface IContentProvider<in TElement>
{
    ContentResult Generate(TElement element, int width, int height);
}

public readonly record struct ContentResult(byte[] PngBytes, int Width, int Height);

// SVG-native content: returns SVG markup string (unchanged)
public interface ISvgContentProvider<in TElement>
{
    string GenerateSvgContent(TElement element, float width, float height);
}
```

**Rationale:** `ContentResult` wraps PNG-encoded bytes. This is the simplest cross-backend contract. Every backend can decode PNG, and encoding to PNG is a one-liner in both SkiaSharp and ImageSharp. The width/height metadata allows backends to position the image without decoding.

**Alternative considered:** Returning `Stream` or `ReadOnlyMemory<byte>`. Rejected because `byte[]` is simpler, AOT-safe, and PNG encoding is already happening in the existing code (see `DrawBitmapElement` in `SvgRenderingEngine` line 507).

### Layer 2: Encoding Core Projects (Pure .NET, No Rendering)

**`FlexRender.QrCode.Core`** -- depends on `FlexRender.Core` + `QRCoder` only.

Contains:
- `QrEncoder` -- wraps `QRCodeGenerator`, returns a `bool[][]` module matrix plus module count.
- `QrDataValidator` -- `MaxDataCapacity` dictionary, `ValidateDataCapacity()`, `MapEccLevel()`.
- Shared SVG path builder (`QrSvgPathBuilder`) used by both the SVG-native provider and a future PDF backend.

**`FlexRender.Barcode.Core`** -- depends on `FlexRender.Core` only (no external NuGet packages).

Contains:
- `Code128Encoder` -- the Code128B pattern table, start/stop patterns, checksum calculation. Returns `string` (the "10110..." bit pattern).
- `Code128Validator` -- character validation.
- Shared SVG bar builder for SVG-native barcode rendering.

**`FlexRender.SvgElement.Core`** -- depends on `FlexRender.Core` only (no external NuGet packages).

Contains:
- `SvgContentParser` -- SVG content parsing and validation logic shared across backends.

**No Core project for Image** -- image loading/decoding is inherently backend-specific. No shareable encoding logic exists.

### Layer 3: Per-Backend Provider Projects

Each encoding Core gets one project per rendering backend that implements `IContentProvider<T>` and/or `ISvgContentProvider<T>`:

| Project | Depends On | Provides |
|---------|-----------|----------|
| `FlexRender.QrCode.Core` | Core, QRCoder | `QrEncoder`, `QrSvgPathBuilder` |
| `FlexRender.QrCode.Skia` | QrCode.Core, Skia | `IContentProvider<QrElement>` via SKBitmap |
| `FlexRender.QrCode.Svg` | QrCode.Core, Core | `ISvgContentProvider<QrElement>` |
| `FlexRender.QrCode.ImageSharp` | QrCode.Core, ImageSharp(NuGet) | `IContentProvider<QrElement>` via ImageSharp |
| `FlexRender.Barcode.Core` | Core | `Code128Encoder` |
| `FlexRender.Barcode.Skia` | Barcode.Core, Skia | `IContentProvider<BarcodeElement>` |
| `FlexRender.Barcode.Svg` | Barcode.Core, Core | `ISvgContentProvider<BarcodeElement>` |
| `FlexRender.Barcode.ImageSharp` | Barcode.Core, ImageSharp(NuGet) | `IContentProvider<BarcodeElement>` |
| `FlexRender.SvgElement.Core` | Core | SVG content parsing, validation |
| `FlexRender.SvgElement.Skia` | SvgElement.Core, Skia, Svg.Skia | `IContentProvider<SvgElement>` via SKBitmap |
| `FlexRender.SvgElement.Svg` | SvgElement.Core, Core | `ISvgContentProvider<SvgElement>` (embeds SVG directly) |

**Total new projects: 9** (QrCode.Core, QrCode.Svg, QrCode.ImageSharp, Barcode.Core, Barcode.Svg, Barcode.ImageSharp, SvgElement.Core, SvgElement.Svg, + SvgElement.Skia is a rename).
**Renamed projects: 3** (QrCode -> QrCode.Skia, Barcode -> Barcode.Skia, SvgElement -> SvgElement.Skia).

**Note:** `FlexRender.SvgElement.ImageSharp` is NOT supported in this phase. SVG rasterization via ImageSharp would require vendoring ~40 ShimSkiaSharp classes from the Svg library. This can be added in a future phase if demand justifies it.

### What About Image Providers?

`ImageProvider` (Skia) and `ImageSharpImageProvider` stay where they are -- inside `FlexRender.Skia` and `FlexRender.ImageSharp` respectively. They have no shareable encoding logic; image loading/decoding is inherently backend-specific. No `.Core` project needed.

### What About FlexRender.ImageSharp?

`FlexRender.ImageSharp` becomes **thinner**: its inline `ImageSharpQrProvider` and `ImageSharpBarcodeProvider` classes get deleted. Instead, users add `FlexRender.QrCode.ImageSharp` and `FlexRender.Barcode.ImageSharp` as separate packages. The `ImageSharpImageProvider` and the rendering engine stay in `FlexRender.ImageSharp`.

---

## FlexRender.Svg Independence from FlexRender.Skia

The current `FlexRender.Svg` project depends on `FlexRender.Skia` for two reasons:

1. `IContentProvider<T>` / `ISvgContentProvider<T>` interfaces (will move to Core).
2. `DrawBitmapElement` uses `SKBitmap` / `SKImage` / `SKData` to PNG-encode bitmaps for base64 embedding.

After this restructuring:
- Reason 1 is resolved: interfaces move to Core.
- Reason 2 is resolved: `IContentProvider<T>.Generate()` now returns `ContentResult` with PNG bytes. `DrawBitmapElement` becomes a simple base64-encode of those bytes. No SkiaSharp types needed.

**Result:** `FlexRender.Svg` drops its dependency on `FlexRender.Skia`. It depends only on `FlexRender.Core`.

The SVG renderer accepts `IContentProvider<QrElement>?` and `ISvgContentProvider<QrElement>?` through its constructor. When the user configures `FlexRender.QrCode.Svg`, the SVG-native provider is used. When they configure `FlexRender.QrCode.Skia`, the raster provider generates PNG bytes that get base64-embedded. Both paths work without the SVG project knowing which backend provided them.

---

## Builder API Changes

### Current API (unchanged for Skia users)

```csharp
var render = new FlexRenderBuilder()
    .WithSkia(skia => skia
        .WithQr()        // FlexRender.QrCode.Skia
        .WithBarcode()   // FlexRender.Barcode.Skia
        .WithSvgElement()) // FlexRender.SvgElement.Skia
    .Build();
```

The `WithQr()` and `WithBarcode()` extension methods move from the old projects to the new `.Skia` projects. Same method names, same namespace. Binary-breaking but source-compatible for most users (just swap the NuGet package).

### New SVG-specific API

```csharp
var render = new FlexRenderBuilder()
    .WithSvg(svg => svg
        .WithQr()        // FlexRender.QrCode.Svg provides ISvgContentProvider<QrElement>
        .WithBarcode())  // FlexRender.Barcode.Svg provides ISvgContentProvider<BarcodeElement>
    .Build();
```

This gives SVG-native QR codes and barcodes without any SkiaSharp dependency.

### Mixed SVG + Raster API

```csharp
var render = new FlexRenderBuilder()
    .WithSvg(svg => svg
        .WithQr()       // SVG-native QR
        .WithBarcode()  // SVG-native barcode
        .WithSkia(skia => skia
            .WithQr()        // Raster QR for PNG fallback
            .WithBarcode()   // Raster barcode for PNG fallback
            .WithSvgElement()))
    .Build();
```

### ImageSharp API

```csharp
var render = new FlexRenderBuilder()
    .WithImageSharp(is => is
        .WithQr()        // FlexRender.QrCode.ImageSharp
        .WithBarcode())  // FlexRender.Barcode.ImageSharp
    .Build();
```

Same method names as before, but now they come from separate NuGet packages rather than being built into `FlexRender.ImageSharp`.

---

## SvgBuilder Changes

The `SvgBuilder` needs new provider slots for SVG-native content providers:

```csharp
public sealed class SvgBuilder
{
    // Existing
    internal bool IsSkiaEnabled { get; private set; }
    internal Action<SkiaBuilder>? SkiaConfigureAction { get; private set; }

    // New: SVG-native providers
    internal ISvgContentProvider<QrElement>? QrSvgProvider { get; private set; }
    internal ISvgContentProvider<BarcodeElement>? BarcodeSvgProvider { get; private set; }
    internal ISvgContentProvider<SvgElement>? SvgElementSvgProvider { get; private set; }

    // New: raster providers (backend-neutral)
    internal IContentProvider<QrElement>? QrRasterProvider { get; private set; }
    internal IContentProvider<BarcodeElement>? BarcodeRasterProvider { get; private set; }

    internal void SetQrSvgProvider(ISvgContentProvider<QrElement> provider) { ... }
    internal void SetBarcodeSvgProvider(ISvgContentProvider<BarcodeElement> provider) { ... }
    internal void SetSvgElementSvgProvider(ISvgContentProvider<SvgElement> provider) { ... }
}
```

The `SvgBuilderExtensions.WithSvg()` factory method assembles providers with this priority:
1. Use `ISvgContentProvider<T>` if registered (vector-native output).
2. Fall back to `IContentProvider<T>` with PNG-to-base64 embedding.
3. Fall back to extracting providers from the Skia sub-builder (backward compatible).

---

## SkiaBuilder Changes

`SkiaBuilder.SetQrProvider` and `SetBarcodeProvider` change their parameter types from backend-specific to the new Core interfaces:

```csharp
internal void SetQrProvider(IContentProvider<QrElement> provider) { ... }
```

Since the interface signature changes (returns `ContentResult` instead of `SKBitmap`), the Skia-specific provider implementations need a thin adapter or can implement the new interface directly. The Skia providers will internally generate `SKBitmap`, then PNG-encode it to produce `ContentResult`.

**Alternative considered:** Keep `IContentProvider<T>` returning `SKBitmap` and add a separate `IBitmaplessContentProvider<T>` in Core. Rejected because it creates two parallel hierarchies and the Skia rendering engine would need to check both interfaces.

---

## Migration Path

### Phase 1: Move interfaces to Core (breaking change, do first)

1. Move `IContentProvider<T>`, `ISvgContentProvider<T>`, and `IResourceLoaderAware` from `FlexRender.Skia` to `FlexRender.Core`.
2. Change `IContentProvider<T>.Generate()` signature to return `ContentResult` instead of `SKBitmap`.
3. Update `FlexRender.Skia`'s `RenderingEngine` to decode `ContentResult.PngBytes` back to `SKBitmap` for canvas drawing.
4. Update `FlexRender.Svg`'s `SvgRenderingEngine` to base64-encode `ContentResult.PngBytes` directly (removing `SKImage`/`SKData` usage).
5. Remove `FlexRender.Svg`'s dependency on `FlexRender.Skia`.
6. Update all existing providers to implement the new interface.

### Phase 2: Extract encoding Core projects

7. Create `FlexRender.QrCode.Core` with `QrEncoder`, `QrDataValidator`, `QrSvgPathBuilder`.
8. Create `FlexRender.Barcode.Core` with `Code128Encoder`, `Code128Validator`.
9. Create `FlexRender.SvgElement.Core` with `SvgContentParser` (SVG content parsing/validation).
10. Update existing QR/Barcode/SvgElement providers to use the Core classes.
11. Delete duplicated code from `FlexRender.ImageSharp`.

### Phase 3: Create per-backend projects

12. Rename `FlexRender.QrCode` to `FlexRender.QrCode.Skia`.
13. Rename `FlexRender.Barcode` to `FlexRender.Barcode.Skia`.
14. Rename `FlexRender.SvgElement` to `FlexRender.SvgElement.Skia`.
15. Create `FlexRender.QrCode.Svg` with `QrSvgProvider : ISvgContentProvider<QrElement>`.
16. Create `FlexRender.Barcode.Svg` with `BarcodeSvgProvider : ISvgContentProvider<BarcodeElement>`.
17. Create `FlexRender.SvgElement.Svg` with `SvgSvgElementProvider : ISvgContentProvider<SvgElement>`.
18. Create `FlexRender.QrCode.ImageSharp` (extract from `FlexRender.ImageSharp`).
19. Create `FlexRender.Barcode.ImageSharp` (extract from `FlexRender.ImageSharp`).

**Note:** `SvgSvgElementProvider` is the simplest provider in the entire system -- it is essentially a pass-through that takes the SVG content from the element and returns it directly for embedding in SVG output. This is the most natural way to include SVG elements in SVG output, requiring no rasterization.

### Phase 4: Update builders and wiring

20. Update `SvgBuilder` with SVG-native provider slots (including `ISvgContentProvider<SvgElement>?`).
21. Add `WithQr()`/`WithBarcode()`/`WithSvgElement()` extension methods on `SvgBuilder` from the `.Svg` projects.
22. Update `ImageSharpBuilder` extensions to come from the `.ImageSharp` projects.
23. Update `FlexRender.MetaPackage` to reference the new project names.
24. Update CLI project references.
25. Update all tests.

---

## Project Count Analysis

**Before:** 14 projects in src/
**After:** 20 projects in src/ (+9 new, -3 renamed = net +6)

New projects:
- `FlexRender.QrCode.Core`
- `FlexRender.QrCode.Svg`
- `FlexRender.QrCode.ImageSharp`
- `FlexRender.Barcode.Core`
- `FlexRender.Barcode.Svg`
- `FlexRender.Barcode.ImageSharp`
- `FlexRender.SvgElement.Core`
- `FlexRender.SvgElement.Svg`

Renamed projects:
- `FlexRender.QrCode` -> `FlexRender.QrCode.Skia`
- `FlexRender.Barcode` -> `FlexRender.Barcode.Skia`
- `FlexRender.SvgElement` -> `FlexRender.SvgElement.Skia`

This is reasonable for a library with three rendering backends. Each project is small and focused. The Core projects contain only encoding/parsing logic (one or two files each). The per-backend projects contain a single provider class plus a builder extension method.

---

## Dependency Graph (After)

```
FlexRender.Core  (AST, layout, interfaces -- no external deps)
  |
  +-- FlexRender.QrCode.Core  (+ QRCoder)
  |     +-- FlexRender.QrCode.Skia     (+ FlexRender.Skia)
  |     +-- FlexRender.QrCode.Svg      (standalone)
  |     +-- FlexRender.QrCode.ImageSharp (+ SixLabors.ImageSharp)
  |
  +-- FlexRender.Barcode.Core  (no external deps)
  |     +-- FlexRender.Barcode.Skia     (+ FlexRender.Skia)
  |     +-- FlexRender.Barcode.Svg      (standalone)
  |     +-- FlexRender.Barcode.ImageSharp (+ SixLabors.ImageSharp)
  |
  +-- FlexRender.SvgElement.Core  (no external deps)
  |     +-- FlexRender.SvgElement.Skia  (+ FlexRender.Skia, Svg.Skia)
  |     +-- FlexRender.SvgElement.Svg   (standalone, embeds SVG directly)
  |
  +-- FlexRender.Skia  (+ SkiaSharp)
  |     +-- FlexRender.HarfBuzz   (+ HarfBuzzSharp)
  |
  +-- FlexRender.Svg  (SVG output -- depends ONLY on Core now)
  |
  +-- FlexRender.ImageSharp  (+ SixLabors.ImageSharp, SixLabors.Fonts)
  |
  +-- FlexRender.Yaml  (+ YamlDotNet)
  +-- FlexRender.Http  (+ System.Net.Http)
```

Key improvement: `FlexRender.Svg` no longer depends on `FlexRender.Skia`. Users who only need SVG output can avoid pulling in SkiaSharp entirely.

---

## Risks and Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| PNG encode/decode overhead in Skia backend | Performance regression for raster rendering | Benchmark before/after. If measurable, add an internal `ISkiaContentProvider<T>` that returns `SKBitmap` directly, checked first in `RenderingEngine`. |
| Breaking change for existing NuGet consumers | Package rename breaks `<PackageReference>` | Publish old package IDs as empty shells that forward-reference new names. |
| Too many small NuGet packages confuse users | Adoption friction | MetaPackage already bundles everything. Document "quick start" with MetaPackage vs "a-la-carte" setup. |
| ImageSharp QR/Barcode builder extensions move to new packages | Source-breaking for ImageSharp users | The method names stay identical; only the NuGet package changes. Document in migration guide. |

## Performance Note on ContentResult

The `ContentResult` approach introduces a PNG encode in the provider and a PNG decode in the Skia rendering engine. For the SVG renderer this is free (it was already encoding to PNG base64). For the Skia renderer this is new overhead.

**Mitigation:** Introduce an internal marker interface `ISkiaNativeProvider<T>` in `FlexRender.Skia` that returns `SKBitmap` directly. The Skia rendering engine checks for this interface first. The `.Skia` provider projects implement both `IContentProvider<T>` (for cross-backend use) and `ISkiaNativeProvider<T>` (for zero-copy Skia rendering). This keeps the public API clean while avoiding the encode/decode round-trip in the hot path.

```csharp
// In FlexRender.Skia (internal)
internal interface ISkiaNativeProvider<in TElement>
{
    SKBitmap GenerateBitmap(TElement element, int width, int height);
}

// In FlexRender.QrCode.Skia
internal sealed class SkiaQrProvider
    : IContentProvider<QrElement>,
      ISvgContentProvider<QrElement>,
      ISkiaNativeProvider<QrElement>
{
    // Skia engine uses GenerateBitmap() directly
    // SVG engine uses Generate() which PNG-encodes
    // ISvgContentProvider used by SVG engine for vector output
}
```

This way:
- **Skia raster path:** zero overhead (uses `ISkiaNativeProvider<T>` -> `SKBitmap` directly).
- **SVG output path:** prefers `ISvgContentProvider<T>` for vector output, falls back to `IContentProvider<T>` -> PNG base64.
- **ImageSharp path:** uses `IContentProvider<T>` -> decodes PNG to ImageSharp Image.
- **Cross-backend:** any provider works with any renderer through `IContentProvider<T>`.

---

## Files to Create / Modify / Delete

### New files (Phase 2-3)

```
src/FlexRender.QrCode.Core/
    FlexRender.QrCode.Core.csproj
    QrEncoder.cs
    QrDataValidator.cs
    QrSvgPathBuilder.cs

src/FlexRender.Barcode.Core/
    FlexRender.Barcode.Core.csproj
    Code128Encoder.cs
    Code128Validator.cs

src/FlexRender.QrCode.Skia/          (renamed from FlexRender.QrCode/)
    FlexRender.QrCode.Skia.csproj
    Providers/SkiaQrProvider.cs       (renamed from QrProvider.cs)
    SkiaBuilderExtensions.cs

src/FlexRender.QrCode.Svg/
    FlexRender.QrCode.Svg.csproj
    Providers/SvgQrProvider.cs
    SvgBuilderExtensions.cs

src/FlexRender.QrCode.ImageSharp/
    FlexRender.QrCode.ImageSharp.csproj
    Providers/ImageSharpQrProvider.cs
    ImageSharpBuilderExtensions.cs

src/FlexRender.Barcode.Skia/         (renamed from FlexRender.Barcode/)
    FlexRender.Barcode.Skia.csproj
    Providers/SkiaBarcodeProvider.cs  (renamed from BarcodeProvider.cs)
    SkiaBuilderExtensions.cs

src/FlexRender.Barcode.Svg/
    FlexRender.Barcode.Svg.csproj
    Providers/SvgBarcodeProvider.cs
    SvgBuilderExtensions.cs

src/FlexRender.Barcode.ImageSharp/
    FlexRender.Barcode.ImageSharp.csproj
    Providers/ImageSharpBarcodeProvider.cs
    ImageSharpBuilderExtensions.cs

src/FlexRender.SvgElement.Core/
    FlexRender.SvgElement.Core.csproj
    SvgContentParser.cs       -- SVG content parsing/validation

src/FlexRender.SvgElement.Skia/  (renamed from FlexRender.SvgElement/)
    FlexRender.SvgElement.Skia.csproj
    Providers/SkiaSvgElementProvider.cs  (renamed from SvgElementProvider.cs)
    SkiaBuilderExtensions.cs

src/FlexRender.SvgElement.Svg/
    FlexRender.SvgElement.Svg.csproj
    Providers/SvgSvgElementProvider.cs  -- embeds SVG directly into SVG output
    SvgBuilderExtensions.cs
```

### Modified files (Phase 1)

```
src/FlexRender.Core/FlexRender.Core.csproj         -- add IContentProvider, ISvgContentProvider, IResourceLoaderAware
src/FlexRender.Skia/FlexRender.Skia.csproj          -- remove provider interfaces, add ISkiaNativeProvider
src/FlexRender.Skia/SkiaBuilder.cs                   -- update provider types
src/FlexRender.Skia/Rendering/RenderingEngine.cs     -- check ISkiaNativeProvider first, then IContentProvider
src/FlexRender.Svg/FlexRender.Svg.csproj             -- remove Skia dependency
src/FlexRender.Svg/Rendering/SvgRenderingEngine.cs   -- use ContentResult.PngBytes for base64
src/FlexRender.Svg/SvgRender.cs                      -- update constructor
src/FlexRender.Svg/SvgBuilderExtensions.cs            -- update provider assembly
src/FlexRender.ImageSharp/ImageSharpRender.cs          -- use IContentProvider instead of inline providers
src/FlexRender.MetaPackage/FlexRender.MetaPackage.csproj -- update references
```

### Deleted files (Phase 2-3)

```
src/FlexRender.Skia/Providers/IContentProvider.cs       -- moved to Core
src/FlexRender.Skia/Providers/ISvgContentProvider.cs     -- moved to Core
src/FlexRender.Skia/Providers/IResourceLoaderAware.cs    -- moved to Core
src/FlexRender.ImageSharp/Providers/ImageSharpQrProvider.cs     -- moved to QrCode.ImageSharp
src/FlexRender.ImageSharp/Providers/ImageSharpBarcodeProvider.cs -- moved to Barcode.ImageSharp
```

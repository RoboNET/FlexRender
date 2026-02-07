# Provider Restructuring Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Eliminate code duplication for encoding logic (Code128, QR), break `FlexRender.Svg`'s dependency on `FlexRender.Skia`, and create backend-neutral provider abstractions in `FlexRender.Core`.

**Architecture:** Move `IContentProvider<T>`, `ISvgContentProvider<T>`, and `IResourceLoaderAware` from `FlexRender.Skia` to `FlexRender.Core` with a new `ContentResult` return type (PNG bytes instead of `SKBitmap`). Extract shared encoding logic into `.Core` projects. Create per-backend provider projects (`.Skia`, `.Svg`, `.ImageSharp`). Add `ISkiaNativeProvider<T>` for zero-copy Skia rendering.

**Tech Stack:** .NET 10 + net8.0 multi-targeting, C# latest, xUnit, SkiaSharp, QRCoder, SixLabors.ImageSharp, Svg.Skia

---

## Prerequisites

Before starting, ensure the solution builds and all tests pass:

```bash
cd /Users/robonet/Projects/SkiaLayout
dotnet build FlexRender.slnx
dotnet test FlexRender.slnx
```

Create a feature branch:

```bash
git checkout -b refactor/provider-restructuring
```

---

## Phase 1: Move Interfaces to Core and Break Svg-to-Skia Dependency

### Task 1: Add ContentResult and Backend-Neutral Interfaces to FlexRender.Core

**Files:**
- Create: `src/FlexRender.Core/Providers/ContentResult.cs`
- Create: `src/FlexRender.Core/Providers/IContentProvider.cs`
- Create: `src/FlexRender.Core/Providers/ISvgContentProvider.cs`
- Create: `src/FlexRender.Core/Providers/IResourceLoaderAware.cs`
- Test: `tests/FlexRender.Tests/Providers/ContentResultTests.cs`

**Step 1: Write failing test**

Create `tests/FlexRender.Tests/Providers/ContentResultTests.cs`:

```csharp
using FlexRender.Providers;
using Xunit;

namespace FlexRender.Tests.Providers;

/// <summary>
/// Tests for the <see cref="ContentResult"/> record struct.
/// </summary>
public sealed class ContentResultTests
{
    [Fact]
    public void Constructor_WithValidArgs_StoresValues()
    {
        var pngBytes = new byte[] { 137, 80, 78, 71 }; // PNG magic bytes
        var result = new ContentResult(pngBytes, 100, 200);

        Assert.Same(pngBytes, result.PngBytes);
        Assert.Equal(100, result.Width);
        Assert.Equal(200, result.Height);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var bytes = new byte[] { 1, 2, 3 };
        var a = new ContentResult(bytes, 50, 50);
        var b = new ContentResult(bytes, 50, 50);

        Assert.Equal(a, b);
    }

    [Fact]
    public void Equality_DifferentDimensions_AreNotEqual()
    {
        var bytes = new byte[] { 1, 2, 3 };
        var a = new ContentResult(bytes, 50, 50);
        var b = new ContentResult(bytes, 100, 100);

        Assert.NotEqual(a, b);
    }
}
```

**Step 2: Run test (expect failure)**

```bash
cd /Users/robonet/Projects/SkiaLayout && dotnet test tests/FlexRender.Tests --filter "ContentResultTests" --no-restore
```

Expected: FAIL - `ContentResult` type not found.

**Step 3: Implement**

Create `src/FlexRender.Core/Providers/ContentResult.cs`:

```csharp
namespace FlexRender.Providers;

/// <summary>
/// Contains the result of a content provider's raster generation: PNG-encoded image bytes
/// along with the image dimensions. This is the backend-neutral exchange format between
/// content providers and rendering engines.
/// </summary>
/// <param name="PngBytes">The PNG-encoded image bytes.</param>
/// <param name="Width">The image width in pixels.</param>
/// <param name="Height">The image height in pixels.</param>
public readonly record struct ContentResult(byte[] PngBytes, int Width, int Height);
```

Create `src/FlexRender.Core/Providers/IContentProvider.cs`:

```csharp
namespace FlexRender.Providers;

/// <summary>
/// Provides raster content generation for template elements.
/// Returns PNG-encoded bytes for cross-backend compatibility.
/// </summary>
/// <typeparam name="TElement">The type of template element this provider handles.</typeparam>
public interface IContentProvider<in TElement>
{
    /// <summary>
    /// Generates a PNG-encoded bitmap representation of the element.
    /// </summary>
    /// <param name="element">The element to generate content for.</param>
    /// <param name="width">The allocated width in pixels.</param>
    /// <param name="height">The allocated height in pixels.</param>
    /// <returns>A <see cref="ContentResult"/> containing PNG bytes and dimensions.</returns>
    ContentResult Generate(TElement element, int width, int height);
}
```

Create `src/FlexRender.Core/Providers/ISvgContentProvider.cs`:

```csharp
namespace FlexRender.Providers;

/// <summary>
/// Provides SVG-native content generation for template elements.
/// </summary>
/// <remarks>
/// <para>
/// Content providers that implement this interface can generate native SVG markup
/// instead of rasterized bitmaps. The SVG rendering engine checks for this interface
/// and uses it when available, falling back to bitmap rasterization via
/// <see cref="IContentProvider{TElement}"/> otherwise.
/// </para>
/// <para>
/// The returned SVG markup is inserted directly into the SVG document at the
/// specified position and dimensions. It should not include an outer wrapping element
/// -- the rendering engine handles positioning via a nested <c>&lt;svg&gt;</c> element.
/// </para>
/// </remarks>
/// <typeparam name="TElement">The type of template element this provider handles.</typeparam>
public interface ISvgContentProvider<in TElement>
{
    /// <summary>
    /// Generates SVG markup for the specified element.
    /// </summary>
    /// <param name="element">The element to generate SVG content for.</param>
    /// <param name="width">The allocated width in SVG user units.</param>
    /// <param name="height">The allocated height in SVG user units.</param>
    /// <returns>A string containing SVG markup (e.g., path, rect, or group elements).</returns>
    string GenerateSvgContent(TElement element, float width, float height);
}
```

Create `src/FlexRender.Core/Providers/IResourceLoaderAware.cs`:

```csharp
using FlexRender.Abstractions;

namespace FlexRender.Providers;

/// <summary>
/// Allows content providers to receive resource loaders for loading external assets.
/// </summary>
/// <remarks>
/// Content providers that need to load resources from URIs (files, HTTP, base64, embedded)
/// can implement this interface to receive the configured resource loader chain.
/// The loaders are injected after construction by the rendering infrastructure.
/// </remarks>
public interface IResourceLoaderAware
{
    /// <summary>
    /// Sets the resource loaders for this provider.
    /// </summary>
    /// <param name="loaders">The ordered collection of resource loaders.</param>
    void SetResourceLoaders(IReadOnlyList<IResourceLoader> loaders);
}
```

**Step 4: Run test (expect success)**

```bash
cd /Users/robonet/Projects/SkiaLayout && dotnet test tests/FlexRender.Tests --filter "ContentResultTests" --no-restore
```

Expected: PASS (3 tests)

**Step 5: Commit**

```bash
cd /Users/robonet/Projects/SkiaLayout && git add src/FlexRender.Core/Providers/ContentResult.cs src/FlexRender.Core/Providers/IContentProvider.cs src/FlexRender.Core/Providers/ISvgContentProvider.cs src/FlexRender.Core/Providers/IResourceLoaderAware.cs tests/FlexRender.Tests/Providers/ContentResultTests.cs && git commit -m "feat(core): add backend-neutral provider interfaces and ContentResult to Core"
```

---

### Task 2: Add ISkiaNativeProvider to FlexRender.Skia

This internal interface allows Skia providers to return `SKBitmap` directly for zero-copy rendering, avoiding the PNG encode/decode overhead in the raster hot path.

**Files:**
- Create: `src/FlexRender.Skia/Providers/ISkiaNativeProvider.cs`
- Test: `tests/FlexRender.Tests/Providers/SkiaNativeProviderInterfaceTests.cs`

**Step 1: Write failing test**

Create `tests/FlexRender.Tests/Providers/SkiaNativeProviderInterfaceTests.cs`:

```csharp
using FlexRender.Providers;
using SkiaSharp;
using Xunit;

namespace FlexRender.Tests.Providers;

/// <summary>
/// Tests for <see cref="ISkiaNativeProvider{TElement}"/>.
/// </summary>
public sealed class SkiaNativeProviderInterfaceTests
{
    [Fact]
    public void ISkiaNativeProvider_IsGenericInterface()
    {
        var type = typeof(ISkiaNativeProvider<>);
        Assert.True(type.IsInterface);
        Assert.True(type.IsGenericTypeDefinition);
    }

    [Fact]
    public void ISkiaNativeProvider_HasGenerateBitmapMethod()
    {
        var type = typeof(ISkiaNativeProvider<string>);
        var method = type.GetMethod("GenerateBitmap");

        Assert.NotNull(method);
        Assert.Equal(typeof(SKBitmap), method!.ReturnType);
    }
}
```

**Step 2: Run test (expect failure)**

```bash
cd /Users/robonet/Projects/SkiaLayout && dotnet test tests/FlexRender.Tests --filter "SkiaNativeProviderInterfaceTests" --no-restore
```

Expected: FAIL - `ISkiaNativeProvider` not found.

**Step 3: Implement**

Create `src/FlexRender.Skia/Providers/ISkiaNativeProvider.cs`:

```csharp
using SkiaSharp;

namespace FlexRender.Providers;

/// <summary>
/// Internal optimization interface for Skia content providers.
/// Allows providers to return <see cref="SKBitmap"/> directly to the Skia rendering engine,
/// avoiding the PNG encode/decode overhead that <see cref="IContentProvider{TElement}"/> requires.
/// </summary>
/// <remarks>
/// <para>
/// The Skia rendering engine checks for this interface first. If a provider implements it,
/// <see cref="GenerateBitmap"/> is called instead of <see cref="IContentProvider{TElement}.Generate"/>.
/// This keeps the public API clean (all providers implement <see cref="IContentProvider{TElement}"/>)
/// while avoiding unnecessary serialization in the hot path.
/// </para>
/// </remarks>
/// <typeparam name="TElement">The type of template element this provider handles.</typeparam>
internal interface ISkiaNativeProvider<in TElement>
{
    /// <summary>
    /// Generates a bitmap representation of the element for direct Skia canvas drawing.
    /// </summary>
    /// <param name="element">The element to generate content for.</param>
    /// <param name="width">The allocated width in pixels.</param>
    /// <param name="height">The allocated height in pixels.</param>
    /// <returns>An <see cref="SKBitmap"/> containing the rendered content. Caller is responsible for disposal.</returns>
    SKBitmap GenerateBitmap(TElement element, int width, int height);
}
```

**Step 4: Run test (expect success)**

```bash
cd /Users/robonet/Projects/SkiaLayout && dotnet test tests/FlexRender.Tests --filter "SkiaNativeProviderInterfaceTests" --no-restore
```

Expected: PASS (2 tests)

**Step 5: Commit**

```bash
cd /Users/robonet/Projects/SkiaLayout && git add src/FlexRender.Skia/Providers/ISkiaNativeProvider.cs tests/FlexRender.Tests/Providers/SkiaNativeProviderInterfaceTests.cs && git commit -m "feat(skia): add ISkiaNativeProvider for zero-copy bitmap rendering"
```

---

### Task 3: Update QrProvider to Implement New Interfaces

The existing `QrProvider` currently implements the old `IContentProvider<QrElement>` (returns `SKBitmap` via `Generate(element)` with no width/height). Update it to implement the new Core `IContentProvider<QrElement>` (returns `ContentResult`) and `ISkiaNativeProvider<QrElement>` (returns `SKBitmap` directly).

**Files:**
- Modify: `src/FlexRender.QrCode/Providers/QrProvider.cs`
- Modify: `tests/FlexRender.Tests/Providers/QrProviderTests.cs`

**Step 1: Write failing test**

Update `tests/FlexRender.Tests/Providers/QrProviderTests.cs` -- add these tests (append to the existing test class):

```csharp
    /// <summary>
    /// Verifies QrProvider implements the new Core IContentProvider with ContentResult.
    /// </summary>
    [Fact]
    public void Generate_NewInterface_ReturnsContentResult()
    {
        var element = new QrElement
        {
            Data = "Hello, World!",
            Size = 100
        };

        IContentProvider<QrElement> provider = _provider;
        var result = provider.Generate(element, 100, 100);

        Assert.NotNull(result.PngBytes);
        Assert.True(result.PngBytes.Length > 0);
        Assert.Equal(100, result.Width);
        Assert.Equal(100, result.Height);
        // Verify PNG magic bytes
        Assert.Equal(137, result.PngBytes[0]);
        Assert.Equal(80, result.PngBytes[1]);  // 'P'
        Assert.Equal(78, result.PngBytes[2]);  // 'N'
        Assert.Equal(71, result.PngBytes[3]);  // 'G'
    }

    /// <summary>
    /// Verifies QrProvider implements ISkiaNativeProvider for zero-copy Skia rendering.
    /// </summary>
    [Fact]
    public void QrProvider_ImplementsISkiaNativeProvider()
    {
        Assert.IsAssignableFrom<ISkiaNativeProvider<QrElement>>(_provider);
    }

    /// <summary>
    /// Verifies GenerateBitmap returns correct-sized SKBitmap.
    /// </summary>
    [Fact]
    public void GenerateBitmap_ReturnsCorrectSizedBitmap()
    {
        var element = new QrElement
        {
            Data = "Test",
            Size = 150
        };

        var nativeProvider = (ISkiaNativeProvider<QrElement>)_provider;
        using var bitmap = nativeProvider.GenerateBitmap(element, 150, 150);

        Assert.NotNull(bitmap);
        Assert.Equal(150, bitmap.Width);
        Assert.Equal(150, bitmap.Height);
    }
```

**Step 2: Run test (expect failure)**

```bash
cd /Users/robonet/Projects/SkiaLayout && dotnet test tests/FlexRender.Tests --filter "QrProviderTests" --no-restore
```

Expected: FAIL - compilation errors because `IContentProvider<T>.Generate` signature changed.

**Step 3: Implement**

First, delete the OLD interfaces from `FlexRender.Skia`:
- Delete `src/FlexRender.Skia/Providers/IContentProvider.cs`
- Delete `src/FlexRender.Skia/Providers/ISvgContentProvider.cs`
- Delete `src/FlexRender.Skia/Providers/IResourceLoaderAware.cs`

Then update `src/FlexRender.QrCode/Providers/QrProvider.cs`:

```csharp
using System.Globalization;
using System.Text;
using FlexRender.Parsing.Ast;
using FlexRender.Providers;
using FlexRender.Rendering;
using QRCoder;
using SkiaSharp;

namespace FlexRender.QrCode.Providers;

/// <summary>
/// Provides QR code generation as both raster bitmaps and native SVG markup.
/// </summary>
/// <remarks>
/// <para>
/// For raster output (PNG, JPEG), generates PNG bytes via <see cref="IContentProvider{TElement}"/>.
/// For Skia-native rendering, returns <see cref="SKBitmap"/> directly via <see cref="ISkiaNativeProvider{TElement}"/>.
/// For SVG output, generates native SVG path elements via <see cref="ISvgContentProvider{TElement}"/>,
/// producing smaller, scalable, pixel-perfect vector QR codes.
/// </para>
/// <para>
/// The SVG output uses horizontal run-length encoding to minimize path data size.
/// Adjacent dark modules on the same row are merged into a single rectangle sub-path.
/// </para>
/// </remarks>
public sealed class QrProvider : IContentProvider<QrElement>, ISvgContentProvider<QrElement>, ISkiaNativeProvider<QrElement>
{
    /// <summary>
    /// Maximum data capacity in bytes for each error correction level.
    /// </summary>
    private static readonly Dictionary<ErrorCorrectionLevel, int> MaxDataCapacity = new()
    {
        { ErrorCorrectionLevel.L, 2953 },
        { ErrorCorrectionLevel.M, 2331 },
        { ErrorCorrectionLevel.Q, 1663 },
        { ErrorCorrectionLevel.H, 1273 }
    };

    /// <summary>
    /// Generates a PNG-encoded QR code from the specified element configuration.
    /// </summary>
    /// <param name="element">The QR code element configuration.</param>
    /// <param name="width">The target width in pixels.</param>
    /// <param name="height">The target height in pixels.</param>
    /// <returns>A <see cref="ContentResult"/> containing PNG bytes.</returns>
    public ContentResult Generate(QrElement element, int width, int height)
    {
        var targetSize = Math.Min(width, height);
        if (targetSize <= 0) targetSize = element.Size ?? 100;

        using var bitmap = GenerateSkBitmap(element, targetSize);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);

        return new ContentResult(data.ToArray(), bitmap.Width, bitmap.Height);
    }

    /// <summary>
    /// Generates a bitmap for direct Skia canvas drawing (zero-copy hot path).
    /// </summary>
    /// <param name="element">The QR code element configuration.</param>
    /// <param name="width">The target width in pixels.</param>
    /// <param name="height">The target height in pixels.</param>
    /// <returns>An <see cref="SKBitmap"/> containing the rendered QR code.</returns>
    SKBitmap ISkiaNativeProvider<QrElement>.GenerateBitmap(QrElement element, int width, int height)
    {
        var targetSize = Math.Min(width, height);
        if (targetSize <= 0) targetSize = element.Size ?? 100;

        return GenerateSkBitmap(element, targetSize);
    }

    /// <summary>
    /// Core SKBitmap generation shared by both Generate and GenerateBitmap.
    /// </summary>
    private static SKBitmap GenerateSkBitmap(QrElement element, int targetSize)
    {
        ArgumentNullException.ThrowIfNull(element);

        if (string.IsNullOrEmpty(element.Data))
        {
            throw new ArgumentException("QR code data cannot be empty.", nameof(element));
        }

        if (targetSize <= 0)
        {
            throw new ArgumentException("QR code size must be positive.", nameof(element));
        }

        var eccLevel = MapEccLevel(element.ErrorCorrection);
        ValidateDataCapacity(element);

        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(element.Data, eccLevel);

        var moduleCount = qrCodeData.ModuleMatrix.Count;
        var moduleSize = targetSize / (float)moduleCount;

        var bitmap = new SKBitmap(targetSize, targetSize);
        using var canvas = new SKCanvas(bitmap);

        var foreground = ColorParser.Parse(element.Foreground);
        var background = element.Background is not null
            ? ColorParser.Parse(element.Background)
            : SKColors.Transparent;

        canvas.Clear(background);

        using var paint = new SKPaint
        {
            Color = foreground,
            IsAntialias = false,
            Style = SKPaintStyle.Fill
        };

        for (var y = 0; y < moduleCount; y++)
        {
            for (var x = 0; x < moduleCount; x++)
            {
                if (qrCodeData.ModuleMatrix[y][x])
                {
                    var rect = new SKRect(
                        x * moduleSize,
                        y * moduleSize,
                        (x + 1) * moduleSize,
                        (y + 1) * moduleSize);
                    canvas.DrawRect(rect, paint);
                }
            }
        }

        return bitmap;
    }

    /// <summary>
    /// Generates native SVG markup for a QR code element.
    /// </summary>
    /// <param name="element">The QR code element configuration.</param>
    /// <param name="width">The allocated width in SVG user units.</param>
    /// <param name="height">The allocated height in SVG user units.</param>
    /// <returns>SVG markup containing the QR code as vector paths.</returns>
    public string GenerateSvgContent(QrElement element, float width, float height)
    {
        ArgumentNullException.ThrowIfNull(element);

        if (string.IsNullOrEmpty(element.Data))
        {
            throw new ArgumentException("QR code data cannot be empty.", nameof(element));
        }

        var eccLevel = MapEccLevel(element.ErrorCorrection);
        ValidateDataCapacity(element);

        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(element.Data, eccLevel);

        var moduleCount = qrCodeData.ModuleMatrix.Count;
        var moduleWidth = width / moduleCount;
        var moduleHeight = height / moduleCount;

        var sb = new StringBuilder(1024);
        sb.Append("<g>");

        if (element.Background is not null)
        {
            sb.Append("<rect width=\"").Append(F(width));
            sb.Append("\" height=\"").Append(F(height));
            sb.Append("\" fill=\"").Append(EscapeXml(element.Background)).Append("\"/>");
        }

        var pathData = BuildPathData(qrCodeData, moduleCount, moduleWidth, moduleHeight);

        if (pathData.Length > 0)
        {
            sb.Append("<path d=\"").Append(pathData);
            sb.Append("\" fill=\"").Append(EscapeXml(element.Foreground)).Append("\"/>");
        }

        sb.Append("</g>");
        return sb.ToString();
    }

    private static string BuildPathData(
        QRCodeData qrCodeData,
        int moduleCount,
        float moduleWidth,
        float moduleHeight)
    {
        var sb = new StringBuilder(moduleCount * moduleCount / 2);

        for (var row = 0; row < moduleCount; row++)
        {
            var col = 0;
            while (col < moduleCount)
            {
                if (!qrCodeData.ModuleMatrix[row][col])
                {
                    col++;
                    continue;
                }

                var runStart = col;
                while (col < moduleCount && qrCodeData.ModuleMatrix[row][col])
                {
                    col++;
                }

                var runLength = col - runStart;

                var x = runStart * moduleWidth;
                var y = row * moduleHeight;
                var w = runLength * moduleWidth;

                sb.Append('M').Append(F(x)).Append(' ').Append(F(y));
                sb.Append('h').Append(F(w));
                sb.Append('v').Append(F(moduleHeight));
                sb.Append('h').Append(F(-w));
                sb.Append('z');
            }
        }

        return sb.ToString();
    }

    private static QRCodeGenerator.ECCLevel MapEccLevel(ErrorCorrectionLevel level)
    {
        return level switch
        {
            ErrorCorrectionLevel.L => QRCodeGenerator.ECCLevel.L,
            ErrorCorrectionLevel.M => QRCodeGenerator.ECCLevel.M,
            ErrorCorrectionLevel.Q => QRCodeGenerator.ECCLevel.Q,
            ErrorCorrectionLevel.H => QRCodeGenerator.ECCLevel.H,
            _ => QRCodeGenerator.ECCLevel.M
        };
    }

    private static void ValidateDataCapacity(QrElement element)
    {
        var dataBytes = Encoding.UTF8.GetByteCount(element.Data);
        var maxCapacity = MaxDataCapacity[element.ErrorCorrection];
        if (dataBytes > maxCapacity)
        {
            throw new ArgumentException(
                $"QR code data ({dataBytes} bytes) exceeds maximum capacity for error correction level " +
                $"{element.ErrorCorrection} ({maxCapacity} bytes).",
                nameof(element));
        }
    }

    private static string EscapeXml(string value)
    {
        if (value.AsSpan().IndexOfAny("&<>\"'") < 0)
            return value;
        return value.Replace("&", "&amp;")
                    .Replace("<", "&lt;")
                    .Replace(">", "&gt;")
                    .Replace("\"", "&quot;")
                    .Replace("'", "&apos;");
    }

    private static string F(float value)
    {
        return value.ToString("G", CultureInfo.InvariantCulture);
    }
}
```

**Important:** The existing `QrProviderTests` call `_provider.Generate(element)` which is the old signature with a single argument returning `SKBitmap`. These tests need to be updated to call the static helper or use `ISkiaNativeProvider`. Update the existing tests in `QrProviderTests` to change:
- `_provider.Generate(element)` becomes `((ISkiaNativeProvider<QrElement>)_provider).GenerateBitmap(element, element.Size ?? 100, element.Size ?? 100)`
- `QrProvider.Generate(element, null, null)` becomes `((ISkiaNativeProvider<QrElement>)_provider).GenerateBitmap(element, element.Size ?? 100, element.Size ?? 100)` (the static method is removed).

This is a breaking change to the existing test file. Rewrite the entire test file to use the new interfaces.

**Step 4: Run test (expect success)**

```bash
cd /Users/robonet/Projects/SkiaLayout && dotnet test tests/FlexRender.Tests --filter "QrProviderTests" --no-restore
```

Expected: PASS

**Step 5: Commit**

```bash
cd /Users/robonet/Projects/SkiaLayout && git add -A && git commit -m "refactor(qr): update QrProvider to implement new Core interfaces"
```

---

### Task 4: Update BarcodeProvider to Implement New Interfaces

**Files:**
- Modify: `src/FlexRender.Barcode/Providers/BarcodeProvider.cs`
- Modify: `tests/FlexRender.Tests/Providers/BarcodeProviderTests.cs`

**Step 1: Write failing test**

Add to `tests/FlexRender.Tests/Providers/BarcodeProviderTests.cs`:

```csharp
    /// <summary>
    /// Verifies BarcodeProvider implements the new Core IContentProvider with ContentResult.
    /// </summary>
    [Fact]
    public void Generate_NewInterface_ReturnsContentResult()
    {
        var element = new BarcodeElement
        {
            Data = "ABC123",
            Format = BarcodeFormat.Code128
        };

        IContentProvider<BarcodeElement> provider = _provider;
        var result = provider.Generate(element, 200, 80);

        Assert.NotNull(result.PngBytes);
        Assert.True(result.PngBytes.Length > 0);
        Assert.Equal(200, result.Width);
        Assert.Equal(80, result.Height);
        // Verify PNG magic bytes
        Assert.Equal(137, result.PngBytes[0]);
    }

    /// <summary>
    /// Verifies BarcodeProvider implements ISkiaNativeProvider.
    /// </summary>
    [Fact]
    public void BarcodeProvider_ImplementsISkiaNativeProvider()
    {
        Assert.IsAssignableFrom<ISkiaNativeProvider<BarcodeElement>>(_provider);
    }
```

**Step 2: Run test (expect failure)**

```bash
cd /Users/robonet/Projects/SkiaLayout && dotnet test tests/FlexRender.Tests --filter "BarcodeProviderTests" --no-restore
```

Expected: FAIL - compilation errors because `Generate(element)` signature mismatch.

**Step 3: Implement**

Update `src/FlexRender.Barcode/Providers/BarcodeProvider.cs` to implement `IContentProvider<BarcodeElement>` (new signature), `ISkiaNativeProvider<BarcodeElement>`:

The pattern is the same as QrProvider:
- `IContentProvider<BarcodeElement>.Generate(element, width, height)` generates `SKBitmap`, PNG-encodes it, returns `ContentResult`
- `ISkiaNativeProvider<BarcodeElement>.GenerateBitmap(element, width, height)` returns `SKBitmap` directly
- Remove the old `Generate(BarcodeElement element)` single-arg method
- Keep the static `Generate(element, layoutWidth, layoutHeight)` as a private `GenerateSkBitmap` method

Update existing tests in `BarcodeProviderTests` to use `ISkiaNativeProvider<BarcodeElement>` for bitmap-returning tests, similar to QrProvider changes.

**Step 4: Run test (expect success)**

```bash
cd /Users/robonet/Projects/SkiaLayout && dotnet test tests/FlexRender.Tests --filter "BarcodeProviderTests" --no-restore
```

Expected: PASS

**Step 5: Commit**

```bash
cd /Users/robonet/Projects/SkiaLayout && git add -A && git commit -m "refactor(barcode): update BarcodeProvider to implement new Core interfaces"
```

---

### Task 5: Update SvgElementProvider to Implement New Interfaces

**Files:**
- Modify: `src/FlexRender.SvgElement/Providers/SvgElementProvider.cs`
- Modify: `tests/FlexRender.Tests/Providers/SvgElementProviderTests.cs`

**Step 1: Write failing test**

Add to `tests/FlexRender.Tests/Providers/SvgElementProviderTests.cs`:

```csharp
    /// <summary>
    /// Verifies SvgElementProvider implements the new Core IContentProvider with ContentResult.
    /// </summary>
    [Fact]
    public void Generate_NewInterface_ReturnsContentResult()
    {
        var provider = CreateProviderWithoutLoaders();
        var element = new SvgAstElement
        {
            Content = MinimalSvg,
            SvgWidth = 50,
            SvgHeight = 50
        };

        IContentProvider<SvgAstElement> contentProvider = provider;
        var result = contentProvider.Generate(element, 50, 50);

        result.PngBytes.Should().NotBeNull();
        result.PngBytes.Length.Should().BeGreaterThan(0);
        result.Width.Should().Be(50);
        result.Height.Should().Be(50);
    }

    /// <summary>
    /// Verifies SvgElementProvider implements ISkiaNativeProvider.
    /// </summary>
    [Fact]
    public void SvgElementProvider_ImplementsISkiaNativeProvider()
    {
        var provider = CreateProviderWithoutLoaders();
        provider.Should().BeAssignableTo<ISkiaNativeProvider<SvgAstElement>>();
    }
```

**Step 2: Run test (expect failure)**

```bash
cd /Users/robonet/Projects/SkiaLayout && dotnet test tests/FlexRender.Tests --filter "SvgElementProviderTests" --no-restore
```

Expected: FAIL

**Step 3: Implement**

Update `src/FlexRender.SvgElement/Providers/SvgElementProvider.cs`:
- Implement `IContentProvider<SvgElement>` (new signature with `int width, int height`, returns `ContentResult`)
- Implement `ISkiaNativeProvider<SvgElement>` (returns `SKBitmap` directly)
- The old `Generate(SvgElement element)` becomes `GenerateBitmap(SvgElement element, int width, int height)` for the native provider
- `Generate(SvgElement element, int width, int height)` for ContentResult PNG-encodes the bitmap

Update existing tests that called `provider.Generate(element)` (old single-arg signature) to use `((ISkiaNativeProvider<SvgAstElement>)provider).GenerateBitmap(element, width, height)`.

**Step 4: Run test (expect success)**

```bash
cd /Users/robonet/Projects/SkiaLayout && dotnet test tests/FlexRender.Tests --filter "SvgElementProviderTests" --no-restore
```

Expected: PASS

**Step 5: Commit**

```bash
cd /Users/robonet/Projects/SkiaLayout && git add -A && git commit -m "refactor(svg-element): update SvgElementProvider to implement new Core interfaces"
```

---

### Task 6: Update SkiaBuilder and Skia RenderingEngine to Use New Interfaces

The Skia rendering engine currently calls `_qrProvider.Generate(qr)` which returns `SKBitmap`. It needs to check for `ISkiaNativeProvider<T>` first (zero-copy), then fall back to `IContentProvider<T>` (PNG decode).

**Files:**
- Modify: `src/FlexRender.Skia/SkiaBuilder.cs`
- Modify: `src/FlexRender.Skia/Rendering/RenderingEngine.cs`
- Modify: `src/FlexRender.Skia/Rendering/SkiaRenderer.cs`
- Modify: `src/FlexRender.Skia/SkiaRender.cs`

**Step 1: Write failing test**

No new test file needed. The existing snapshot/integration tests will validate this. But we need the solution to compile first.

**Step 2: Run build (expect failure)**

```bash
cd /Users/robonet/Projects/SkiaLayout && dotnet build FlexRender.slnx
```

Expected: FAIL - compilation errors in RenderingEngine, SkiaRenderer because they reference the old `Generate(element)` method.

**Step 3: Implement**

Update `src/FlexRender.Skia/Rendering/RenderingEngine.cs`:

In the `DrawElement` method, change the QR/Barcode/Svg provider dispatch:

```csharp
case QrElement qr when _qrProvider is ISkiaNativeProvider<QrElement> nativeQr:
    using (var bitmap = nativeQr.GenerateBitmap(qr, (int)width, (int)height))
    {
        DrawBitmapWithRotation(canvas, bitmap, element, x, y, width, height);
    }
    break;

case QrElement qr when _qrProvider is not null:
    var qrResult = _qrProvider.Generate(qr, (int)width, (int)height);
    using (var bitmap = SKBitmap.Decode(qrResult.PngBytes))
    {
        DrawBitmapWithRotation(canvas, bitmap, element, x, y, width, height);
    }
    break;

case BarcodeElement barcode when _barcodeProvider is ISkiaNativeProvider<BarcodeElement> nativeBarcode:
    using (var bitmap = nativeBarcode.GenerateBitmap(barcode, (int)width, (int)height))
    {
        DrawBitmapWithRotation(canvas, bitmap, element, x, y, width, height);
    }
    break;

case BarcodeElement barcode when _barcodeProvider is not null:
    var barcodeResult = _barcodeProvider.Generate(barcode, (int)width, (int)height);
    using (var bitmap = SKBitmap.Decode(barcodeResult.PngBytes))
    {
        DrawBitmapWithRotation(canvas, bitmap, element, x, y, width, height);
    }
    break;

case SvgElement svg when _svgProvider is ISkiaNativeProvider<SvgElement> nativeSvg:
    using (var bitmap = nativeSvg.GenerateBitmap(svg, (int)width, (int)height))
    {
        DrawBitmapWithRotation(canvas, bitmap, element, x, y, width, height);
    }
    break;

case SvgElement svg when _svgProvider is not null:
    var svgResult = _svgProvider.Generate(svg, (int)width, (int)height);
    using (var bitmap = SKBitmap.Decode(svgResult.PngBytes))
    {
        DrawBitmapWithRotation(canvas, bitmap, element, x, y, width, height);
    }
    break;
```

Update `src/FlexRender.Skia/SkiaBuilder.cs`: No signature changes needed -- the property types already use `IContentProvider<T>` from `FlexRender.Providers` namespace, which now resolves to the Core version.

Update `src/FlexRender.Skia/Rendering/SkiaRenderer.cs`: The constructor takes `IContentProvider<QrElement>?` etc. These should still compile since the namespace hasn't changed. Verify and fix if needed.

**Step 4: Run build and tests (expect success)**

```bash
cd /Users/robonet/Projects/SkiaLayout && dotnet build FlexRender.slnx && dotnet test tests/FlexRender.Tests --no-restore
```

Expected: PASS (all existing tests pass)

**Step 5: Commit**

```bash
cd /Users/robonet/Projects/SkiaLayout && git add -A && git commit -m "refactor(skia): update rendering engine to use ISkiaNativeProvider with ContentResult fallback"
```

---

### Task 7: Update SvgRenderingEngine to Use ContentResult (Remove SKBitmap Dependency)

The SVG rendering engine currently uses `SKBitmap`, `SKImage`, and `SKData` to encode bitmaps for base64 embedding. Replace this with `ContentResult.PngBytes` which are already PNG-encoded.

**Files:**
- Modify: `src/FlexRender.Svg/Rendering/SvgRenderingEngine.cs`
- Modify: `src/FlexRender.Svg/SvgRender.cs`
- Modify: `src/FlexRender.Svg/SvgBuilderExtensions.cs`
- Modify: `src/FlexRender.Svg/FlexRender.Svg.csproj` (remove Skia dependency)
- Test: existing SVG rendering tests

**Step 1: Write failing test**

No new test file. We need the build to pass with the Skia dependency removed.

**Step 2: Implement**

Update `src/FlexRender.Svg/Rendering/SvgRenderingEngine.cs`:

1. Remove `using SkiaSharp;` from the top.
2. Remove the `DrawBitmapElement(SKBitmap ...)` method.
3. Add a new `DrawBitmapElement(ContentResult ...)` method that takes `ContentResult` and base64-encodes `PngBytes` directly:

```csharp
    private static void DrawBitmapElement(
        StringBuilder sb,
        ContentResult content,
        float x,
        float y,
        float width,
        float height)
    {
        var base64 = Convert.ToBase64String(content.PngBytes);

        sb.Append("<image x=\"").Append(F(x)).Append("\" y=\"").Append(F(y));
        sb.Append("\" width=\"").Append(F(width));
        sb.Append("\" height=\"").Append(F(height)).Append('"');
        sb.Append(" href=\"data:image/png;base64,").Append(base64).Append('"');
        sb.Append(" preserveAspectRatio=\"xMidYMid meet\"");
        sb.Append("/>");
    }
```

4. Update the QR/Barcode dispatch in `DrawElement`:

```csharp
            case QrElement qr when _qrProvider is ISvgContentProvider<QrElement> svgQrProvider:
                DrawSvgContentProvider(sb, svgQrProvider, qr, x, y, width, height);
                break;

            case QrElement qr when _qrProvider is not null:
            {
                var content = _qrProvider.Generate(qr, (int)width, (int)height);
                DrawBitmapElement(sb, content, x, y, width, height);
                break;
            }

            case BarcodeElement barcode when _barcodeProvider is ISvgContentProvider<BarcodeElement> svgBarcodeProvider:
                DrawSvgContentProvider(sb, svgBarcodeProvider, barcode, x, y, width, height);
                break;

            case BarcodeElement barcode when _barcodeProvider is not null:
            {
                var content = _barcodeProvider.Generate(barcode, (int)width, (int)height);
                DrawBitmapElement(sb, content, x, y, width, height);
                break;
            }
```

5. The `BuildFontMap` method uses `SKData`, `SKTypeface` from SkiaSharp to extract font family names. Since we are removing the Skia dependency, replace this with simple file name extraction (use the font definition's name or fallback). The `BuildFontMap` method should:
   - Read font bytes from file
   - Use the file name (without extension) as the font family name instead of `SKTypeface.FromData`
   - Skip `SKData.CreateCopy` entirely

Replace the font family extraction block:
```csharp
// Before: using (var skData = SKData.CreateCopy(fontBytes))
//         using (var typeface = SKTypeface.FromData(skData))
//         { familyName = typeface?.FamilyName ?? ... }

// After: Use the font definition name or the file stem as fallback
familyName = fontName;
```

This means SVG font-face declarations will use the template font name as the CSS family name. This is actually more predictable than relying on the typeface's internal name.

6. Update `SvgRender.cs` constructor to accept `ISvgContentProvider<SvgElement>?` as well.

7. Update `SvgBuilderExtensions.cs` to pass SVG content providers from the SvgBuilder.

8. Update `FlexRender.Svg.csproj` to remove the Skia project reference:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <PackageId>FlexRender.Svg</PackageId>
    <Description>SVG vector output renderer for FlexRender. Generates scalable SVG from the same layout tree.</Description>
    <IsAotCompatible>true</IsAotCompatible>
  </PropertyGroup>

  <ItemGroup>
    <InternalsVisibleTo Include="FlexRender.Tests" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\FlexRender.Core\FlexRender.Core.csproj" />
    <!-- FlexRender.Skia dependency REMOVED -->
  </ItemGroup>
</Project>
```

**Step 3: Run build and tests**

```bash
cd /Users/robonet/Projects/SkiaLayout && dotnet build FlexRender.slnx && dotnet test FlexRender.slnx --no-restore
```

Expected: PASS (all existing tests pass)

**Step 4: Commit**

```bash
cd /Users/robonet/Projects/SkiaLayout && git add -A && git commit -m "refactor(svg): remove FlexRender.Svg dependency on FlexRender.Skia"
```

---

### Task 8: Update SvgBuilder With SVG-Native Provider Slots

Update the SvgBuilder to accept SVG-native providers directly, not just through the Skia sub-builder.

**Files:**
- Modify: `src/FlexRender.Svg/SvgBuilder.cs`
- Modify: `src/FlexRender.Svg/SvgBuilderExtensions.cs`
- Modify: `src/FlexRender.Svg/SvgRender.cs`
- Modify: `src/FlexRender.Svg/Rendering/SvgRenderingEngine.cs`
- Test: `tests/FlexRender.Tests/Rendering/SvgBuilderTests.cs`

**Step 1: Write failing test**

Create `tests/FlexRender.Tests/Rendering/SvgBuilderTests.cs`:

```csharp
using FlexRender.Parsing.Ast;
using FlexRender.Providers;
using FlexRender.Svg;
using Xunit;

namespace FlexRender.Tests.Rendering;

/// <summary>
/// Tests for the updated SvgBuilder with SVG-native provider slots.
/// </summary>
public sealed class SvgBuilderTests
{
    [Fact]
    public void SvgBuilder_SetQrSvgProvider_StoresProvider()
    {
        var builder = new SvgBuilder();
        var provider = new FakeQrSvgProvider();

        builder.SetQrSvgProvider(provider);

        Assert.Same(provider, builder.QrSvgProvider);
    }

    [Fact]
    public void SvgBuilder_SetBarcodeSvgProvider_StoresProvider()
    {
        var builder = new SvgBuilder();
        var provider = new FakeBarcodeSvgProvider();

        builder.SetBarcodeSvgProvider(provider);

        Assert.Same(provider, builder.BarcodeSvgProvider);
    }

    [Fact]
    public void SvgBuilder_SetQrRasterProvider_StoresProvider()
    {
        var builder = new SvgBuilder();
        var provider = new FakeQrRasterProvider();

        builder.SetQrRasterProvider(provider);

        Assert.Same(provider, builder.QrRasterProvider);
    }

    [Fact]
    public void SvgBuilder_SetBarcodeRasterProvider_StoresProvider()
    {
        var builder = new SvgBuilder();
        var provider = new FakeBarcodeRasterProvider();

        builder.SetBarcodeRasterProvider(provider);

        Assert.Same(provider, builder.BarcodeRasterProvider);
    }

    private sealed class FakeQrSvgProvider : ISvgContentProvider<QrElement>
    {
        public string GenerateSvgContent(QrElement element, float width, float height) => "<g/>";
    }

    private sealed class FakeBarcodeSvgProvider : ISvgContentProvider<BarcodeElement>
    {
        public string GenerateSvgContent(BarcodeElement element, float width, float height) => "<g/>";
    }

    private sealed class FakeQrRasterProvider : IContentProvider<QrElement>
    {
        public ContentResult Generate(QrElement element, int width, int height) =>
            new(Array.Empty<byte>(), width, height);
    }

    private sealed class FakeBarcodeRasterProvider : IContentProvider<BarcodeElement>
    {
        public ContentResult Generate(BarcodeElement element, int width, int height) =>
            new(Array.Empty<byte>(), width, height);
    }
}
```

**Step 2: Run test (expect failure)**

```bash
cd /Users/robonet/Projects/SkiaLayout && dotnet test tests/FlexRender.Tests --filter "SvgBuilderTests" --no-restore
```

Expected: FAIL - `SetQrSvgProvider`, `QrSvgProvider` etc. do not exist.

**Step 3: Implement**

Update `src/FlexRender.Svg/SvgBuilder.cs` to add provider slots:

```csharp
using FlexRender.Abstractions;
using FlexRender.Configuration;
using FlexRender.Parsing.Ast;
using FlexRender.Providers;

namespace FlexRender.Svg;

/// <summary>
/// Builder for configuring SVG rendering options, SVG-native providers, and optional raster backend.
/// </summary>
public sealed class SvgBuilder
{
    private Action<SkiaBuilder>? _skiaConfigureAction;
    private bool _skiaEnabled;
    private Func<FlexRenderBuilder, IFlexRender>? _rasterFactory;

    internal bool IsSkiaEnabled => _skiaEnabled;
    internal Action<SkiaBuilder>? SkiaConfigureAction => _skiaConfigureAction;
    internal Func<FlexRenderBuilder, IFlexRender>? RasterFactory => _rasterFactory;

    // SVG-native providers
    internal ISvgContentProvider<QrElement>? QrSvgProvider { get; private set; }
    internal ISvgContentProvider<BarcodeElement>? BarcodeSvgProvider { get; private set; }
    internal ISvgContentProvider<SvgElement>? SvgElementSvgProvider { get; private set; }

    // Raster providers (backend-neutral, used when no SVG-native provider is available)
    internal IContentProvider<QrElement>? QrRasterProvider { get; private set; }
    internal IContentProvider<BarcodeElement>? BarcodeRasterProvider { get; private set; }

    /// <summary>
    /// Sets the SVG-native QR code content provider.
    /// </summary>
    internal void SetQrSvgProvider(ISvgContentProvider<QrElement> provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        QrSvgProvider = provider;
    }

    /// <summary>
    /// Sets the SVG-native barcode content provider.
    /// </summary>
    internal void SetBarcodeSvgProvider(ISvgContentProvider<BarcodeElement> provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        BarcodeSvgProvider = provider;
    }

    /// <summary>
    /// Sets the SVG-native SVG element content provider.
    /// </summary>
    internal void SetSvgElementSvgProvider(ISvgContentProvider<SvgElement> provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        SvgElementSvgProvider = provider;
    }

    /// <summary>
    /// Sets the raster QR code content provider (for PNG-to-base64 embedding).
    /// </summary>
    internal void SetQrRasterProvider(IContentProvider<QrElement> provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        QrRasterProvider = provider;
    }

    /// <summary>
    /// Sets the raster barcode content provider (for PNG-to-base64 embedding).
    /// </summary>
    internal void SetBarcodeRasterProvider(IContentProvider<BarcodeElement> provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        BarcodeRasterProvider = provider;
    }

    /// <summary>
    /// Enables the Skia raster backend for PNG, JPEG, BMP, and Raw output alongside SVG.
    /// </summary>
    public SvgBuilder WithSkia(Action<SkiaBuilder>? configure = null)
    {
        if (_rasterFactory is not null)
        {
            throw new InvalidOperationException(
                "A raster backend is already configured via WithRasterBackend(). Cannot also use WithSkia().");
        }
        _skiaEnabled = true;
        _skiaConfigureAction = configure;
        return this;
    }

    /// <summary>
    /// Sets a custom raster rendering backend for PNG, JPEG, BMP, and Raw output alongside SVG.
    /// </summary>
    public SvgBuilder WithRasterBackend(Func<FlexRenderBuilder, IFlexRender> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        if (_skiaEnabled)
        {
            throw new InvalidOperationException(
                "Skia raster backend is already configured via WithSkia(). Cannot also use WithRasterBackend().");
        }
        _rasterFactory = factory;
        return this;
    }
}
```

Note: The `SvgBuilder.WithSkia` method references `SkiaBuilder` which is in `FlexRender.Skia`. Since we removed the Skia dependency from `FlexRender.Svg`, this will not compile. We need to make `WithSkia` available only when `FlexRender.Skia` is referenced. The solution: move `WithSkia` to an extension method in `FlexRender.Skia` instead of being a method on `SvgBuilder`.

Actually, looking more carefully: `SvgBuilder` currently references `SkiaBuilder` directly. After removing the Skia dependency, `SvgBuilder` cannot reference `SkiaBuilder`. The solution is:

1. `SvgBuilder` stores a generic `Action<object>?` configure action and a type discriminator
2. OR better: `SvgBuilder.WithSkia()` and `SkiaConfigureAction` stay but are gated by a `dynamic` or delegate approach

The simplest approach is to use `Func<FlexRenderBuilder, IFlexRender>?` for ALL raster backends including Skia. The `WithSkia()` extension method on `SvgBuilder` would be defined in `FlexRender.Skia` (not in `FlexRender.Svg`):

Remove the `WithSkia` method from `SvgBuilder`. Make `SvgBuilder` only have the raster factory approach. Then in `FlexRender.Skia`, add a `SvgBuilderSkiaExtensions` class with `WithSkia(this SvgBuilder builder, Action<SkiaBuilder>? configure = null)` that calls `builder.WithRasterBackend(...)`.

Update `src/FlexRender.Svg/SvgBuilder.cs` accordingly -- remove `WithSkia`, `_skiaEnabled`, `_skiaConfigureAction`, `IsSkiaEnabled`, `SkiaConfigureAction`. Keep only `WithRasterBackend` and the provider slots.

Then add to `FlexRender.Skia` a new extension method file for `SvgBuilder`:

Create `src/FlexRender.Skia/SvgBuilderSkiaExtensions.cs`:

```csharp
using FlexRender.Configuration;
using FlexRender.Svg;

namespace FlexRender;

/// <summary>
/// Extension methods for configuring Skia as the raster backend for SVG rendering.
/// </summary>
public static class SvgBuilderSkiaExtensions
{
    /// <summary>
    /// Enables the Skia raster backend for PNG, JPEG, BMP, and Raw output alongside SVG.
    /// Also extracts Skia providers (QR, Barcode, SVG element) for SVG embedding.
    /// </summary>
    public static SvgBuilder WithSkia(this SvgBuilder builder, Action<SkiaBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var skiaBuilder = new SkiaBuilder();
        configure?.Invoke(skiaBuilder);

        // Extract providers for SVG embedding
        if (skiaBuilder.QrProvider is not null)
        {
            builder.SetQrRasterProvider(skiaBuilder.QrProvider);
            if (skiaBuilder.QrProvider is Providers.ISvgContentProvider<Parsing.Ast.QrElement> svgQr)
            {
                builder.SetQrSvgProvider(svgQr);
            }
        }

        if (skiaBuilder.BarcodeProvider is not null)
        {
            builder.SetBarcodeRasterProvider(skiaBuilder.BarcodeProvider);
            if (skiaBuilder.BarcodeProvider is Providers.ISvgContentProvider<Parsing.Ast.BarcodeElement> svgBarcode)
            {
                builder.SetBarcodeSvgProvider(svgBarcode);
            }
        }

        builder.WithRasterBackend(b => new Skia.SkiaRender(
            b.Limits,
            b.Options,
            b.ResourceLoaders,
            skiaBuilder,
            b.FilterRegistry));

        return builder;
    }
}
```

Then update `SvgBuilderExtensions.cs` to wire the providers from `SvgBuilder` into `SvgRender`:

```csharp
builder.SetRendererFactory(b =>
{
    Abstractions.IFlexRender? rasterRenderer = null;

    if (svgBuilder.RasterFactory is not null)
    {
        rasterRenderer = svgBuilder.RasterFactory(b);
    }

    return new SvgRender(
        b.Limits,
        b.Options,
        rasterRenderer,
        svgBuilder.QrSvgProvider,
        svgBuilder.QrRasterProvider,
        svgBuilder.BarcodeSvgProvider,
        svgBuilder.BarcodeRasterProvider,
        svgBuilder.SvgElementSvgProvider);
});
```

Update `SvgRender` constructor to accept the new provider parameters:

```csharp
internal SvgRender(
    ResourceLimits limits,
    FlexRenderOptions options,
    IFlexRender? rasterRenderer = null,
    ISvgContentProvider<QrElement>? qrSvgProvider = null,
    IContentProvider<QrElement>? qrRasterProvider = null,
    ISvgContentProvider<BarcodeElement>? barcodeSvgProvider = null,
    IContentProvider<BarcodeElement>? barcodeRasterProvider = null,
    ISvgContentProvider<SvgElement>? svgElementSvgProvider = null)
```

Update `SvgRenderingEngine` constructor to accept both SVG-native and raster providers, with priority: SVG-native > raster > null.

**Step 4: Run tests**

```bash
cd /Users/robonet/Projects/SkiaLayout && dotnet build FlexRender.slnx && dotnet test FlexRender.slnx --no-restore
```

Expected: PASS

**Step 5: Commit**

```bash
cd /Users/robonet/Projects/SkiaLayout && git add -A && git commit -m "refactor(svg): add SVG-native provider slots to SvgBuilder, move WithSkia to extension"
```

---

### Task 9: Update FlexRender.Skia InternalsVisibleTo and Fix Remaining Build Issues

After the interface changes, some `InternalsVisibleTo` entries may need updating. The `FlexRender.Skia.csproj` has `InternalsVisibleTo` for `FlexRender.Barcode`, `FlexRender.QrCode`, `FlexRender.SvgElement`, `FlexRender.Svg`. Since `ISkiaNativeProvider` is internal, these projects need to keep their `InternalsVisibleTo`. Also, `FlexRender.Core.csproj` needs `InternalsVisibleTo` for the provider projects that access internal interfaces through it.

**Files:**
- Modify: `src/FlexRender.Core/FlexRender.Core.csproj` (add InternalsVisibleTo for new projects if needed)
- Modify: `src/FlexRender.Skia/FlexRender.Skia.csproj`

**Step 1: Build the entire solution**

```bash
cd /Users/robonet/Projects/SkiaLayout && dotnet build FlexRender.slnx
```

Fix any remaining build errors.

**Step 2: Run all tests**

```bash
cd /Users/robonet/Projects/SkiaLayout && dotnet test FlexRender.slnx
```

Expected: ALL tests pass.

**Step 3: Commit**

```bash
cd /Users/robonet/Projects/SkiaLayout && git add -A && git commit -m "build: fix InternalsVisibleTo and remaining build issues after interface migration"
```

---

## Phase 2: Extract Encoding Core Projects

### Task 10: Create FlexRender.Barcode.Core with Code128Encoder

Extract the Code128 encoding table and checksum logic from `BarcodeProvider` into a shared project.

**Files:**
- Create: `src/FlexRender.Barcode.Core/FlexRender.Barcode.Core.csproj`
- Create: `src/FlexRender.Barcode.Core/Code128Encoder.cs`
- Create: `tests/FlexRender.Tests/Encoding/Code128EncoderTests.cs`
- Modify: `FlexRender.slnx` (add new project)

**Step 1: Write failing test**

Create `tests/FlexRender.Tests/Encoding/Code128EncoderTests.cs`:

```csharp
using FlexRender.Barcode.Core;
using Xunit;

namespace FlexRender.Tests.Encoding;

/// <summary>
/// Tests for <see cref="Code128Encoder"/>.
/// </summary>
public sealed class Code128EncoderTests
{
    [Fact]
    public void Encode_SimpleData_ReturnsPattern()
    {
        var result = Code128Encoder.Encode("ABC");

        Assert.NotNull(result);
        Assert.True(result.Length > 0);
        // Pattern should only contain '0' and '1'
        Assert.All(result, c => Assert.True(c == '0' || c == '1'));
    }

    [Fact]
    public void Encode_IncludesStartPattern()
    {
        var result = Code128Encoder.Encode("A");

        // Code128 Start B pattern
        Assert.StartsWith("11010010000", result);
    }

    [Fact]
    public void Encode_IncludesStopPattern()
    {
        var result = Code128Encoder.Encode("A");

        Assert.EndsWith("1100011101011", result);
    }

    [Fact]
    public void Encode_SameInput_SameOutput()
    {
        var result1 = Code128Encoder.Encode("Hello");
        var result2 = Code128Encoder.Encode("Hello");

        Assert.Equal(result1, result2);
    }

    [Fact]
    public void IsValidCode128B_ValidChars_ReturnsTrue()
    {
        Assert.True(Code128Encoder.IsValidCode128B("ABC123"));
        Assert.True(Code128Encoder.IsValidCode128B("Hello World"));
        Assert.True(Code128Encoder.IsValidCode128B("test-data"));
    }

    [Fact]
    public void IsValidCode128B_InvalidChars_ReturnsFalse()
    {
        Assert.False(Code128Encoder.IsValidCode128B("Test\x00Invalid"));
        Assert.False(Code128Encoder.IsValidCode128B("\x01"));
    }

    [Fact]
    public void IsValidCode128B_EmptyString_ReturnsFalse()
    {
        Assert.False(Code128Encoder.IsValidCode128B(""));
        Assert.False(Code128Encoder.IsValidCode128B(null!));
    }

    [Theory]
    [InlineData("ABC123")]
    [InlineData("Hello World")]
    [InlineData("!@#$%^")]
    public void Encode_VariousInputs_ProducesValidPatterns(string input)
    {
        var result = Code128Encoder.Encode(input);

        Assert.NotNull(result);
        Assert.True(result.Length > 0);
    }
}
```

**Step 2: Run test (expect failure)**

```bash
cd /Users/robonet/Projects/SkiaLayout && dotnet test tests/FlexRender.Tests --filter "Code128EncoderTests" --no-restore
```

Expected: FAIL - `FlexRender.Barcode.Core` namespace not found.

**Step 3: Implement**

Create directory: `src/FlexRender.Barcode.Core/`

Create `src/FlexRender.Barcode.Core/FlexRender.Barcode.Core.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <PackageId>FlexRender.Barcode.Core</PackageId>
    <Description>Backend-neutral barcode encoding logic for FlexRender. No rendering dependencies.</Description>
    <IsAotCompatible>true</IsAotCompatible>
  </PropertyGroup>

  <ItemGroup>
    <InternalsVisibleTo Include="FlexRender.Tests" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\FlexRender.Core\FlexRender.Core.csproj" />
  </ItemGroup>

</Project>
```

Create `src/FlexRender.Barcode.Core/Code128Encoder.cs`:

```csharp
using System.Text;

namespace FlexRender.Barcode.Core;

/// <summary>
/// Encodes data into Code128 barcode patterns (character set B).
/// Returns a binary string of '0' and '1' representing bar/space patterns.
/// </summary>
/// <remarks>
/// This class contains the shared encoding logic used by all rendering backends
/// (Skia, ImageSharp, SVG). It has no rendering dependencies.
/// </remarks>
public static class Code128Encoder
{
    /// <summary>
    /// Code 128 encoding table for character set B (ASCII 32-127).
    /// </summary>
    private static readonly Dictionary<char, string> Code128BPatterns = new()
    {
        { ' ', "11011001100" }, { '!', "11001101100" }, { '"', "11001100110" },
        { '#', "10010011000" }, { '$', "10010001100" }, { '%', "10001001100" },
        { '&', "10011001000" }, { '\'', "10011000100" }, { '(', "10001100100" },
        { ')', "11001001000" }, { '*', "11001000100" }, { '+', "11000100100" },
        { ',', "10110011100" }, { '-', "10011011100" }, { '.', "10011001110" },
        { '/', "10111001100" }, { '0', "10011101100" }, { '1', "10011100110" },
        { '2', "11001110010" }, { '3', "11001011100" }, { '4', "11001001110" },
        { '5', "11011100100" }, { '6', "11001110100" }, { '7', "11101101110" },
        { '8', "11101001100" }, { '9', "11100101100" }, { ':', "11100100110" },
        { ';', "11101100100" }, { '<', "11100110100" }, { '=', "11100110010" },
        { '>', "11011011000" }, { '?', "11011000110" }, { '@', "11000110110" },
        { 'A', "10100011000" }, { 'B', "10001011000" }, { 'C', "10001000110" },
        { 'D', "10110001000" }, { 'E', "10001101000" }, { 'F', "10001100010" },
        { 'G', "11010001000" }, { 'H', "11000101000" }, { 'I', "11000100010" },
        { 'J', "10110111000" }, { 'K', "10110001110" }, { 'L', "10001101110" },
        { 'M', "10111011000" }, { 'N', "10111000110" }, { 'O', "10001110110" },
        { 'P', "11101110110" }, { 'Q', "11010001110" }, { 'R', "11000101110" },
        { 'S', "11011101000" }, { 'T', "11011100010" }, { 'U', "11011101110" },
        { 'V', "11101011000" }, { 'W', "11101000110" }, { 'X', "11100010110" },
        { 'Y', "11101101000" }, { 'Z', "11101100010" }, { '[', "11100011010" },
        { '\\', "11101111010" }, { ']', "11001000010" }, { '^', "11110001010" },
        { '_', "10100110000" }, { '`', "10100001100" }, { 'a', "10010110000" },
        { 'b', "10010000110" }, { 'c', "10000101100" }, { 'd', "10000100110" },
        { 'e', "10110010000" }, { 'f', "10110000100" }, { 'g', "10011010000" },
        { 'h', "10011000010" }, { 'i', "10000110100" }, { 'j', "10000110010" },
        { 'k', "11000010010" }, { 'l', "11001010000" }, { 'm', "11110111010" },
        { 'n', "11000010100" }, { 'o', "10001111010" }, { 'p', "10100111100" },
        { 'q', "10010111100" }, { 'r', "10010011110" }, { 's', "10111100100" },
        { 't', "10011110100" }, { 'u', "10011110010" }, { 'v', "11110100100" },
        { 'w', "11110010100" }, { 'x', "11110010010" }, { 'y', "11011011110" },
        { 'z', "11011110110" }, { '{', "11110110110" }, { '|', "10101111000" },
        { '}', "10100011110" }, { '~', "10001011110" }
    };

    /// <summary>
    /// Code 128 start pattern for code set B.
    /// </summary>
    private const string Code128StartB = "11010010000";

    /// <summary>
    /// Code 128 stop pattern.
    /// </summary>
    private const string Code128Stop = "1100011101011";

    /// <summary>
    /// Encodes the specified data into a Code128B bar pattern string.
    /// </summary>
    /// <param name="data">The ASCII text to encode (characters 32-126).</param>
    /// <returns>A string of '0' and '1' characters representing the barcode pattern.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="data"/> is null/empty or contains unsupported characters.
    /// </exception>
    public static string Encode(string data)
    {
        if (string.IsNullOrEmpty(data))
        {
            throw new ArgumentException("Barcode data cannot be empty.", nameof(data));
        }

        foreach (var c in data)
        {
            if (!Code128BPatterns.ContainsKey(c))
            {
                throw new ArgumentException(
                    $"Character '{c}' (ASCII {(int)c}) is not supported in Code 128B. Supported range: ASCII 32-126.",
                    nameof(data));
            }
        }

        var patternBuilder = new StringBuilder(Code128StartB);

        var checksum = 104; // Start B code value
        var position = 1;

        foreach (var c in data)
        {
            patternBuilder.Append(Code128BPatterns[c]);
            var codeValue = c - 32;
            checksum += codeValue * position;
            position++;
        }

        checksum %= 103;
        var checksumChar = (char)(checksum + 32);
        if (Code128BPatterns.TryGetValue(checksumChar, out var checksumPattern))
        {
            patternBuilder.Append(checksumPattern);
        }

        patternBuilder.Append(Code128Stop);
        return patternBuilder.ToString();
    }

    /// <summary>
    /// Validates whether all characters in the data are valid Code128B characters.
    /// </summary>
    /// <param name="data">The data to validate.</param>
    /// <returns><c>true</c> if all characters are valid Code128B characters; otherwise <c>false</c>.</returns>
    public static bool IsValidCode128B(string data)
    {
        if (string.IsNullOrEmpty(data))
        {
            return false;
        }

        foreach (var c in data)
        {
            if (!Code128BPatterns.ContainsKey(c))
            {
                return false;
            }
        }

        return true;
    }
}
```

Add to `FlexRender.slnx`:

```xml
<Project Path="src/FlexRender.Barcode.Core/FlexRender.Barcode.Core.csproj" />
```

Add project reference to test project `tests/FlexRender.Tests/FlexRender.Tests.csproj`:

```xml
<ProjectReference Include="..\..\src\FlexRender.Barcode.Core\FlexRender.Barcode.Core.csproj" />
```

**Step 4: Run test (expect success)**

```bash
cd /Users/robonet/Projects/SkiaLayout && dotnet test tests/FlexRender.Tests --filter "Code128EncoderTests" --no-restore
```

Expected: PASS

**Step 5: Commit**

```bash
cd /Users/robonet/Projects/SkiaLayout && git add -A && git commit -m "feat(barcode-core): extract Code128Encoder to FlexRender.Barcode.Core"
```

---

### Task 11: Create FlexRender.QrCode.Core with QrEncoder and QrSvgPathBuilder

Extract QR encoding logic, data validation, and SVG path building from QrProvider into a shared project.

**Files:**
- Create: `src/FlexRender.QrCode.Core/FlexRender.QrCode.Core.csproj`
- Create: `src/FlexRender.QrCode.Core/QrEncoder.cs`
- Create: `src/FlexRender.QrCode.Core/QrDataValidator.cs`
- Create: `src/FlexRender.QrCode.Core/QrSvgPathBuilder.cs`
- Create: `tests/FlexRender.Tests/Encoding/QrEncoderTests.cs`
- Modify: `FlexRender.slnx`

**Step 1: Write failing test**

Create `tests/FlexRender.Tests/Encoding/QrEncoderTests.cs`:

```csharp
using FlexRender.QrCode.Core;
using FlexRender.Parsing.Ast;
using Xunit;

namespace FlexRender.Tests.Encoding;

/// <summary>
/// Tests for QR encoding core logic.
/// </summary>
public sealed class QrEncoderTests
{
    [Fact]
    public void Encode_SimpleData_ReturnsModuleMatrix()
    {
        var result = QrEncoder.Encode("Hello", ErrorCorrectionLevel.M);

        Assert.NotNull(result.ModuleMatrix);
        Assert.True(result.ModuleCount > 0);
        Assert.Equal(result.ModuleCount, result.ModuleMatrix.Count);
    }

    [Theory]
    [InlineData(ErrorCorrectionLevel.L)]
    [InlineData(ErrorCorrectionLevel.M)]
    [InlineData(ErrorCorrectionLevel.Q)]
    [InlineData(ErrorCorrectionLevel.H)]
    public void Encode_AllEccLevels_Succeeds(ErrorCorrectionLevel level)
    {
        var result = QrEncoder.Encode("Test", level);
        Assert.True(result.ModuleCount > 0);
    }

    [Fact]
    public void ValidateDataCapacity_WithinCapacity_DoesNotThrow()
    {
        var exception = Record.Exception(() =>
            QrDataValidator.ValidateDataCapacity("Hello", ErrorCorrectionLevel.M));

        Assert.Null(exception);
    }

    [Fact]
    public void ValidateDataCapacity_ExceedsCapacity_ThrowsArgumentException()
    {
        var longData = new string('A', 3000);

        Assert.Throws<ArgumentException>(() =>
            QrDataValidator.ValidateDataCapacity(longData, ErrorCorrectionLevel.L));
    }

    [Fact]
    public void BuildSvgPathData_ReturnsPathCommands()
    {
        var result = QrEncoder.Encode("Hello", ErrorCorrectionLevel.M);
        var pathData = QrSvgPathBuilder.BuildPathData(
            result.ModuleMatrix, result.ModuleCount, 10f, 10f);

        Assert.Contains("M", pathData);
        Assert.Contains("h", pathData);
        Assert.Contains("v", pathData);
        Assert.Contains("z", pathData);
    }
}
```

**Step 2: Run test (expect failure)**

```bash
cd /Users/robonet/Projects/SkiaLayout && dotnet test tests/FlexRender.Tests --filter "QrEncoderTests" --no-restore
```

Expected: FAIL

**Step 3: Implement**

Create `src/FlexRender.QrCode.Core/FlexRender.QrCode.Core.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <PackageId>FlexRender.QrCode.Core</PackageId>
    <Description>Backend-neutral QR code encoding logic for FlexRender. No rendering dependencies.</Description>
    <IsAotCompatible>true</IsAotCompatible>
  </PropertyGroup>

  <ItemGroup>
    <InternalsVisibleTo Include="FlexRender.Tests" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\FlexRender.Core\FlexRender.Core.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="QRCoder" />
  </ItemGroup>

</Project>
```

Create `src/FlexRender.QrCode.Core/QrEncoder.cs`:

```csharp
using FlexRender.Parsing.Ast;
using QRCoder;

namespace FlexRender.QrCode.Core;

/// <summary>
/// Encodes data into a QR code module matrix using QRCoder.
/// </summary>
public static class QrEncoder
{
    /// <summary>
    /// Encodes the specified data into a QR code module matrix.
    /// </summary>
    /// <param name="data">The data to encode.</param>
    /// <param name="errorCorrectionLevel">The error correction level.</param>
    /// <returns>A <see cref="QrEncodeResult"/> containing the module matrix and count.</returns>
    /// <exception cref="ArgumentException">Thrown when data is null or empty.</exception>
    public static QrEncodeResult Encode(string data, ErrorCorrectionLevel errorCorrectionLevel)
    {
        if (string.IsNullOrEmpty(data))
        {
            throw new ArgumentException("QR code data cannot be empty.", nameof(data));
        }

        QrDataValidator.ValidateDataCapacity(data, errorCorrectionLevel);

        var eccLevel = MapEccLevel(errorCorrectionLevel);

        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(data, eccLevel);

        var moduleCount = qrCodeData.ModuleMatrix.Count;

        // Copy the module matrix to avoid holding onto QRCodeData
        var matrix = new List<bool[]>(moduleCount);
        for (var i = 0; i < moduleCount; i++)
        {
            var row = new bool[moduleCount];
            Array.Copy(qrCodeData.ModuleMatrix[i], row, moduleCount);
            matrix.Add(row);
        }

        return new QrEncodeResult(matrix, moduleCount);
    }

    /// <summary>
    /// Maps the FlexRender error correction level to QRCoder's enum.
    /// </summary>
    internal static QRCodeGenerator.ECCLevel MapEccLevel(ErrorCorrectionLevel level)
    {
        return level switch
        {
            ErrorCorrectionLevel.L => QRCodeGenerator.ECCLevel.L,
            ErrorCorrectionLevel.M => QRCodeGenerator.ECCLevel.M,
            ErrorCorrectionLevel.Q => QRCodeGenerator.ECCLevel.Q,
            ErrorCorrectionLevel.H => QRCodeGenerator.ECCLevel.H,
            _ => QRCodeGenerator.ECCLevel.M
        };
    }
}

/// <summary>
/// Result of QR code encoding containing the module matrix and dimensions.
/// </summary>
/// <param name="ModuleMatrix">The QR code module matrix (true = dark module).</param>
/// <param name="ModuleCount">The number of modules per side.</param>
public sealed record QrEncodeResult(IReadOnlyList<bool[]> ModuleMatrix, int ModuleCount);
```

Create `src/FlexRender.QrCode.Core/QrDataValidator.cs`:

```csharp
using System.Text;
using FlexRender.Parsing.Ast;

namespace FlexRender.QrCode.Core;

/// <summary>
/// Validates QR code data against capacity limits for each error correction level.
/// </summary>
public static class QrDataValidator
{
    /// <summary>
    /// Maximum data capacity in bytes for each error correction level.
    /// </summary>
    private static readonly Dictionary<ErrorCorrectionLevel, int> MaxDataCapacity = new()
    {
        { ErrorCorrectionLevel.L, 2953 },
        { ErrorCorrectionLevel.M, 2331 },
        { ErrorCorrectionLevel.Q, 1663 },
        { ErrorCorrectionLevel.H, 1273 }
    };

    /// <summary>
    /// Validates that the data fits within the QR code capacity for the given error correction level.
    /// </summary>
    /// <param name="data">The data to validate.</param>
    /// <param name="errorCorrectionLevel">The error correction level.</param>
    /// <exception cref="ArgumentException">Thrown when data exceeds capacity.</exception>
    public static void ValidateDataCapacity(string data, ErrorCorrectionLevel errorCorrectionLevel)
    {
        var dataBytes = Encoding.UTF8.GetByteCount(data);
        var maxCapacity = MaxDataCapacity[errorCorrectionLevel];
        if (dataBytes > maxCapacity)
        {
            throw new ArgumentException(
                $"QR code data ({dataBytes} bytes) exceeds maximum capacity for error correction level " +
                $"{errorCorrectionLevel} ({maxCapacity} bytes).",
                nameof(data));
        }
    }
}
```

Create `src/FlexRender.QrCode.Core/QrSvgPathBuilder.cs`:

```csharp
using System.Globalization;
using System.Text;

namespace FlexRender.QrCode.Core;

/// <summary>
/// Builds optimized SVG path data from a QR code module matrix.
/// Uses horizontal run-length encoding for compact output.
/// </summary>
public static class QrSvgPathBuilder
{
    /// <summary>
    /// Builds SVG path data from a module matrix using horizontal run-length encoding.
    /// </summary>
    /// <param name="moduleMatrix">The QR code module matrix.</param>
    /// <param name="moduleCount">The number of modules per side.</param>
    /// <param name="moduleWidth">The width of each module in SVG user units.</param>
    /// <param name="moduleHeight">The height of each module in SVG user units.</param>
    /// <returns>SVG path data string with M, h, v, z commands.</returns>
    public static string BuildPathData(
        IReadOnlyList<bool[]> moduleMatrix,
        int moduleCount,
        float moduleWidth,
        float moduleHeight)
    {
        ArgumentNullException.ThrowIfNull(moduleMatrix);

        var sb = new StringBuilder(moduleCount * moduleCount / 2);

        for (var row = 0; row < moduleCount; row++)
        {
            var col = 0;
            while (col < moduleCount)
            {
                if (!moduleMatrix[row][col])
                {
                    col++;
                    continue;
                }

                var runStart = col;
                while (col < moduleCount && moduleMatrix[row][col])
                {
                    col++;
                }

                var runLength = col - runStart;

                var x = runStart * moduleWidth;
                var y = row * moduleHeight;
                var w = runLength * moduleWidth;

                sb.Append('M').Append(F(x)).Append(' ').Append(F(y));
                sb.Append('h').Append(F(w));
                sb.Append('v').Append(F(moduleHeight));
                sb.Append('h').Append(F(-w));
                sb.Append('z');
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Escapes XML special characters in attribute values.
    /// </summary>
    public static string EscapeXml(string value)
    {
        if (value.AsSpan().IndexOfAny("&<>\"'") < 0)
            return value;
        return value.Replace("&", "&amp;")
                    .Replace("<", "&lt;")
                    .Replace(">", "&gt;")
                    .Replace("\"", "&quot;")
                    .Replace("'", "&apos;");
    }

    /// <summary>
    /// Formats a float using invariant culture with no trailing zeros.
    /// </summary>
    public static string F(float value)
    {
        return value.ToString("G", CultureInfo.InvariantCulture);
    }
}
```

Add to `FlexRender.slnx`, test project references, etc.

**Step 4: Run test (expect success)**

```bash
cd /Users/robonet/Projects/SkiaLayout && dotnet test tests/FlexRender.Tests --filter "QrEncoderTests" --no-restore
```

Expected: PASS

**Step 5: Commit**

```bash
cd /Users/robonet/Projects/SkiaLayout && git add -A && git commit -m "feat(qr-core): extract QrEncoder, QrDataValidator, QrSvgPathBuilder to FlexRender.QrCode.Core"
```

---

### Task 12: Update Existing Providers to Use Core Encoding Projects

Update `BarcodeProvider` to delegate to `Code128Encoder` and `QrProvider` to delegate to `QrEncoder`/`QrSvgPathBuilder`. Delete the duplicated code from `FlexRender.ImageSharp`.

**Files:**
- Modify: `src/FlexRender.Barcode/FlexRender.Barcode.csproj` (add Barcode.Core dependency)
- Modify: `src/FlexRender.Barcode/Providers/BarcodeProvider.cs`
- Modify: `src/FlexRender.QrCode/FlexRender.QrCode.csproj` (add QrCode.Core dependency)
- Modify: `src/FlexRender.QrCode/Providers/QrProvider.cs`

**Step 1: Update BarcodeProvider to use Code128Encoder**

In `src/FlexRender.Barcode/FlexRender.Barcode.csproj`, add:
```xml
<ProjectReference Include="..\FlexRender.Barcode.Core\FlexRender.Barcode.Core.csproj" />
```

In `BarcodeProvider.cs`, replace the inline `Code128BPatterns` dictionary, `Code128StartB`, `Code128Stop`, checksum calculation, and character validation with a single call to `Code128Encoder.Encode(element.Data)`. Delete those members from BarcodeProvider.

**Step 2: Update QrProvider to use QrEncoder and QrSvgPathBuilder**

In `src/FlexRender.QrCode/FlexRender.QrCode.csproj`, add:
```xml
<ProjectReference Include="..\FlexRender.QrCode.Core\FlexRender.QrCode.Core.csproj" />
```

In `QrProvider.cs`, replace the inline `MaxDataCapacity`, `MapEccLevel`, `ValidateDataCapacity`, `BuildPathData`, `EscapeXml`, and `F` methods with calls to `QrEncoder`, `QrDataValidator`, `QrSvgPathBuilder`.

**Step 3: Run all tests**

```bash
cd /Users/robonet/Projects/SkiaLayout && dotnet test FlexRender.slnx
```

Expected: ALL tests pass.

**Step 4: Commit**

```bash
cd /Users/robonet/Projects/SkiaLayout && git add -A && git commit -m "refactor: update QR and barcode providers to use shared Core encoding projects"
```

---

### Task 13: Delete Duplicated Code from FlexRender.ImageSharp

Now that encoding logic lives in Core projects, update the ImageSharp providers to use them, then delete the duplicated encoding tables.

**Files:**
- Modify: `src/FlexRender.ImageSharp/FlexRender.ImageSharp.csproj` (add Core project dependencies, remove QRCoder)
- Modify: `src/FlexRender.ImageSharp/Providers/ImageSharpQrProvider.cs`
- Modify: `src/FlexRender.ImageSharp/Providers/ImageSharpBarcodeProvider.cs`
- Modify: `src/FlexRender.ImageSharp/Rendering/ImageSharpRenderingEngine.cs`

**Step 1: Update ImageSharp project**

In `FlexRender.ImageSharp.csproj`, add:
```xml
<ProjectReference Include="..\FlexRender.QrCode.Core\FlexRender.QrCode.Core.csproj" />
<ProjectReference Include="..\FlexRender.Barcode.Core\FlexRender.Barcode.Core.csproj" />
```

Remove:
```xml
<PackageReference Include="QRCoder" />
```

(QRCoder is now transitively referenced through QrCode.Core.)

**Step 2: Update ImageSharpQrProvider to use QrEncoder**

Replace the inline `MaxDataCapacity`, `MapEccLevel`, `ValidateDataCapacity` with calls to `QrEncoder.Encode()` and `QrDataValidator`. The QR module matrix iteration stays but uses `QrEncodeResult.ModuleMatrix` instead of `QRCodeData.ModuleMatrix` directly.

**Step 3: Update ImageSharpBarcodeProvider to use Code128Encoder**

Replace the inline `Code128BPatterns`, `Code128StartB`, `Code128Stop`, character validation, and checksum logic with a single `Code128Encoder.Encode(element.Data)` call. The rendering (drawing bars using the pattern string) stays in ImageSharpBarcodeProvider.

**Step 4: Run all tests**

```bash
cd /Users/robonet/Projects/SkiaLayout && dotnet test FlexRender.slnx
```

Expected: ALL tests pass.

**Step 5: Commit**

```bash
cd /Users/robonet/Projects/SkiaLayout && git add -A && git commit -m "refactor(imagesharp): eliminate duplicated encoding tables, use Core projects"
```

---

## Phase 3: Update Solution Structure and CLI

### Task 14: Update Solution File and MetaPackage

**Files:**
- Modify: `FlexRender.slnx`
- Modify: `src/FlexRender.MetaPackage/FlexRender.MetaPackage.csproj`
- Modify: `src/FlexRender.Cli/FlexRender.Cli.csproj`

**Step 1: Update solution file**

Ensure `FlexRender.slnx` includes all new projects:
- `src/FlexRender.Barcode.Core/FlexRender.Barcode.Core.csproj`
- `src/FlexRender.QrCode.Core/FlexRender.QrCode.Core.csproj`

**Step 2: Update MetaPackage**

Add Core project references to MetaPackage:
```xml
<ProjectReference Include="..\FlexRender.QrCode.Core\FlexRender.QrCode.Core.csproj" />
<ProjectReference Include="..\FlexRender.Barcode.Core\FlexRender.Barcode.Core.csproj" />
```

**Step 3: Update CLI project references**

Ensure CLI has references to all needed projects. Add Core projects:
```xml
<ProjectReference Include="..\FlexRender.QrCode.Core\FlexRender.QrCode.Core.csproj" />
<ProjectReference Include="..\FlexRender.Barcode.Core\FlexRender.Barcode.Core.csproj" />
```

**Step 4: Build and test**

```bash
cd /Users/robonet/Projects/SkiaLayout && dotnet build FlexRender.slnx && dotnet test FlexRender.slnx
```

Expected: ALL pass.

**Step 5: Commit**

```bash
cd /Users/robonet/Projects/SkiaLayout && git add -A && git commit -m "build: update solution, metapackage, and CLI with Core project references"
```

---

### Task 15: Add InternalsVisibleTo for New Projects and Final Cleanup

**Files:**
- Modify: `src/FlexRender.Core/FlexRender.Core.csproj`
- Modify: `src/FlexRender.Skia/FlexRender.Skia.csproj`

**Step 1: Add InternalsVisibleTo entries**

In `FlexRender.Core.csproj`, add any new projects that need internal access:
```xml
<InternalsVisibleTo Include="FlexRender.Barcode.Core" />
<InternalsVisibleTo Include="FlexRender.QrCode.Core" />
```

In `FlexRender.Skia.csproj`, ensure `InternalsVisibleTo` includes all Skia-dependent projects.

**Step 2: Build and run all tests**

```bash
cd /Users/robonet/Projects/SkiaLayout && dotnet build FlexRender.slnx && dotnet test FlexRender.slnx
```

Expected: ALL pass.

**Step 3: Commit**

```bash
cd /Users/robonet/Projects/SkiaLayout && git add -A && git commit -m "build: update InternalsVisibleTo for provider restructuring"
```

---

## Phase 4: Integration Verification

### Task 16: Full Integration Test

Run the entire test suite and verify the CLI works.

**Step 1: Run all tests**

```bash
cd /Users/robonet/Projects/SkiaLayout && dotnet test FlexRender.slnx -v normal
```

Expected: ALL tests pass. The test count should be the same or higher than before restructuring.

**Step 2: Verify CLI renders correctly**

```bash
cd /Users/robonet/Projects/SkiaLayout/examples && dotnet run --project ../src/FlexRender.Cli -- render receipt.yaml -d receipt-data.json -o /tmp/restructuring-test-receipt.png
```

```bash
cd /Users/robonet/Projects/SkiaLayout/examples && dotnet run --project ../src/FlexRender.Cli -- render receipt.yaml -d receipt-data.json -o /tmp/restructuring-test-receipt.svg -f svg
```

Expected: Both files are generated without errors.

**Step 3: Verify dependency graph**

Verify `FlexRender.Svg` no longer depends on `FlexRender.Skia`:

```bash
cd /Users/robonet/Projects/SkiaLayout && dotnet list src/FlexRender.Svg/FlexRender.Svg.csproj reference
```

Expected: Only `FlexRender.Core` listed, NOT `FlexRender.Skia`.

**Step 4: Commit any final fixes**

```bash
cd /Users/robonet/Projects/SkiaLayout && git add -A && git commit -m "test: verify provider restructuring integration"
```

---

## Summary of Changes

### New Files Created
| File | Purpose |
|------|---------|
| `src/FlexRender.Core/Providers/ContentResult.cs` | Backend-neutral PNG bytes container |
| `src/FlexRender.Core/Providers/IContentProvider.cs` | New interface returning ContentResult |
| `src/FlexRender.Core/Providers/ISvgContentProvider.cs` | Moved from Skia (unchanged) |
| `src/FlexRender.Core/Providers/IResourceLoaderAware.cs` | Moved from Skia (unchanged) |
| `src/FlexRender.Skia/Providers/ISkiaNativeProvider.cs` | Zero-copy SKBitmap interface for Skia |
| `src/FlexRender.Skia/SvgBuilderSkiaExtensions.cs` | SvgBuilder.WithSkia() extension (moved from SvgBuilder) |
| `src/FlexRender.Barcode.Core/FlexRender.Barcode.Core.csproj` | New project for barcode encoding |
| `src/FlexRender.Barcode.Core/Code128Encoder.cs` | Shared Code128 encoding logic |
| `src/FlexRender.QrCode.Core/FlexRender.QrCode.Core.csproj` | New project for QR encoding |
| `src/FlexRender.QrCode.Core/QrEncoder.cs` | Shared QR encoding via QRCoder |
| `src/FlexRender.QrCode.Core/QrDataValidator.cs` | Shared QR capacity validation |
| `src/FlexRender.QrCode.Core/QrSvgPathBuilder.cs` | Shared SVG path generation |

### Files Deleted
| File | Reason |
|------|--------|
| `src/FlexRender.Skia/Providers/IContentProvider.cs` | Moved to Core |
| `src/FlexRender.Skia/Providers/ISvgContentProvider.cs` | Moved to Core |
| `src/FlexRender.Skia/Providers/IResourceLoaderAware.cs` | Moved to Core |

### Files Modified
| File | Changes |
|------|---------|
| `src/FlexRender.QrCode/Providers/QrProvider.cs` | New interface impl, delegates to QrCode.Core |
| `src/FlexRender.Barcode/Providers/BarcodeProvider.cs` | New interface impl, delegates to Barcode.Core |
| `src/FlexRender.SvgElement/Providers/SvgElementProvider.cs` | New interface impl |
| `src/FlexRender.Skia/Rendering/RenderingEngine.cs` | ISkiaNativeProvider dispatch |
| `src/FlexRender.Skia/Rendering/SkiaRenderer.cs` | Constructor param types |
| `src/FlexRender.Svg/Rendering/SvgRenderingEngine.cs` | Remove SkiaSharp, use ContentResult |
| `src/FlexRender.Svg/SvgRender.cs` | New constructor with provider slots |
| `src/FlexRender.Svg/SvgBuilder.cs` | Provider slots, remove WithSkia |
| `src/FlexRender.Svg/SvgBuilderExtensions.cs` | Wire provider slots |
| `src/FlexRender.Svg/FlexRender.Svg.csproj` | Remove Skia dependency |
| `src/FlexRender.ImageSharp/Providers/ImageSharpQrProvider.cs` | Use QrEncoder |
| `src/FlexRender.ImageSharp/Providers/ImageSharpBarcodeProvider.cs` | Use Code128Encoder |
| `src/FlexRender.ImageSharp/FlexRender.ImageSharp.csproj` | Add Core deps, remove QRCoder |

### Test Files
| File | Changes |
|------|---------|
| `tests/FlexRender.Tests/Providers/ContentResultTests.cs` | NEW |
| `tests/FlexRender.Tests/Providers/SkiaNativeProviderInterfaceTests.cs` | NEW |
| `tests/FlexRender.Tests/Encoding/Code128EncoderTests.cs` | NEW |
| `tests/FlexRender.Tests/Encoding/QrEncoderTests.cs` | NEW |
| `tests/FlexRender.Tests/Rendering/SvgBuilderTests.cs` | NEW |
| `tests/FlexRender.Tests/Providers/QrProviderTests.cs` | Updated for new interfaces |
| `tests/FlexRender.Tests/Providers/BarcodeProviderTests.cs` | Updated for new interfaces |
| `tests/FlexRender.Tests/Providers/SvgElementProviderTests.cs` | Updated for new interfaces |

### Dependency Graph Change
```
BEFORE: FlexRender.Svg --> FlexRender.Skia --> FlexRender.Core
AFTER:  FlexRender.Svg --> FlexRender.Core (direct, no Skia)
```

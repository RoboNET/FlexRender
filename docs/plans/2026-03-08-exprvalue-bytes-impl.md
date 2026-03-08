# ExprValue BytesValue Unification — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Store resolved BytesValue directly in ExprValue<T> so images, SVGs, and content elements get binary data without base64 round-trip. Load resources via IResourceLoader chain during template expansion.

**Architecture:** Add `BytesValue? Bytes` property to `ExprValue<T>`. TemplateExpander resolves `{{variable}}` → BytesValue AND loads URI sources (data:, file:, http://) via loader chain, storing results in `ExprValue.Bytes`. Downstream consumers (ImageProvider, ContentSourceResolver, SvgProvider) check `Bytes` first, skip loader chain if present. PreloadImagesAsync/PreloadSvgContentAsync check `Bytes` too, skipping re-loading.

**Tech Stack:** C# / .NET, xUnit

**Design doc:** `docs/plans/2026-03-08-exprvalue-bytes-design.md`

---

### Task 1: Add BytesValue property to ExprValue

**Files:**
- Modify: `src/FlexRender.Core/Parsing/Ast/ExprValue.cs`
- Test: `tests/FlexRender.Tests/Parsing/Ast/ExprValueTests.cs`

**Step 1: Write the failing test**

Add to `ExprValueTests.cs`:

```csharp
[Fact]
public void WithBytes_StoresBytesValue()
{
    ExprValue<string> v = "logo.png";
    var bytes = new BytesValue(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, "image/png");
    var withBytes = v.WithBytes(bytes);

    Assert.Equal("logo.png", withBytes.Value);
    Assert.NotNull(withBytes.Bytes);
    Assert.Equal(bytes, withBytes.Bytes);
}

[Fact]
public void WithBytes_PreservesAllProperties()
{
    var v = ExprValue<string>.Expression("{{logo}}");
    var bytes = new BytesValue([0x01, 0x02], "image/png");
    var withBytes = v.WithBytes(bytes);

    Assert.True(withBytes.IsExpression);
    Assert.Equal("{{logo}}", withBytes.RawValue);
    Assert.Same(bytes, withBytes.Bytes);
}

[Fact]
public void Default_HasNullBytes()
{
    var v = default(ExprValue<string>);
    Assert.Null(v.Bytes);
}

[Fact]
public void ImplicitConversion_HasNullBytes()
{
    ExprValue<string> v = "hello";
    Assert.Null(v.Bytes);
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/FlexRender.Tests/FlexRender.Tests.csproj --filter "FullyQualifiedName~ExprValueTests.WithBytes" --no-restore -v minimal`
Expected: FAIL — `WithBytes` method and `Bytes` property don't exist

**Step 3: Write minimal implementation**

In `ExprValue.cs`, add the property and method:

```csharp
/// <summary>
/// Gets the resolved binary data when the source expression resolved to a <see cref="FlexRender.BytesValue"/>.
/// Allows binary data to flow through the pipeline without base64 encoding/decoding overhead.
/// </summary>
public BytesValue? Bytes { get; private init; }

/// <summary>
/// Creates a copy of this <see cref="ExprValue{T}"/> with the specified <see cref="BytesValue"/> attached.
/// Used by TemplateExpander when a template variable resolves to binary data or when
/// a resource loader returns binary content.
/// </summary>
/// <param name="bytes">The binary data to attach.</param>
/// <returns>A new <see cref="ExprValue{T}"/> with Bytes set.</returns>
public ExprValue<T> WithBytes(BytesValue bytes)
{
    return this with { Bytes = bytes };
}
```

**Step 4: Run test to verify it passes**

Run: `dotnet test tests/FlexRender.Tests/FlexRender.Tests.csproj --filter "FullyQualifiedName~ExprValueTests" --no-restore -v minimal`
Expected: ALL PASS

**Step 5: Commit**

```bash
git add src/FlexRender.Core/Parsing/Ast/ExprValue.cs tests/FlexRender.Tests/Parsing/Ast/ExprValueTests.cs
git commit -m "feat: add BytesValue property to ExprValue"
```

---

### Task 2: TemplateExpander — resolve BytesValue from variables into ExprValue.Bytes

**Files:**
- Modify: `src/FlexRender.Core/TemplateEngine/TemplateExpander.cs`
- Test: `tests/FlexRender.Tests/TemplateEngine/ExpandContentBinaryTests.cs` (existing)

**Step 1: Write the failing test**

Add to `ExpandContentBinaryTests.cs`:

```csharp
[Fact]
public async Task ExpandContent_BytesValue_StoredInExprValueBytes()
{
    // Arrange: ContentElement with {{payload}} that resolves to BytesValue
    var binaryParser = new CapturingBinaryParser("ndc");
    var registry = new ContentParserRegistry();
    registry.RegisterBinary(binaryParser);

    var expander = new TemplateExpander(new ResourceLimits(), contentParserRegistry: registry);

    var rawBytes = new byte[] { 0x1B, 0x40 };
    var template = CreateTemplate(
        new ContentElement { Source = "{{payload}}", Format = "ndc" });

    var data = new ObjectValue { ["payload"] = new BytesValue(rawBytes) };
    var result = await expander.ExpandAsync(template, data);

    // Assert: binary parser received the bytes (meaning ExprValue.Bytes was used)
    Assert.NotNull(binaryParser.LastData);
    Assert.Equal(rawBytes, binaryParser.LastData);
}
```

This test already exists as `ExpandContent_BytesValue_DispatchesToBinaryParser` and passes. The real validation is that after refactoring, this test still passes. Write a new test for images:

Create new test file `tests/FlexRender.Tests/TemplateEngine/ExpandImageBytesTests.cs`:

```csharp
using FlexRender.Parsing.Ast;
using FlexRender.TemplateEngine;
using Xunit;

namespace FlexRender.Tests.TemplateEngine;

public sealed class ExpandImageBytesTests
{
    [Fact]
    public async Task ExpandAsync_ImageWithBytesVariable_StoresBytesInExprValue()
    {
        var expander = new TemplateExpander(new ResourceLimits());
        var rawBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        var template = new Template
        {
            Name = "test",
            Version = 1,
            Canvas = new CanvasSettings { Width = 200 },
            Elements =
            [
                new ImageElement { Src = ExprValue<string>.Expression("{{logo}}") }
            ]
        };

        var data = new ObjectValue { ["logo"] = new BytesValue(rawBytes, "image/png") };
        var result = await expander.ExpandAsync(template, data);

        var image = Assert.IsType<ImageElement>(result.Elements[0]);
        Assert.NotNull(image.Src.Bytes);
        Assert.Equal(rawBytes, image.Src.Bytes!.Value);
        Assert.Equal("image/png", image.Src.Bytes.MimeType);
    }

    [Fact]
    public async Task ExpandAsync_ImageWithStringVariable_NoBytesInExprValue()
    {
        var expander = new TemplateExpander(new ResourceLimits());
        var template = new Template
        {
            Name = "test",
            Version = 1,
            Canvas = new CanvasSettings { Width = 200 },
            Elements =
            [
                new ImageElement { Src = ExprValue<string>.Expression("{{logoUrl}}") }
            ]
        };

        var data = new ObjectValue { ["logoUrl"] = new StringValue("logo.png") };
        var result = await expander.ExpandAsync(template, data);

        var image = Assert.IsType<ImageElement>(result.Elements[0]);
        Assert.Null(image.Src.Bytes);
        Assert.Equal("logo.png", image.Src.Value);
    }

    [Fact]
    public async Task ExpandAsync_SvgWithBytesVariable_StoresBytesInExprValue()
    {
        var expander = new TemplateExpander(new ResourceLimits());
        var svgBytes = System.Text.Encoding.UTF8.GetBytes("<svg></svg>");
        var template = new Template
        {
            Name = "test",
            Version = 1,
            Canvas = new CanvasSettings { Width = 200 },
            Elements =
            [
                new SvgElement { Src = ExprValue<string>.Expression("{{icon}}") }
            ]
        };

        var data = new ObjectValue { ["icon"] = new BytesValue(svgBytes, "image/svg+xml") };
        var result = await expander.ExpandAsync(template, data);

        var svg = Assert.IsType<SvgElement>(result.Elements[0]);
        Assert.NotNull(svg.Src.Bytes);
        Assert.Equal(svgBytes, svg.Src.Bytes!.Value);
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/FlexRender.Tests/FlexRender.Tests.csproj --filter "FullyQualifiedName~ExpandImageBytesTests" --no-restore -v minimal`
Expected: FAIL — `image.Src.Bytes` is null (BytesValue not stored yet)

**Step 3: Implement — add TryResolveBytesValue helper, use in CloneImageElement/CloneSvgElement**

In `TemplateExpander.cs`:

1. Add a new helper method that resolves `{{variable}}` to `BytesValue` if possible:

```csharp
/// <summary>
/// Tries to resolve a pure {{variable}} expression to a BytesValue.
/// Returns null if the expression is mixed, nested, or resolves to a non-bytes value.
/// </summary>
private static BytesValue? TryResolveBytesValue(ExprValue<string> source, TemplateContext context)
{
    var raw = source.RawValue ?? source.Value;
    if (raw is null)
        return null;

    if (!raw.StartsWith("{{", StringComparison.Ordinal) || !raw.EndsWith("}}", StringComparison.Ordinal))
        return null;

    var inner = raw[2..^2].Trim();
    if (inner.Contains("{{", StringComparison.Ordinal))
        return null;

    var resolved = ExpressionEvaluator.Resolve(inner, context);
    return resolved as BytesValue;
}
```

2. Update `CloneImageElement` — replace `TryResolveBytesAsDataUri` with `TryResolveBytesValue`:

```csharp
private ImageElement CloneImageElement(ImageElement image, TemplateContext context)
{
    var bytesValue = TryResolveBytesValue(image.Src, context);
    var resolvedSrc = bytesValue is not null
        ? image.Src.Value ?? ""  // Keep original src text, bytes are in ExprValue.Bytes
        : SubstituteVariables(image.Src.Value, context);

    var srcExpr = new ExprValue<string>(resolvedSrc ?? "");
    if (bytesValue is not null)
        srcExpr = srcExpr.WithBytes(bytesValue);

    var clone = new ImageElement
    {
        Src = srcExpr,
        ImageWidth = image.ImageWidth,
        ImageHeight = image.ImageHeight,
        Fit = image.Fit,
        Rotate = image.Rotate,
        Background = SubstituteVariables(image.Background.Value, context),
        Padding = image.Padding,
        Margin = image.Margin
    };

    TemplateElement.CopyBaseProperties(image, clone);
    return clone;
}
```

3. Update `CloneSvgElement` — add BytesValue support:

```csharp
private SvgElement CloneSvgElement(SvgElement svg, TemplateContext context)
{
    var bytesValue = TryResolveBytesValue(svg.Src, context);
    var resolvedSrc = bytesValue is not null
        ? svg.Src.Value ?? ""
        : SubstituteVariables(svg.Src.Value, context);

    var srcExpr = new ExprValue<string>(resolvedSrc ?? "");
    if (bytesValue is not null)
        srcExpr = srcExpr.WithBytes(bytesValue);

    var clone = new SvgElement
    {
        Src = srcExpr,
        Content = SubstituteVariables(svg.Content.Value, context),
        SvgWidth = svg.SvgWidth,
        SvgHeight = svg.SvgHeight,
        Fit = svg.Fit,
        Rotate = svg.Rotate,
        Background = SubstituteVariables(svg.Background.Value, context),
        Padding = svg.Padding,
        Margin = svg.Margin
    };

    TemplateElement.CopyBaseProperties(svg, clone);
    return clone;
}
```

4. **Delete** `TryResolveBytesAsDataUri()` method entirely (lines 1129-1151).

**Step 4: Run tests to verify**

Run: `dotnet test tests/FlexRender.Tests/FlexRender.Tests.csproj --filter "FullyQualifiedName~ExpandImageBytesTests|FullyQualifiedName~ExpandContentBinaryTests" --no-restore -v minimal`
Expected: ALL PASS

**Step 5: Commit**

```bash
git add src/FlexRender.Core/TemplateEngine/TemplateExpander.cs tests/FlexRender.Tests/TemplateEngine/ExpandImageBytesTests.cs
git commit -m "feat: resolve BytesValue into ExprValue.Bytes in TemplateExpander"
```

---

### Task 3: ContentSourceResolver — use ExprValue.Bytes instead of TryResolveBytes

**Files:**
- Modify: `src/FlexRender.Core/TemplateEngine/ContentSourceResolver.cs`
- Test: `tests/FlexRender.Tests/TemplateEngine/ContentSourceResolverTests.cs` (existing)

**Step 1: Write the failing test**

Add to `ContentSourceResolverTests.cs`:

```csharp
[Fact]
public async Task Resolve_ExprValueWithBytes_ReturnsBinaryContentDirectly()
{
    var rawBytes = new byte[] { 0x1B, 0x40, 0x48 };
    var bytes = new BytesValue(rawBytes, "application/octet-stream");
    var source = new ExprValue<string>("").WithBytes(bytes);
    var context = new TemplateContext(new ObjectValue());

    var result = await ContentSourceResolver.ResolveAsync(source, context, loaders: null);

    var binary = Assert.IsType<BinaryContent>(result);
    Assert.Equal(rawBytes, binary.Data.ToArray());
    Assert.Equal("application/octet-stream", binary.MimeType);
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/FlexRender.Tests/FlexRender.Tests.csproj --filter "FullyQualifiedName~ContentSourceResolverTests.Resolve_ExprValueWithBytes" --no-restore -v minimal`
Expected: FAIL — ContentSourceResolver doesn't check `source.Bytes` yet

**Step 3: Implement**

In `ContentSourceResolver.cs`, replace the `TryResolveBytes` call with `source.Bytes` check:

```csharp
public static async ValueTask<ContentSource> ResolveAsync(
    ExprValue<string> source,
    TemplateContext context,
    IReadOnlyList<IResourceLoader>? loaders,
    Func<string?, TemplateContext, string?>? substituteVariables = null)
{
    ArgumentNullException.ThrowIfNull(context);

    // Step 1: Check if ExprValue already carries resolved bytes
    if (source.Bytes is not null)
    {
        return new BinaryContent(source.Bytes.Memory, source.Bytes.MimeType);
    }

    // Step 2: Resolve source as string
    var rawText = source.RawValue ?? source.Value;
    var resolvedSource = substituteVariables?.Invoke(rawText, context) ?? rawText;

    if (resolvedSource is null)
    {
        return new TextContent(string.Empty);
    }

    // Step 3: "text:" prefix — always treat as text, bypass loaders
    if (resolvedSource.StartsWith("text:", StringComparison.Ordinal))
    {
        return new TextContent(resolvedSource["text:".Length..]);
    }

    // Step 4: Delegate everything to IResourceLoader chain
    if (loaders is not null)
    {
        var loaded = await TryLoadFromLoadersAsync(resolvedSource, loaders).ConfigureAwait(false);
        if (loaded is not null)
        {
            return loaded;
        }
    }

    // Step 5: No loader handled it — treat as plain text content
    return new TextContent(resolvedSource);
}
```

**Delete** `TryResolveBytes()` method entirely (lines 111-132).

**Step 4: Run all ContentSourceResolver and ExpandContentBinary tests**

Run: `dotnet test tests/FlexRender.Tests/FlexRender.Tests.csproj --filter "FullyQualifiedName~ContentSourceResolver|FullyQualifiedName~ExpandContentBinary|FullyQualifiedName~ContentElementIntegration" --no-restore -v minimal`
Expected: ALL PASS

**Step 5: Commit**

```bash
git add src/FlexRender.Core/TemplateEngine/ContentSourceResolver.cs tests/FlexRender.Tests/TemplateEngine/ContentSourceResolverTests.cs
git commit -m "refactor: ContentSourceResolver uses ExprValue.Bytes instead of TryResolveBytes"
```

---

### Task 4: TemplateExpander — load URI sources via IResourceLoader chain into ExprValue.Bytes

**Files:**
- Modify: `src/FlexRender.Core/TemplateEngine/TemplateExpander.cs`
- Test: `tests/FlexRender.Tests/TemplateEngine/ExpandImageBytesTests.cs`

This task adds resource loading during expansion: when an image/svg/content src is a URI that a loader can handle (data:, file:, etc.), load it during expansion and store in `ExprValue.Bytes`.

**Step 1: Write the failing test**

Add to `ExpandImageBytesTests.cs`:

```csharp
[Fact]
public async Task ExpandAsync_ImageWithDataUri_LoadsBytesViaLoaderChain()
{
    var options = new FlexRenderOptions();
    IReadOnlyList<IResourceLoader> loaders = [new Base64ResourceLoader(options)];
    var expander = new TemplateExpander(new ResourceLimits(), resourceLoaders: loaders);

    var rawBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
    var base64 = Convert.ToBase64String(rawBytes);

    var template = new Template
    {
        Name = "test",
        Version = 1,
        Canvas = new CanvasSettings { Width = 200 },
        Elements =
        [
            new ImageElement { Src = $"data:image/png;base64,{base64}" }
        ]
    };

    var result = await expander.ExpandAsync(template, new ObjectValue());

    var image = Assert.IsType<ImageElement>(result.Elements[0]);
    Assert.NotNull(image.Src.Bytes);
    Assert.Equal(rawBytes, image.Src.Bytes!.Value);
}

[Fact]
public async Task ExpandAsync_ImageWithPlainFilename_NoBytesWhenNoFileLoader()
{
    // No loaders registered — plain filename stays as string, no bytes
    var expander = new TemplateExpander(new ResourceLimits());

    var template = new Template
    {
        Name = "test",
        Version = 1,
        Canvas = new CanvasSettings { Width = 200 },
        Elements =
        [
            new ImageElement { Src = "logo.png" }
        ]
    };

    var result = await expander.ExpandAsync(template, new ObjectValue());

    var image = Assert.IsType<ImageElement>(result.Elements[0]);
    Assert.Null(image.Src.Bytes);
    Assert.Equal("logo.png", image.Src.Value);
}
```

Add required `using`:
```csharp
using FlexRender.Configuration;
using FlexRender.Loaders;
using FlexRender.Abstractions;
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/FlexRender.Tests/FlexRender.Tests.csproj --filter "FullyQualifiedName~ExpandImageBytesTests.ExpandAsync_ImageWithDataUri" --no-restore -v minimal`
Expected: FAIL — `image.Src.Bytes` is null (loader chain not called during expansion for images)

**Step 3: Implement — add TryLoadBytesAsync helper, use in CloneImageElement/CloneSvgElement**

In `TemplateExpander.cs`:

1. Add async helper method:

```csharp
/// <summary>
/// Tries to load binary content from a URI source via the resource loader chain.
/// Returns null if no loader can handle the source or no loaders are registered.
/// </summary>
private async ValueTask<BytesValue?> TryLoadBytesFromLoadersAsync(string? source)
{
    if (string.IsNullOrEmpty(source) || _resourceLoaders is null)
        return null;

    foreach (var loader in _resourceLoaders)
    {
        if (loader.CanHandle(source))
        {
            try
            {
                var stream = await loader.Load(source).ConfigureAwait(false);
                if (stream is not null)
                {
                    using (stream)
                    {
                        return BytesValue.FromStream(stream);
                    }
                }
            }
            catch
            {
                // Loader claimed it can handle but failed — continue to next
            }
        }
    }

    return null;
}
```

2. Make `CloneImageElement` async and use the helper:

```csharp
private async ValueTask<ImageElement> CloneImageElementAsync(ImageElement image, TemplateContext context)
{
    // First try: variable resolves to BytesValue
    var bytesValue = TryResolveBytesValue(image.Src, context);
    string? resolvedSrc;

    if (bytesValue is not null)
    {
        resolvedSrc = image.Src.Value ?? "";
    }
    else
    {
        resolvedSrc = SubstituteVariables(image.Src.Value, context);
        // Second try: resolved source is a URI that a loader can handle
        bytesValue = await TryLoadBytesFromLoadersAsync(resolvedSrc).ConfigureAwait(false);
    }

    var srcExpr = new ExprValue<string>(resolvedSrc ?? "");
    if (bytesValue is not null)
        srcExpr = srcExpr.WithBytes(bytesValue);

    var clone = new ImageElement
    {
        Src = srcExpr,
        ImageWidth = image.ImageWidth,
        ImageHeight = image.ImageHeight,
        Fit = image.Fit,
        Rotate = image.Rotate,
        Background = SubstituteVariables(image.Background.Value, context),
        Padding = image.Padding,
        Margin = image.Margin
    };

    TemplateElement.CopyBaseProperties(image, clone);
    return clone;
}
```

3. Similarly make `CloneSvgElement` async:

```csharp
private async ValueTask<SvgElement> CloneSvgElementAsync(SvgElement svg, TemplateContext context)
{
    var bytesValue = TryResolveBytesValue(svg.Src, context);
    string? resolvedSrc;

    if (bytesValue is not null)
    {
        resolvedSrc = svg.Src.Value ?? "";
    }
    else
    {
        resolvedSrc = SubstituteVariables(svg.Src.Value, context);
        bytesValue = await TryLoadBytesFromLoadersAsync(resolvedSrc).ConfigureAwait(false);
    }

    var srcExpr = new ExprValue<string>(resolvedSrc ?? "");
    if (bytesValue is not null)
        srcExpr = srcExpr.WithBytes(bytesValue);

    var clone = new SvgElement
    {
        Src = srcExpr,
        Content = SubstituteVariables(svg.Content.Value, context),
        SvgWidth = svg.SvgWidth,
        SvgHeight = svg.SvgHeight,
        Fit = svg.Fit,
        Rotate = svg.Rotate,
        Background = SubstituteVariables(svg.Background.Value, context),
        Padding = svg.Padding,
        Margin = svg.Margin
    };

    TemplateElement.CopyBaseProperties(svg, clone);
    return clone;
}
```

4. Update the call sites in the main expansion switch to use async versions. Find where `CloneImageElement` and `CloneSvgElement` are called and change to `await CloneImageElementAsync(...)` / `await CloneSvgElementAsync(...)`.

**Step 4: Run all tests**

Run: `dotnet test tests/FlexRender.Tests/FlexRender.Tests.csproj --no-restore -v minimal`
Expected: ALL PASS

**Step 5: Commit**

```bash
git add src/FlexRender.Core/TemplateEngine/TemplateExpander.cs tests/FlexRender.Tests/TemplateEngine/ExpandImageBytesTests.cs
git commit -m "feat: load URI sources into ExprValue.Bytes during template expansion"
```

---

### Task 5: ImageProvider / PreloadImagesAsync — use ExprValue.Bytes when available

**Files:**
- Modify: `src/FlexRender.Skia.Render/Rendering/RenderingEngine.cs`
- Modify: `src/FlexRender.Skia.Render/Providers/ImageProvider.cs`
- Test: `tests/FlexRender.Tests/Integration/ContentElementIntegrationTests.cs` (verify existing)

**Step 1: Update PreloadImagesFromProcessedAsync to check ExprValue.Bytes**

In `RenderingEngine.cs`, modify `PreloadImagesFromProcessedAsync` to check `image.Src.Bytes` before calling the loader:

```csharp
internal async Task<Dictionary<string, SKBitmap>> PreloadImagesFromProcessedAsync(
    Template processedTemplate,
    CancellationToken cancellationToken)
{
    if (_imageLoader is null)
        return new Dictionary<string, SKBitmap>(0, StringComparer.Ordinal);

    var cache = new Dictionary<string, SKBitmap>(StringComparer.Ordinal);

    PreloadImagesFromElements(processedTemplate.Elements, cache, cancellationToken);

    // Load remaining URIs that don't have pre-resolved bytes
    var uris = CollectImageUris(processedTemplate);
    foreach (var uri in uris)
    {
        if (cache.ContainsKey(uri))
            continue; // Already loaded from bytes

        cancellationToken.ThrowIfCancellationRequested();
        var bitmap = await _imageLoader.Load(uri, cancellationToken).ConfigureAwait(false);
        if (bitmap is not null)
        {
            cache[uri] = bitmap;
        }
    }

    return cache;
}

private static void PreloadImagesFromElements(
    IReadOnlyList<TemplateElement> elements,
    Dictionary<string, SKBitmap> cache,
    CancellationToken cancellationToken)
{
    foreach (var element in elements)
    {
        if (element is ImageElement image && image.Src.Bytes is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = image.Src.Value;
            if (!string.IsNullOrEmpty(key) && !cache.ContainsKey(key))
            {
                using var stream = image.Src.Bytes.AsStream();
                var bitmap = SKBitmap.Decode(stream);
                if (bitmap is not null)
                {
                    cache[key] = bitmap;
                }
            }
        }
        else if (element is FlexElement flex)
        {
            PreloadImagesFromElements(flex.Children, cache, cancellationToken);
        }
    }
}
```

**Step 2: Run all tests**

Run: `dotnet test FlexRender.slnx --no-restore -v minimal`
Expected: ALL PASS

**Step 3: Commit**

```bash
git add src/FlexRender.Skia.Render/Rendering/RenderingEngine.cs
git commit -m "feat: PreloadImages uses ExprValue.Bytes, skips loader chain when bytes available"
```

---

### Task 6: Full integration test and cleanup

**Files:**
- Create: `tests/FlexRender.Tests/Integration/BytesValueImageIntegrationTests.cs`
- Verify: all existing tests pass

**Step 1: Write integration test**

```csharp
using FlexRender.Abstractions;
using FlexRender.Configuration;
using FlexRender.Loaders;
using FlexRender.Parsing.Ast;
using FlexRender.TemplateEngine;
using Xunit;

namespace FlexRender.Tests.Integration;

/// <summary>
/// Integration tests verifying BytesValue flows through the full pipeline
/// for both content elements and image elements.
/// </summary>
public sealed class BytesValueImageIntegrationTests
{
    [Fact]
    public async Task FullPipeline_ImageWithBytesVariable_BytesPreservedInExprValue()
    {
        var expander = new TemplateExpander(new ResourceLimits());
        var processor = new TemplateProcessor(new ResourceLimits());
        var pipeline = new TemplatePipeline(expander, processor);

        var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        var template = new Template
        {
            Name = "test",
            Version = 1,
            Canvas = new CanvasSettings { Width = 200 },
            Elements =
            [
                new ImageElement { Src = ExprValue<string>.Expression("{{logo}}") }
            ]
        };

        var data = new ObjectValue
        {
            ["logo"] = new BytesValue(pngBytes, "image/png")
        };

        var result = await pipeline.ProcessAsync(template, data);
        var image = Assert.IsType<ImageElement>(result.Elements[0]);

        Assert.NotNull(image.Src.Bytes);
        Assert.Equal(pngBytes, image.Src.Bytes!.Value);
        Assert.Equal("image/png", image.Src.Bytes.MimeType);
    }

    [Fact]
    public async Task FullPipeline_ImageWithDataUri_BytesLoadedByLoaderChain()
    {
        var options = new FlexRenderOptions();
        IReadOnlyList<IResourceLoader> loaders = [new Base64ResourceLoader(options)];
        var expander = new TemplateExpander(new ResourceLimits(), resourceLoaders: loaders);
        var processor = new TemplateProcessor(new ResourceLimits());
        var pipeline = new TemplatePipeline(expander, processor);

        var rawBytes = new byte[] { 0xDE, 0xAD };
        var base64 = Convert.ToBase64String(rawBytes);

        var template = new Template
        {
            Name = "test",
            Version = 1,
            Canvas = new CanvasSettings { Width = 200 },
            Elements =
            [
                new ImageElement { Src = $"data:application/octet-stream;base64,{base64}" }
            ]
        };

        var result = await pipeline.ProcessAsync(template, new ObjectValue());
        var image = Assert.IsType<ImageElement>(result.Elements[0]);

        Assert.NotNull(image.Src.Bytes);
        Assert.Equal(rawBytes, image.Src.Bytes!.Value);
    }
}
```

**Step 2: Run full test suite**

Run: `dotnet test FlexRender.slnx --no-restore -v minimal`
Expected: ALL PASS

**Step 3: Commit**

```bash
git add tests/FlexRender.Tests/Integration/BytesValueImageIntegrationTests.cs
git commit -m "test: add BytesValue image integration tests"
```

---

### Task 7: Update documentation

**Files:**
- Modify: `docs/plans/2026-03-08-exprvalue-bytes-design.md` — mark as implemented
- Modify: `docs/wiki/API-Reference.md` — add BytesValue image support note
- Modify: `llms-full.txt` — update image element docs

**Step 1: Update docs**

In `docs/wiki/API-Reference.md` Resource Loading section, add note:

```markdown
> **BytesValue support:** When template variables resolve to `BytesValue` (binary data), the bytes are stored directly in `ExprValue.Bytes` without base64 encoding. This works for `image`, `svg`, and `content` elements. Resource loaders also pre-load URI sources (data:, file:, http://) into `ExprValue.Bytes` during template expansion.
```

**Step 2: Commit**

```bash
git add docs/
git commit -m "docs: document BytesValue unification in ExprValue"
```

using FlexRender.ImageSharp.Providers;
using FlexRender.Parsing.Ast;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace FlexRender.ImageSharp.Tests.Providers;

public sealed class ImageSharpImageProviderTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _testImagePath;

    public ImageSharpImageProviderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"imagesharp_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        // Create a 100x50 red test image
        _testImagePath = Path.Combine(_tempDir, "test.png");
        using var img = new Image<Rgba32>(100, 50, new Rgba32(255, 0, 0, 255));
        img.Save(_testImagePath, new PngEncoder());
    }

    [Fact]
    public void Generate_ValidPath_ReturnsImage()
    {
        var element = new ImageElement { Src = _testImagePath };
        using var image = ImageSharpImageProvider.Generate(element);

        Assert.NotNull(image);
        Assert.Equal(100, image.Width);
        Assert.Equal(50, image.Height);
    }

    [Fact]
    public void Generate_NullElement_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ImageSharpImageProvider.Generate(null!));
    }

    [Fact]
    public void Generate_EmptySrc_Throws()
    {
        var element = new ImageElement { Src = "" };
        Assert.Throws<ArgumentException>(() => ImageSharpImageProvider.Generate(element));
    }

    [Fact]
    public void Generate_FileNotFound_Throws()
    {
        var element = new ImageElement { Src = "/nonexistent/image.png" };
        Assert.Throws<FileNotFoundException>(() => ImageSharpImageProvider.Generate(element));
    }

    [Fact]
    public void Generate_PathTraversal_Throws()
    {
        // Path.GetFullPath canonicalizes the path, resolving ".." segments.
        // The resolved path won't exist, so FileNotFoundException is thrown.
        var element = new ImageElement { Src = "../../etc/passwd" };
        Assert.Throws<FileNotFoundException>(() => ImageSharpImageProvider.Generate(element));
    }

    [Fact]
    public void Generate_FitContain_PreservesAspectRatio()
    {
        var element = new ImageElement
        {
            Src = _testImagePath,
            Fit = ImageFit.Contain,
            ImageWidth = 200,
            ImageHeight = 200
        };
        using var image = ImageSharpImageProvider.Generate(element);

        Assert.Equal(200, image.Width);
        Assert.Equal(200, image.Height);
    }

    [Fact]
    public void Generate_FitFill_StretchesToTarget()
    {
        var element = new ImageElement
        {
            Src = _testImagePath,
            Fit = ImageFit.Fill,
            ImageWidth = 200,
            ImageHeight = 200
        };
        using var image = ImageSharpImageProvider.Generate(element);

        Assert.Equal(200, image.Width);
        Assert.Equal(200, image.Height);
    }

    [Fact]
    public void Generate_FitCover_FillsTarget()
    {
        var element = new ImageElement
        {
            Src = _testImagePath,
            Fit = ImageFit.Cover,
            ImageWidth = 200,
            ImageHeight = 200
        };
        using var image = ImageSharpImageProvider.Generate(element);

        Assert.Equal(200, image.Width);
        Assert.Equal(200, image.Height);
    }

    [Fact]
    public void Generate_FitNone_UsesNaturalSizeInTarget()
    {
        var element = new ImageElement
        {
            Src = _testImagePath,
            Fit = ImageFit.None,
            ImageWidth = 200,
            ImageHeight = 200
        };
        using var image = ImageSharpImageProvider.Generate(element);

        Assert.Equal(200, image.Width);
        Assert.Equal(200, image.Height);
    }

    [Fact]
    public void Generate_LayoutDimensions_TakePrecedence()
    {
        var element = new ImageElement
        {
            Src = _testImagePath,
            ImageWidth = 100,
            ImageHeight = 50
        };
        using var image = ImageSharpImageProvider.Generate(element, layoutWidth: 300, layoutHeight: 150);

        Assert.Equal(300, image.Width);
        Assert.Equal(150, image.Height);
    }

    [Fact]
    public void Generate_Base64DataUrl_LoadsImage()
    {
        // Create a base64-encoded 2x2 red PNG
        using var ms = new MemoryStream();
        using (var img = new Image<Rgba32>(2, 2, new Rgba32(255, 0, 0, 255)))
        {
            img.Save(ms, new PngEncoder());
        }
        var base64 = Convert.ToBase64String(ms.ToArray());
        var dataUrl = $"data:image/png;base64,{base64}";

        var element = new ImageElement { Src = dataUrl };
        using var image = ImageSharpImageProvider.Generate(element);

        Assert.Equal(2, image.Width);
        Assert.Equal(2, image.Height);
    }

    [Fact]
    public void Generate_InvalidBase64_Throws()
    {
        var element = new ImageElement { Src = "data:image/png;base64,!!invalid!!" };
        Assert.Throws<ArgumentException>(() => ImageSharpImageProvider.Generate(element));
    }

    [Fact]
    public void Generate_Base64NoComma_Throws()
    {
        var element = new ImageElement { Src = "data:image/png;base64" };
        Assert.Throws<ArgumentException>(() => ImageSharpImageProvider.Generate(element));
    }

    [Fact]
    public void Generate_FileExceedsMaxSize_Throws()
    {
        var oversizedPath = Path.Combine(_tempDir, "oversized.bin");
        try
        {
            // Create a file slightly over the 10 MB limit
            using (var fs = new FileStream(oversizedPath, FileMode.Create, FileAccess.Write))
            {
                fs.SetLength(ImageSharpImageProvider.MaxBase64DataSize + 1);
            }

            var element = new ImageElement { Src = oversizedPath };

            var ex = Assert.Throws<ArgumentException>(() => ImageSharpImageProvider.Generate(element));
            Assert.Contains("exceeds maximum size", ex.Message);
        }
        finally
        {
            if (File.Exists(oversizedPath))
            {
                File.Delete(oversizedPath);
            }
        }
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* cleanup best effort */ }
    }
}

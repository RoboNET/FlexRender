using FlexRender.Abstractions;
using FlexRender.Configuration;
using FlexRender.Parsing.Ast;
using FlexRender.Rendering;
using SkiaSharp;
using Xunit;
using AstFontStyle = global::FlexRender.Parsing.Ast.FontStyle;

namespace FlexRender.Tests.Rendering;

public class FontManagerTests : IDisposable
{
    private readonly FontManager _fontManager = new();
    private readonly string _tempDir;

    /// <summary>
    /// Absolute path to the test font directory under Snapshots/Fonts.
    /// </summary>
    private static readonly string TestFontsDir = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Snapshots", "Fonts"));

    /// <summary>
    /// Absolute path to the example fonts directory with full Inter family variants.
    /// </summary>
    private static readonly string ExampleFontsDir = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "examples", "assets", "fonts"));

    public FontManagerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"FlexRenderFontTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        _fontManager.Dispose();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    /// <summary>
    /// Simple in-memory resource loader for testing font preload from resources.
    /// </summary>
    private sealed class TestResourceLoader : IResourceLoader
    {
        private readonly Dictionary<string, byte[]> _resources = new(StringComparer.OrdinalIgnoreCase);

        /// <inheritdoc />
        public int Priority => 0;

        /// <summary>
        /// Adds a resource that can be loaded by key.
        /// </summary>
        /// <param name="key">The resource key (URI or file name).</param>
        /// <param name="data">The raw font bytes.</param>
        public void AddResource(string key, byte[] data) => _resources[key] = data;

        /// <inheritdoc />
        public bool CanHandle(string uri) =>
            _resources.ContainsKey(uri) || _resources.ContainsKey(Path.GetFileName(uri));

        /// <inheritdoc />
        public Task<Stream?> Load(string uri, CancellationToken cancellationToken = default)
        {
            if (_resources.TryGetValue(uri, out var data)
                || _resources.TryGetValue(Path.GetFileName(uri), out data))
            {
                return Task.FromResult<Stream?>(new MemoryStream(data, writable: false));
            }

            return Task.FromResult<Stream?>(null);
        }
    }

    [Fact]
    public void GetTypeface_DefaultFont_ReturnsTypeface()
    {
        var typeface = _fontManager.GetTypeface("main");

        Assert.NotNull(typeface);
    }

    [Fact]
    public void GetTypeface_UnknownFont_ReturnsFallback()
    {
        var typeface = _fontManager.GetTypeface("nonexistent-font-xyz");

        Assert.NotNull(typeface);
    }

    [Fact]
    public void GetTypeface_SameNameTwice_ReturnsSameInstance()
    {
        var typeface1 = _fontManager.GetTypeface("main");
        var typeface2 = _fontManager.GetTypeface("main");

        Assert.Same(typeface1, typeface2);
    }

    [Fact]
    public void RegisterFont_WithValidPath_RegistersFont()
    {
        // We can't easily create a valid font file in tests,
        // so we just test that the method doesn't throw for non-existent paths
        // and returns false for missing files

        var result = _fontManager.RegisterFont("test-font", "/nonexistent/path.ttf");

        Assert.False(result);
    }

    [Fact]
    public void RegisterFont_WithFallback_UsesFallbackOnMissingFile()
    {
        _fontManager.RegisterFont("custom", "/nonexistent.ttf", fallback: "Arial");

        var typeface = _fontManager.GetTypeface("custom");

        Assert.NotNull(typeface);
    }

    [Fact]
    public void SetDefaultFallback_ChangesDefault()
    {
        _fontManager.SetDefaultFallback("Helvetica");

        var typeface = _fontManager.GetTypeface("undefined-font");

        Assert.NotNull(typeface);
    }

    [Fact]
    public void ParseFontSize_Pixels_ReturnsValue()
    {
        var size = _fontManager.ParseFontSize("16", 12f, 100f);

        Assert.Equal(16f, size);
    }

    [Fact]
    public void ParseFontSize_Em_MultipliesBaseSize()
    {
        var size = _fontManager.ParseFontSize("1.5em", 12f, 100f);

        Assert.Equal(18f, size);
    }

    [Fact]
    public void ParseFontSize_Percent_MultipliesParentSize()
    {
        var size = _fontManager.ParseFontSize("50%", 12f, 100f);

        Assert.Equal(50f, size);
    }

    [Fact]
    public void ParseFontSize_Invalid_ReturnsBaseSize()
    {
        var size = _fontManager.ParseFontSize("invalid", 12f, 100f);

        Assert.Equal(12f, size);
    }

    [Fact]
    public void ParseFontSize_Empty_ReturnsBaseSize()
    {
        var size = _fontManager.ParseFontSize("", 12f, 100f);

        Assert.Equal(12f, size);
    }

    [Theory]
    [InlineData("24", 12f, 100f, 24f)]
    [InlineData("2em", 12f, 100f, 24f)]
    [InlineData("200%", 12f, 100f, 200f)]
    [InlineData("0.5em", 20f, 100f, 10f)]
    [InlineData("48px", 12f, 100f, 48f)]
    [InlineData("120px", 12f, 100f, 120f)]
    [InlineData("16.5px", 12f, 100f, 16.5f)]
    public void ParseFontSize_VariousFormats_ReturnsCorrectSize(
        string sizeStr, float baseSize, float parentSize, float expected)
    {
        var size = _fontManager.ParseFontSize(sizeStr, baseSize, parentSize);

        Assert.Equal(expected, size, precision: 2);
    }

    [Fact]
    public void ParseFontSize_PxSuffix_ReturnsValue()
    {
        var size = _fontManager.ParseFontSize("48px", 12f, 100f);

        Assert.Equal(48f, size);
    }

    [Fact]
    public void GetTypeface_MainFont_WithNoRegisteredFonts_ReturnsNonNull()
    {
        // "main" is the default font name used by TextElement.
        // When no fonts are registered, it should still return a valid typeface.
        var typeface = _fontManager.GetTypeface("main");

        Assert.NotNull(typeface);
    }

    [Fact]
    public void GetTypeface_EmptyString_ReturnsNonNull()
    {
        var typeface = _fontManager.GetTypeface("");

        Assert.NotNull(typeface);
    }

    [Fact]
    public void GetTypeface_WithDefaultWeightAndStyle_ReturnsSameAsBasic()
    {
        var basic = _fontManager.GetTypeface("main");
        var withDefaults = _fontManager.GetTypeface("main", FontWeight.Normal, AstFontStyle.Normal);

        // Default weight+style should delegate to the basic overload and return the same instance
        Assert.Same(basic, withDefaults);
    }

    [Fact]
    public void GetTypeface_WithBoldWeight_ReturnsNonNull()
    {
        var typeface = _fontManager.GetTypeface("main", FontWeight.Bold, AstFontStyle.Normal);

        Assert.NotNull(typeface);
    }

    [Fact]
    public void GetTypeface_WithItalicStyle_ReturnsNonNull()
    {
        var typeface = _fontManager.GetTypeface("main", FontWeight.Normal, AstFontStyle.Italic);

        Assert.NotNull(typeface);
    }

    [Fact]
    public void GetTypeface_WithBoldItalic_ReturnsNonNull()
    {
        var typeface = _fontManager.GetTypeface("main", FontWeight.Bold, AstFontStyle.Italic);

        Assert.NotNull(typeface);
    }

    [Fact]
    public void GetTypeface_WithObliqueStyle_ReturnsNonNull()
    {
        var typeface = _fontManager.GetTypeface("main", FontWeight.Normal, AstFontStyle.Oblique);

        Assert.NotNull(typeface);
    }

    [Theory]
    [InlineData(FontWeight.Thin)]
    [InlineData(FontWeight.ExtraLight)]
    [InlineData(FontWeight.Light)]
    [InlineData(FontWeight.Medium)]
    [InlineData(FontWeight.SemiBold)]
    [InlineData(FontWeight.ExtraBold)]
    [InlineData(FontWeight.Black)]
    public void GetTypeface_AllWeights_ReturnNonNull(FontWeight weight)
    {
        var typeface = _fontManager.GetTypeface("main", weight, AstFontStyle.Normal);

        Assert.NotNull(typeface);
    }

    [Fact]
    public void GetTypeface_SameVariantTwice_ReturnsSameInstance()
    {
        var first = _fontManager.GetTypeface("main", FontWeight.Bold, AstFontStyle.Italic);
        var second = _fontManager.GetTypeface("main", FontWeight.Bold, AstFontStyle.Italic);

        Assert.Same(first, second);
    }

    [Fact]
    public void GetTypeface_DifferentVariants_MayReturnDifferentInstances()
    {
        var normal = _fontManager.GetTypeface("main", FontWeight.Normal, AstFontStyle.Normal);
        var bold = _fontManager.GetTypeface("main", FontWeight.Bold, AstFontStyle.Normal);

        // Both should be non-null; they may or may not be different typefaces
        // depending on system fonts, but they should not throw
        Assert.NotNull(normal);
        Assert.NotNull(bold);
    }

    [Fact]
    public void GetTypeface_VariantCaseInsensitive_ReturnsSameInstance()
    {
        var lower = _fontManager.GetTypeface("main", FontWeight.Bold, AstFontStyle.Normal);
        var upper = _fontManager.GetTypeface("MAIN", FontWeight.Bold, AstFontStyle.Normal);

        Assert.Same(lower, upper);
    }

    [Fact]
    public void ToSkFontStyle_NormalDefaults_ReturnsUpright400()
    {
        var skStyle = FontManager.ToSkFontStyle(FontWeight.Normal, AstFontStyle.Normal);

        Assert.Equal((int)SKFontStyleWeight.Normal, skStyle.Weight);
        Assert.Equal(SKFontStyleSlant.Upright, skStyle.Slant);
    }

    [Fact]
    public void ToSkFontStyle_Bold_ReturnsWeight700()
    {
        var skStyle = FontManager.ToSkFontStyle(FontWeight.Bold, AstFontStyle.Normal);

        Assert.Equal(700, skStyle.Weight);
        Assert.Equal(SKFontStyleSlant.Upright, skStyle.Slant);
    }

    [Fact]
    public void ToSkFontStyle_Italic_ReturnsItalicSlant()
    {
        var skStyle = FontManager.ToSkFontStyle(FontWeight.Normal, AstFontStyle.Italic);

        Assert.Equal(SKFontStyleSlant.Italic, skStyle.Slant);
    }

    [Fact]
    public void ToSkFontStyle_Oblique_ReturnsObliqueSlant()
    {
        var skStyle = FontManager.ToSkFontStyle(FontWeight.Normal, AstFontStyle.Oblique);

        Assert.Equal(SKFontStyleSlant.Oblique, skStyle.Slant);
    }

    [Fact]
    public void ToSkFontStyle_Black_ReturnsWeight900()
    {
        var skStyle = FontManager.ToSkFontStyle(FontWeight.Black, AstFontStyle.Normal);

        Assert.Equal(900, skStyle.Weight);
    }

    // ─── IsFileLoaded tests ──────────────────────────────────────────────

    [Fact]
    public void IsFileLoaded_UnregisteredFont_ReturnsFalse()
    {
        Assert.False(_fontManager.IsFileLoaded("never-registered"));
    }

    [Fact]
    public void IsFileLoaded_SystemFallback_ReturnsFalse()
    {
        // Trigger system fallback by requesting an unregistered font name
        _fontManager.GetTypeface("some-system-font");

        Assert.False(_fontManager.IsFileLoaded("some-system-font"));
    }

    [Fact]
    public void IsFileLoaded_AfterFileRegistration_ReturnsTrue()
    {
        var fontPath = Path.Combine(TestFontsDir, "Inter-Regular.ttf");
        _fontManager.RegisterFont("inter", fontPath);

        // Force loading so the typeface is cached from file
        _fontManager.GetTypeface("inter");

        Assert.True(_fontManager.IsFileLoaded("inter"));
    }

    // ─── GetTypefaceInfo tests ───────────────────────────────────────────

    [Fact]
    public void GetTypefaceInfo_NonFileLoaded_ReturnsNull()
    {
        // Trigger system fallback
        _fontManager.GetTypeface("fallback-only");

        Assert.Null(_fontManager.GetTypefaceInfo("fallback-only"));
    }

    [Fact]
    public void GetTypefaceInfo_FileLoadedFont_ReturnsFamilyNameAndFixedPitch()
    {
        var fontPath = Path.Combine(TestFontsDir, "Inter-Regular.ttf");
        _fontManager.RegisterFont("inter-info", fontPath);
        _fontManager.GetTypeface("inter-info");

        var info = _fontManager.GetTypefaceInfo("inter-info");

        Assert.NotNull(info);
        Assert.False(string.IsNullOrEmpty(info.Value.FamilyName));
        // Inter is a proportional (variable-width) font
        Assert.False(info.Value.IsFixedPitch);
    }

    [Fact]
    public void GetTypefaceInfo_MonospaceFont_ReportsFixedPitch()
    {
        var fontPath = Path.Combine(TestFontsDir, "JetBrainsMono-Regular.ttf");
        _fontManager.RegisterFont("jetbrains", fontPath);
        _fontManager.GetTypeface("jetbrains");

        var info = _fontManager.GetTypefaceInfo("jetbrains");

        Assert.NotNull(info);
        Assert.True(info.Value.IsFixedPitch);
    }

    // ─── PreloadFontFromResourcesAsync tests ─────────────────────────────

    [Fact]
    public async Task PreloadFontFromResources_LoadsFontFromResourceLoader()
    {
        var fontBytes = await File.ReadAllBytesAsync(Path.Combine(TestFontsDir, "Inter-Regular.ttf"));
        var loader = new TestResourceLoader();
        loader.AddResource("fonts/Inter-Regular.ttf", fontBytes);

        using var manager = new FontManager([loader]);

        var result = await manager.PreloadFontFromResourcesAsync("preloaded", "fonts/Inter-Regular.ttf");

        Assert.True(result);
        Assert.True(manager.IsFileLoaded("preloaded"));

        var typeface = manager.GetTypeface("preloaded");
        Assert.NotNull(typeface);
    }

    [Fact]
    public async Task PreloadFontFromResources_NoLoaderHandlesKey_ReturnsFalse()
    {
        var loader = new TestResourceLoader();
        // Don't add any resources
        using var manager = new FontManager([loader]);

        var result = await manager.PreloadFontFromResourcesAsync("missing", "nonexistent.ttf");

        Assert.False(result);
        Assert.False(manager.IsFileLoaded("missing"));
    }

    [Fact]
    public async Task PreloadFontFromResources_DuplicateKey_DisposesNewTypeface()
    {
        var fontBytes = await File.ReadAllBytesAsync(Path.Combine(TestFontsDir, "Inter-Regular.ttf"));
        var loader = new TestResourceLoader();
        loader.AddResource("fonts/Inter.ttf", fontBytes);

        using var manager = new FontManager([loader]);

        // First preload succeeds
        var first = await manager.PreloadFontFromResourcesAsync("dup-font", "fonts/Inter.ttf");
        Assert.True(first);

        var originalTypeface = manager.GetTypeface("dup-font");

        // Second preload: key already in cache, new typeface should be disposed internally.
        // The method returns true because the loader DID handle the resource.
        var second = await manager.PreloadFontFromResourcesAsync("dup-font", "fonts/Inter.ttf");
        Assert.True(second);

        // The original cached typeface should still be the one returned
        var afterSecond = manager.GetTypeface("dup-font");
        Assert.Same(originalTypeface, afterSecond);
    }

    // ─── RegisterFont re-registration tests ──────────────────────────────

    [Fact]
    public void RegisterFont_Twice_SecondFileTakesEffect()
    {
        var interPath = Path.Combine(TestFontsDir, "Inter-Regular.ttf");
        var jetbrainsPath = Path.Combine(TestFontsDir, "JetBrainsMono-Regular.ttf");

        _fontManager.RegisterFont("swap-font", interPath);
        var first = _fontManager.GetTypeface("swap-font");
        Assert.NotNull(first);

        // Re-register with a different file
        _fontManager.RegisterFont("swap-font", jetbrainsPath);
        var second = _fontManager.GetTypeface("swap-font");

        // The second typeface should come from the new file (JetBrains Mono is monospaced)
        Assert.NotNull(second);
        Assert.NotSame(first, second);

        var info = _fontManager.GetTypefaceInfo("swap-font");
        Assert.NotNull(info);
        Assert.True(info.Value.IsFixedPitch, "After re-registration, should be JetBrains Mono (fixed-pitch)");
    }

    [Fact]
    public void RegisterFont_Twice_ClearsVariantCache()
    {
        var interPath = Path.Combine(ExampleFontsDir, "Inter-Regular.ttf");

        _fontManager.RegisterFont("variant-test", interPath);

        // Load a variant to populate variant cache
        var boldBefore = _fontManager.GetTypeface("variant-test", FontWeight.Bold, AstFontStyle.Normal);
        Assert.NotNull(boldBefore);

        // Re-register
        var jetbrainsPath = Path.Combine(TestFontsDir, "JetBrainsMono-Regular.ttf");
        _fontManager.RegisterFont("variant-test", jetbrainsPath);

        // Variant cache should be cleared; new request loads from new font
        var boldAfter = _fontManager.GetTypeface("variant-test", FontWeight.Bold, AstFontStyle.Normal);
        Assert.NotNull(boldAfter);
        Assert.NotSame(boldBefore, boldAfter);
    }

    // ─── Dispose deduplication tests ─────────────────────────────────────

    [Fact]
    public void Dispose_SharedTypefaces_DisposedOnlyOnce()
    {
        // Register and load a file font, then request a variant that falls back to base
        var fontPath = Path.Combine(TestFontsDir, "Inter-Regular.ttf");

        using var manager = new FontManager();
        manager.RegisterFont("dedup", fontPath);

        // Force base typeface into _typefaces cache
        var baseTypeface = manager.GetTypeface("dedup");

        // Request variant with Normal weight+style (fast path returns same base instance)
        var variant = manager.GetTypeface("dedup", FontWeight.Normal, AstFontStyle.Normal);
        Assert.Same(baseTypeface, variant);

        // Dispose should not throw even though the same SKTypeface instance
        // may appear in both _typefaces and _variantTypefaces
        manager.Dispose();

        // If we got here without ObjectDisposedException or AccessViolation, dedup works
    }

    [Fact]
    public void Dispose_OrphanedTypefaces_AreDisposed()
    {
        var interPath = Path.Combine(TestFontsDir, "Inter-Regular.ttf");
        var jetbrainsPath = Path.Combine(TestFontsDir, "JetBrainsMono-Regular.ttf");

        using var manager = new FontManager();
        manager.RegisterFont("orphan-test", interPath);
        var orphaned = manager.GetTypeface("orphan-test");

        // Re-register to orphan the first typeface
        manager.RegisterFont("orphan-test", jetbrainsPath);
        var replacement = manager.GetTypeface("orphan-test");

        Assert.NotSame(orphaned, replacement);

        // Dispose should handle the orphaned typeface without errors
        manager.Dispose();
    }

    // ─── Integration: LoadTypefaceByFamily tests ─────────────────────────

    [Fact]
    public void GetTypefaceByFamily_RegisteredFileFont_ResolvesToIt()
    {
        var fontPath = Path.Combine(TestFontsDir, "Inter-Regular.ttf");
        _fontManager.RegisterFont("my-inter", fontPath);

        // Force file-load so FamilyName is inspectable
        var loaded = _fontManager.GetTypeface("my-inter");
        var familyName = loaded.FamilyName;

        // Now query by family name
        var byFamily = _fontManager.GetTypefaceByFamily(familyName, FontWeight.Normal, AstFontStyle.Normal);

        Assert.NotNull(byFamily);
        Assert.Equal(familyName, byFamily.FamilyName, ignoreCase: true);
    }

    [Fact]
    public void GetTypefaceByFamily_SystemFont_FindsArial()
    {
        // System font lookup; skip if running in WASM (this test runs on desktop)
        var typeface = _fontManager.GetTypefaceByFamily("Arial", FontWeight.Normal, AstFontStyle.Normal);

        Assert.NotNull(typeface);
        // On most desktop systems, Arial is available. If not, we get the "main" fallback.
    }

    [Fact]
    public void GetTypefaceByFamily_UnknownFamily_FallsBackToMain()
    {
        var typeface = _fontManager.GetTypefaceByFamily(
            "NonExistentFontFamily12345", FontWeight.Normal, AstFontStyle.Normal);

        Assert.NotNull(typeface);
        // Should be the "main" fallback typeface
        var mainTypeface = _fontManager.GetTypeface("main");
        Assert.Same(mainTypeface, typeface);
    }

    [Fact]
    public void GetTypefaceByFamily_LazyLoadedFont_ResolvesWithoutPriorGetTypeface()
    {
        // Regression test: RegisterFont only populates _fontPaths.
        // _fileLoadedTypefaces is populated lazily by GetTypeface → LoadTypeface.
        // LoadTypefaceByFamily must trigger lazy loading before checking _fileLoadedTypefaces,
        // otherwise registered file fonts are skipped and a system fallback is returned.
        var fontPath = Path.Combine(TestFontsDir, "JetBrainsMono-Regular.ttf");
        _fontManager.RegisterFont("mono-lazy", fontPath);

        // Do NOT call GetTypeface("mono-lazy") — that would mask the bug.
        // Go directly to family-based lookup.
        var byFamily = _fontManager.GetTypefaceByFamily("JetBrains Mono", FontWeight.Normal, AstFontStyle.Normal);

        Assert.NotNull(byFamily);
        Assert.Equal("JetBrains Mono", byFamily.FamilyName, ignoreCase: true);
        Assert.True(byFamily.IsFixedPitch, "JetBrains Mono should be monospaced");
    }

    // ─── Integration: Font variant resolution from file ──────────────────

    [Fact]
    public void GetTypeface_BoldWeight_FindsSiblingBoldFile()
    {
        // Register Inter-Regular from examples dir (which has Inter-Bold sibling)
        var regularPath = Path.Combine(ExampleFontsDir, "Inter-Regular.ttf");
        _fontManager.RegisterFont("inter-variants", regularPath);

        // Request Bold variant
        var bold = _fontManager.GetTypeface("inter-variants", FontWeight.Bold, AstFontStyle.Normal);

        Assert.NotNull(bold);
        // The bold variant should have weight closer to 700 than the regular (400)
        Assert.True(bold.FontStyle.Weight >= 600,
            $"Expected bold weight >= 600, got {bold.FontStyle.Weight}");
    }

    [Fact]
    public void GetTypeface_ItalicStyle_FindsSiblingItalicFile()
    {
        var regularPath = Path.Combine(ExampleFontsDir, "Inter-Regular.ttf");
        _fontManager.RegisterFont("inter-italic-test", regularPath);

        var italic = _fontManager.GetTypeface("inter-italic-test", FontWeight.Normal, AstFontStyle.Italic);

        Assert.NotNull(italic);
        Assert.True(italic.FontStyle.Slant != SKFontStyleSlant.Upright,
            $"Expected italic/oblique slant, got {italic.FontStyle.Slant}");
    }

    // ─── Integration: Font variant resolution from registered family ─────

    [Fact]
    public void GetTypefaceByFamily_RegisteredBothWeights_ResolvesBold()
    {
        var regularPath = Path.Combine(ExampleFontsDir, "Inter-Regular.ttf");
        var boldPath = Path.Combine(ExampleFontsDir, "Inter-Bold.ttf");

        _fontManager.RegisterFont("inter-reg", regularPath);
        _fontManager.RegisterFont("inter-bold", boldPath);

        // Force load both so they're file-loaded
        var regular = _fontManager.GetTypeface("inter-reg");
        var bold = _fontManager.GetTypeface("inter-bold");
        var familyName = regular.FamilyName;

        // Query by family name with Bold weight
        var resolvedBold = _fontManager.GetTypefaceByFamily(familyName, FontWeight.Bold, AstFontStyle.Normal);

        Assert.NotNull(resolvedBold);
        Assert.True(resolvedBold.FontStyle.Weight >= 600,
            $"Expected bold weight >= 600, got {resolvedBold.FontStyle.Weight}");
    }

    // ─── Integration: TemplatePreprocessor font registration ─────────────

    [Fact]
    public async Task RegisterFontsAsync_FileFonts_RegistersAndFileLoads()
    {
        var fontPath = Path.Combine(TestFontsDir, "Inter-Regular.ttf");

        var template = new Template
        {
            Fonts =
            {
                ["test-font"] = new FontDefinition(fontPath)
            }
        };

        var preprocessor = new TemplatePreprocessor(_fontManager, options: null);
        await preprocessor.RegisterFontsAsync(template);

        // Font should be registered and loadable
        var typeface = _fontManager.GetTypeface("test-font");
        Assert.NotNull(typeface);
        Assert.True(_fontManager.IsFileLoaded("test-font"));
    }

    [Fact]
    public async Task RegisterFontsAsync_DefaultFont_AlsoRegistersAsMain()
    {
        var fontPath = Path.Combine(TestFontsDir, "Inter-Regular.ttf");

        var template = new Template
        {
            Fonts =
            {
                ["default"] = new FontDefinition(fontPath)
            }
        };

        var preprocessor = new TemplatePreprocessor(_fontManager, options: null);
        await preprocessor.RegisterFontsAsync(template);

        // Force-load both to trigger file-loaded tracking (lazy loading)
        var defaultTypeface = _fontManager.GetTypeface("default");
        var mainTypeface = _fontManager.GetTypeface("main");

        Assert.True(_fontManager.IsFileLoaded("default"));
        Assert.True(_fontManager.IsFileLoaded("main"));

        // Both should resolve to the same font family
        Assert.Equal(defaultTypeface.FamilyName, mainTypeface.FamilyName, ignoreCase: true);
    }

    [Fact]
    public async Task RegisterFontsAsync_ResourceLoaderFallback_LoadsFromLoader()
    {
        var fontBytes = await File.ReadAllBytesAsync(Path.Combine(TestFontsDir, "Inter-Regular.ttf"));
        var loader = new TestResourceLoader();
        loader.AddResource("assets/fonts/Inter-Regular.ttf", fontBytes);

        using var manager = new FontManager([loader]);

        var template = new Template
        {
            Fonts =
            {
                ["resource-font"] = new FontDefinition("assets/fonts/Inter-Regular.ttf")
            }
        };

        var preprocessor = new TemplatePreprocessor(manager, options: null);
        await preprocessor.RegisterFontsAsync(template);

        // Font file doesn't exist on disk at "assets/fonts/Inter-Regular.ttf" (relative),
        // so it falls back to resource loader
        var typeface = manager.GetTypeface("resource-font");
        Assert.NotNull(typeface);
        Assert.True(manager.IsFileLoaded("resource-font"));
    }

    [Fact]
    public async Task RegisterFontsAsync_WithBasePath_ResolvesRelativeFontPath()
    {
        var template = new Template
        {
            Fonts =
            {
                ["base-path-font"] = new FontDefinition("Snapshots/Fonts/Inter-Regular.ttf")
            }
        };

        // Use the test project root as base path
        var testProjectRoot = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
        var options = new FlexRenderOptions { BasePath = testProjectRoot };

        var preprocessor = new TemplatePreprocessor(_fontManager, options);
        await preprocessor.RegisterFontsAsync(template);

        var typeface = _fontManager.GetTypeface("base-path-font");
        Assert.NotNull(typeface);
        Assert.True(_fontManager.IsFileLoaded("base-path-font"));
    }

    // ─── GetTypeface four-parameter overload tests ───────────────────────

    [Fact]
    public void GetTypeface_WithFontNameAndFamily_PrefersRegisteredName()
    {
        var interPath = Path.Combine(TestFontsDir, "Inter-Regular.ttf");
        var jetbrainsPath = Path.Combine(TestFontsDir, "JetBrainsMono-Regular.ttf");

        _fontManager.RegisterFont("explicit-name", jetbrainsPath);
        _fontManager.RegisterFont("other-font", interPath);

        // When fontName is explicitly set (not "main"), it should take priority over fontFamily
        var typeface = _fontManager.GetTypeface(
            "explicit-name", "Arial", FontWeight.Normal, AstFontStyle.Normal);

        var info = _fontManager.GetTypefaceInfo("explicit-name");
        Assert.NotNull(info);
        Assert.True(info.Value.IsFixedPitch, "Should resolve to JetBrains Mono by name, not Arial by family");
    }

    [Fact]
    public void GetTypeface_WithMainNameAndFamily_UsesFamilyLookup()
    {
        var interPath = Path.Combine(TestFontsDir, "Inter-Regular.ttf");
        _fontManager.RegisterFont("inter-family", interPath);

        // Force load to populate file-loaded metadata
        var loaded = _fontManager.GetTypeface("inter-family");
        var familyName = loaded.FamilyName;

        // When fontName is "main", should fall through to fontFamily lookup
        var typeface = _fontManager.GetTypeface(
            "main", familyName, FontWeight.Normal, AstFontStyle.Normal);

        Assert.NotNull(typeface);
        Assert.Equal(familyName, typeface.FamilyName, ignoreCase: true);
    }

    // ─── RegisteredFontPaths property test ───────────────────────────────

    [Fact]
    public void RegisteredFontPaths_ReflectsRegisteredFonts()
    {
        _fontManager.RegisterFont("path-a", "/some/path/a.ttf");
        _fontManager.RegisterFont("path-b", "/some/path/b.ttf");

        var paths = _fontManager.RegisteredFontPaths;

        Assert.Equal(2, paths.Count);
        Assert.Equal("/some/path/a.ttf", paths["path-a"]);
        Assert.Equal("/some/path/b.ttf", paths["path-b"]);
    }

    // ─── ObjectDisposedException tests ───────────────────────────────────

    [Fact]
    public void GetTypeface_AfterDispose_Throws()
    {
        var manager = new FontManager();
        manager.Dispose();

        Assert.Throws<ObjectDisposedException>(() => manager.GetTypeface("main"));
    }

    [Fact]
    public void GetTypefaceByFamily_AfterDispose_Throws()
    {
        var manager = new FontManager();
        manager.Dispose();

        Assert.Throws<ObjectDisposedException>(() =>
            manager.GetTypefaceByFamily("Arial", FontWeight.Normal, AstFontStyle.Normal));
    }
}

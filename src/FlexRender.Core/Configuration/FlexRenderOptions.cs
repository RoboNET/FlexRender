using System.Reflection;

namespace FlexRender.Configuration;

/// <summary>
/// Configuration options for FlexRender rendering engine.
/// </summary>
/// <remarks>
/// These options control various aspects of template rendering including
/// font defaults, image loading, caching, and resource resolution.
/// </remarks>
public sealed class FlexRenderOptions
{
    /// <summary>
    /// Gets the resource limits configuration.
    /// </summary>
    /// <remarks>
    /// Use this to configure security limits for file sizes, nesting depths,
    /// and timeouts. All limits have safe defaults.
    /// </remarks>
    public ResourceLimits Limits { get; } = new();

    /// <summary>
    /// Gets or sets the base path for resolving relative file paths.
    /// </summary>
    /// <remarks>
    /// When set, relative paths in templates (for images, fonts, etc.)
    /// will be resolved relative to this path.
    /// </remarks>
    public string? BasePath { get; set; }

    /// <summary>
    /// Gets or sets the default font family used when no font is specified.
    /// </summary>
    /// <value>The default value is "Arial".</value>
    public string DefaultFontFamily { get; set; } = "Arial";

    /// <summary>
    /// Gets or sets the base font size in points used when no size is specified.
    /// </summary>
    /// <value>The default value is 12.</value>
    public float BaseFontSize { get; set; } = 12f;

    /// <summary>
    /// Gets or sets the timeout duration for HTTP requests when loading remote resources.
    /// </summary>
    /// <value>The default value is 30 seconds.</value>
    /// <remarks>
    /// This property delegates to <see cref="Limits"/>.<see cref="ResourceLimits.HttpTimeout"/>.
    /// Both this property and <c>Limits.HttpTimeout</c> reflect the same value.
    /// </remarks>
    public TimeSpan HttpTimeout
    {
        get => Limits.HttpTimeout;
        set => Limits.HttpTimeout = value;
    }

    /// <summary>
    /// Gets or sets the maximum allowed image size in bytes.
    /// </summary>
    /// <remarks>
    /// Images exceeding this size will be rejected to prevent memory issues.
    /// This property delegates to <see cref="Limits"/>.<see cref="ResourceLimits.MaxImageSize"/>.
    /// Both this property and <c>Limits.MaxImageSize</c> reflect the same value.
    /// </remarks>
    /// <value>The default value is 10 MB (10 * 1024 * 1024 bytes).</value>
    public int MaxImageSize
    {
        get => Limits.MaxImageSize;
        set => Limits.MaxImageSize = value;
    }

    /// <summary>
    /// Gets or sets a value indicating whether resource caching is enabled.
    /// </summary>
    /// <remarks>
    /// When enabled, loaded images and fonts are cached in memory
    /// to improve performance for repeated access.
    /// </remarks>
    /// <value>The default value is <c>true</c>.</value>
    public bool EnableCaching { get; set; } = true;

    /// <summary>
    /// Gets the list of assemblies to search for embedded resources.
    /// </summary>
    /// <remarks>
    /// Add assemblies containing embedded fonts, images, or other resources
    /// that should be accessible via the embedded:// URI scheme.
    /// </remarks>
    public List<Assembly> EmbeddedResourceAssemblies { get; } = new();
}

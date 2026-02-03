using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using FlexRender.Abstractions;

namespace FlexRender.Configuration;

/// <summary>
/// Fluent builder for configuring FlexRender services and options.
/// </summary>
/// <remarks>
/// Use this builder to configure the FlexRender engine with custom settings,
/// resource loaders, and embedded resource assemblies.
/// </remarks>
/// <example>
/// <code>
/// services.AddFlexRender(builder => builder
///     .WithBasePath("/app/templates")
///     .WithDefaultFont("Roboto")
///     .WithHttpTimeout(TimeSpan.FromSeconds(60))
///     .EnableCaching()
///     .AddEmbeddedResources&lt;MyApp&gt;());
/// </code>
/// </example>
public sealed class FlexRenderBuilder
{
    private readonly IServiceCollection _services;
    private readonly FlexRenderOptions _options = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="FlexRenderBuilder"/> class.
    /// </summary>
    /// <param name="services">The service collection to register services with.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    public FlexRenderBuilder(IServiceCollection services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    /// <summary>
    /// Sets the base path for resolving relative file paths in templates.
    /// </summary>
    /// <param name="path">The base directory path.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is empty or whitespace.</exception>
    public FlexRenderBuilder WithBasePath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Base path cannot be empty or whitespace.", nameof(path));
        }

        _options.BasePath = path;
        return this;
    }

    /// <summary>
    /// Sets the default font family used when no font is specified in templates.
    /// </summary>
    /// <param name="fontFamily">The font family name (e.g., "Arial", "Roboto").</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="fontFamily"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="fontFamily"/> is empty or whitespace.</exception>
    public FlexRenderBuilder WithDefaultFont(string fontFamily)
    {
        ArgumentNullException.ThrowIfNull(fontFamily);
        if (string.IsNullOrWhiteSpace(fontFamily))
        {
            throw new ArgumentException("Font family cannot be empty or whitespace.", nameof(fontFamily));
        }

        _options.DefaultFontFamily = fontFamily;
        return this;
    }

    /// <summary>
    /// Sets the timeout duration for HTTP requests when loading remote resources.
    /// </summary>
    /// <param name="timeout">The timeout duration. Must be positive.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="timeout"/> is zero or negative.</exception>
    public FlexRenderBuilder WithHttpTimeout(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "Timeout must be a positive duration.");
        }

        _options.HttpTimeout = timeout;
        return this;
    }

    /// <summary>
    /// Sets the maximum allowed image size in bytes.
    /// </summary>
    /// <param name="bytes">The maximum size in bytes. Must be positive.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="bytes"/> is zero or negative.</exception>
    public FlexRenderBuilder WithMaxImageSize(int bytes)
    {
        if (bytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bytes), bytes, "Maximum image size must be positive.");
        }

        _options.MaxImageSize = bytes;
        return this;
    }

    /// <summary>
    /// Configures resource limits for the FlexRender engine.
    /// </summary>
    /// <param name="configure">An action to configure the resource limits.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configure"/> is null.</exception>
    /// <example>
    /// <code>
    /// builder.WithLimits(limits =>
    /// {
    ///     limits.MaxTemplateFileSize = 2 * 1024 * 1024;
    ///     limits.MaxRenderDepth = 200;
    /// });
    /// </code>
    /// </example>
    public FlexRenderBuilder WithLimits(Action<ResourceLimits> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(_options.Limits);
        return this;
    }

    /// <summary>
    /// Enables or disables resource caching.
    /// </summary>
    /// <param name="enable">True to enable caching; false to disable.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public FlexRenderBuilder EnableCaching(bool enable = true)
    {
        _options.EnableCaching = enable;
        return this;
    }

    /// <summary>
    /// Registers a custom resource loader implementation.
    /// </summary>
    /// <typeparam name="T">The resource loader type implementing <see cref="IResourceLoader"/>.</typeparam>
    /// <returns>The builder instance for method chaining.</returns>
    /// <remarks>
    /// Multiple resource loaders can be registered. They will be tried in order
    /// based on their <see cref="IResourceLoader.Priority"/> property.
    /// </remarks>
    public FlexRenderBuilder AddResourceLoader<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>() where T : class, IResourceLoader
    {
        _services.AddSingleton<IResourceLoader, T>();
        return this;
    }

    /// <summary>
    /// Adds an assembly to search for embedded resources.
    /// </summary>
    /// <param name="assembly">The assembly containing embedded resources.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="assembly"/> is null.</exception>
    public FlexRenderBuilder AddEmbeddedResources(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        if (!_options.EmbeddedResourceAssemblies.Contains(assembly))
        {
            _options.EmbeddedResourceAssemblies.Add(assembly);
        }

        return this;
    }

    /// <summary>
    /// Adds the assembly containing the specified type to search for embedded resources.
    /// </summary>
    /// <typeparam name="T">A type from the assembly to add.</typeparam>
    /// <returns>The builder instance for method chaining.</returns>
    public FlexRenderBuilder AddEmbeddedResources<T>()
    {
        return AddEmbeddedResources(typeof(T).Assembly);
    }

    /// <summary>
    /// Builds and registers all configured services and options.
    /// </summary>
    /// <remarks>
    /// This method is called internally by the extension methods
    /// and should not be called directly by consumers.
    /// </remarks>
    internal void Build()
    {
        _services.TryAddSingleton(Options.Create(_options));
        _services.TryAddSingleton(_options);
    }
}

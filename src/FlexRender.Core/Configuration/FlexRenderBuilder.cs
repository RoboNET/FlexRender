using System.Reflection;
using FlexRender.Abstractions;
using FlexRender.Loaders;

namespace FlexRender.Configuration;

/// <summary>
/// Builder for configuring and creating <see cref="IFlexRender"/> instances.
/// </summary>
/// <remarks>
/// <para>
/// This builder provides a fluent API for configuring rendering options, resource limits,
/// and resource loaders. A renderer implementation must be registered using an extension
/// method like <c>WithSkia()</c> before calling <see cref="Build"/>.
/// </para>
/// <para>
/// By default, the builder includes <see cref="FileResourceLoader"/> and
/// <see cref="Base64ResourceLoader"/>. Use <see cref="WithoutDefaultLoaders"/>
/// to start with an empty loader list for sandboxed scenarios.
/// </para>
/// </remarks>
/// <example>
/// Basic usage:
/// <code>
/// var render = new FlexRenderBuilder()
///     .WithSkia()
///     .Build();
/// </code>
/// Full configuration:
/// <code>
/// var render = new FlexRenderBuilder()
///     .WithBasePath("./templates")
///     .WithLimits(limits => limits.MaxRenderDepth = 200)
///     .WithEmbeddedLoader(typeof(Program).Assembly)
///     .WithSkia(skia => skia.WithQr().WithBarcode())
///     .Build();
/// </code>
/// Sandboxed (no file system access):
/// <code>
/// var render = new FlexRenderBuilder()
///     .WithoutDefaultLoaders()
///     .WithEmbeddedLoader(typeof(Program).Assembly)
///     .WithSkia()
///     .Build();
/// </code>
/// </example>
public sealed class FlexRenderBuilder
{
    private Func<FlexRenderBuilder, IFlexRender>? _rendererFactory;
    private bool _defaultLoadersAdded;
    private bool _built;

    /// <summary>
    /// Gets the resource limits configuration.
    /// </summary>
    /// <remarks>
    /// This property provides direct access to <see cref="FlexRenderOptions.Limits"/>.
    /// Use <see cref="WithLimits"/> for fluent configuration.
    /// </remarks>
    internal ResourceLimits Limits => Options.Limits;

    /// <summary>
    /// Gets the rendering options configuration.
    /// </summary>
    internal FlexRenderOptions Options { get; } = new();

    /// <summary>
    /// Gets the list of configured resource loaders.
    /// </summary>
    /// <remarks>
    /// Loaders are added lazily when <see cref="Build"/> is called to ensure
    /// they receive the fully configured <see cref="Options"/> instance.
    /// </remarks>
    internal List<IResourceLoader> ResourceLoaders { get; } = [];

    /// <summary>
    /// Sets the renderer factory function that creates the <see cref="IFlexRender"/> implementation.
    /// </summary>
    /// <param name="factory">
    /// A function that receives this builder and returns an <see cref="IFlexRender"/> instance.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="factory"/> is null.</exception>
    /// <remarks>
    /// This method is intended for use by renderer extension methods like <c>WithSkia()</c>.
    /// Multiple calls will replace the previous factory.
    /// </remarks>
    internal void SetRendererFactory(Func<FlexRenderBuilder, IFlexRender> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _rendererFactory = factory;
    }

    /// <summary>
    /// Configures the resource limits for the renderer.
    /// </summary>
    /// <param name="configure">An action to configure the <see cref="ResourceLimits"/>.</param>
    /// <returns>This builder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configure"/> is null.</exception>
    /// <example>
    /// <code>
    /// builder.WithLimits(limits =>
    /// {
    ///     limits.MaxRenderDepth = 200;
    ///     limits.MaxImageSize = 20 * 1024 * 1024;
    /// });
    /// </code>
    /// </example>
    public FlexRenderBuilder WithLimits(Action<ResourceLimits> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(Limits);
        return this;
    }

    /// <summary>
    /// Sets the base path for resolving relative file paths in templates.
    /// </summary>
    /// <param name="basePath">The base directory path for relative resource resolution.</param>
    /// <returns>This builder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="basePath"/> is null.</exception>
    /// <remarks>
    /// When set, relative paths in templates (for images, fonts, etc.)
    /// will be resolved relative to this path.
    /// </remarks>
    /// <example>
    /// <code>
    /// builder.WithBasePath("./templates");
    /// </code>
    /// </example>
    public FlexRenderBuilder WithBasePath(string basePath)
    {
        ArgumentNullException.ThrowIfNull(basePath);
        Options.BasePath = basePath;
        return this;
    }

    /// <summary>
    /// Adds an assembly to the list of assemblies to search for embedded resources.
    /// </summary>
    /// <param name="assembly">The assembly containing embedded resources.</param>
    /// <returns>This builder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="assembly"/> is null.</exception>
    /// <remarks>
    /// Embedded resources can be accessed using the <c>embedded://</c> URI scheme.
    /// Multiple assemblies can be registered by calling this method multiple times.
    /// </remarks>
    /// <example>
    /// <code>
    /// builder.WithEmbeddedLoader(typeof(Program).Assembly);
    /// </code>
    /// </example>
    public FlexRenderBuilder WithEmbeddedLoader(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        Options.EmbeddedResourceAssemblies.Add(assembly);
        return this;
    }

    /// <summary>
    /// Clears all default resource loaders, enabling sandboxed operation.
    /// </summary>
    /// <returns>This builder instance for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// By default, the builder includes <see cref="FileResourceLoader"/> and
    /// <see cref="Base64ResourceLoader"/>. Call this method to remove them
    /// for scenarios where file system access should be restricted.
    /// </para>
    /// <para>
    /// After calling this method, you can selectively add loaders using methods
    /// like <see cref="WithEmbeddedLoader"/> or extension methods like <c>WithHttpLoader()</c>.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Sandboxed: only embedded resources allowed
    /// var render = new FlexRenderBuilder()
    ///     .WithoutDefaultLoaders()
    ///     .WithEmbeddedLoader(typeof(Program).Assembly)
    ///     .WithSkia()
    ///     .Build();
    /// </code>
    /// </example>
    public FlexRenderBuilder WithoutDefaultLoaders()
    {
        ResourceLoaders.Clear();
        _defaultLoadersAdded = true; // Prevent adding defaults in Build()
        return this;
    }

    /// <summary>
    /// Builds and returns the configured <see cref="IFlexRender"/> instance.
    /// </summary>
    /// <returns>A fully configured <see cref="IFlexRender"/> instance.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no renderer has been configured. Call an extension method
    /// like <c>WithSkia()</c> before calling <see cref="Build"/>.
    /// </exception>
    /// <remarks>
    /// This method finalizes the configuration and creates the renderer.
    /// The builder should not be modified after calling this method.
    /// </remarks>
    public IFlexRender Build()
    {
        if (_built)
        {
            throw new InvalidOperationException(
                "Build() has already been called. Create a new FlexRenderBuilder for additional instances.");
        }

        if (_rendererFactory is null)
        {
            throw new InvalidOperationException(
                "No renderer configured. Call WithSkia() or another renderer extension method.");
        }

        // Add default loaders if not explicitly cleared
        if (!_defaultLoadersAdded)
        {
            AddDefaultLoaders();
        }

        // Add embedded resource loader if assemblies were registered
        if (Options.EmbeddedResourceAssemblies.Count > 0)
        {
            ResourceLoaders.Add(new EmbeddedResourceLoader(Options));
        }

        _built = true;
        return _rendererFactory(this);
    }

    /// <summary>
    /// Adds the default resource loaders (File and Base64).
    /// </summary>
    private void AddDefaultLoaders()
    {
        ResourceLoaders.Add(new FileResourceLoader(Options));
        ResourceLoaders.Add(new Base64ResourceLoader(Options));
        _defaultLoadersAdded = true;
    }
}

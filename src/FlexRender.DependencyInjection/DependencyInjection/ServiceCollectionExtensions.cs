using FlexRender.Abstractions;
using FlexRender.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering FlexRender services with the dependency injection container.
/// </summary>
/// <remarks>
/// <para>
/// This package provides minimal integration between FlexRender.Core and
/// Microsoft.Extensions.DependencyInjection. It registers <see cref="IFlexRender"/>
/// as a singleton service.
/// </para>
/// <para>
/// The actual renderer implementation (e.g., Skia) and optional features (e.g., QR codes, barcodes)
/// must be configured through the <see cref="FlexRenderBuilder"/> in the configuration callback.
/// </para>
/// </remarks>
/// <example>
/// Basic usage with Skia renderer:
/// <code>
/// services.AddFlexRender(builder => builder
///     .WithSkia());
/// </code>
/// Full configuration with service provider access:
/// <code>
/// services.AddFlexRender((sp, builder) =>
/// {
///     var config = sp.GetRequiredService&lt;IConfiguration&gt;();
///     builder
///         .WithBasePath(config["Templates:BasePath"] ?? "./templates")
///         .WithSkia(skia => skia.WithQr().WithBarcode());
/// });
/// </code>
/// </example>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds FlexRender services to the specified <see cref="IServiceCollection"/>
    /// with access to the service provider for advanced configuration scenarios.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configure">
    /// An action to configure the <see cref="FlexRenderBuilder"/>. The action receives
    /// the <see cref="IServiceProvider"/> for resolving other services during configuration.
    /// </param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="services"/> or <paramref name="configure"/> is null.
    /// </exception>
    /// <remarks>
    /// <para>
    /// This overload is useful when the FlexRender configuration depends on other services,
    /// such as <c>IConfiguration</c> for reading settings or <c>IHttpClientFactory</c>
    /// for configuring HTTP resource loading.
    /// </para>
    /// <para>
    /// The <see cref="IFlexRender"/> instance is registered as a singleton and created
    /// lazily when first resolved from the container.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddFlexRender((sp, builder) =>
    /// {
    ///     var config = sp.GetRequiredService&lt;IConfiguration&gt;();
    ///     var basePath = config["FlexRender:BasePath"] ?? "./templates";
    ///
    ///     builder
    ///         .WithBasePath(basePath)
    ///         .WithLimits(limits => limits.MaxRenderDepth = 200)
    ///         .WithSkia(skia => skia.WithQr().WithBarcode());
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddFlexRender(
        this IServiceCollection services,
        Action<IServiceProvider, FlexRenderBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddSingleton<IFlexRender>(sp =>
        {
            var builder = new FlexRenderBuilder();
            configure(sp, builder);
            return builder.Build();
        });

        return services;
    }

    /// <summary>
    /// Adds FlexRender services to the specified <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configure">An action to configure the <see cref="FlexRenderBuilder"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="services"/> or <paramref name="configure"/> is null.
    /// </exception>
    /// <remarks>
    /// <para>
    /// This is the simpler overload for scenarios where configuration does not depend
    /// on other services. For advanced scenarios requiring service resolution,
    /// use the overload that accepts <see cref="Action{IServiceProvider, FlexRenderBuilder}"/>.
    /// </para>
    /// <para>
    /// A renderer implementation must be configured using an extension method like
    /// <c>WithSkia()</c> before the service is resolved.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddFlexRender(builder => builder
    ///     .WithBasePath("./templates")
    ///     .WithSkia(skia => skia.WithQr()));
    /// </code>
    /// </example>
    public static IServiceCollection AddFlexRender(
        this IServiceCollection services,
        Action<FlexRenderBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        return services.AddFlexRender((_, builder) => configure(builder));
    }
}

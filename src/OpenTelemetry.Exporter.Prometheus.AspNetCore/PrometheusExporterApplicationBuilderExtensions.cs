// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter;
using OpenTelemetry.Internal;
using OpenTelemetry.Metrics;

namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// Provides extension methods for <see cref="IApplicationBuilder"/> to add
/// Prometheus scraping endpoint.
/// </summary>
public static class PrometheusExporterApplicationBuilderExtensions
{
    /// <summary>
    /// Adds OpenTelemetry Prometheus scraping endpoint middleware to an
    /// <see cref="IApplicationBuilder"/> instance.
    /// </summary>
    /// <remarks>Note: A branched pipeline is created for the route
    /// specified by <see
    /// cref="PrometheusAspNetCoreOptions.ScrapeEndpointPath"/>.</remarks>
    /// <param name="app">The <see cref="IApplicationBuilder"/> to add
    /// middleware to.</param>
    /// <returns>A reference to the original <see
    /// cref="IApplicationBuilder"/> for chaining calls.</returns>
    public static IApplicationBuilder UseOpenTelemetryPrometheusScrapingEndpoint(this IApplicationBuilder app)
        => UseOpenTelemetryPrometheusScrapingEndpoint(app, meterProvider: null, predicate: null, path: null, configureBranchedPipeline: null, optionsName: null);

    /// <summary>
    /// Adds OpenTelemetry Prometheus scraping endpoint middleware to an
    /// <see cref="IApplicationBuilder"/> instance.
    /// </summary>
    /// <remarks>Note: A branched pipeline is created for the supplied
    /// <paramref name="path"/>.</remarks>
    /// <param name="app">The <see cref="IApplicationBuilder"/> to add
    /// middleware to.</param>
    /// <param name="path">Path to use for the branched pipeline.</param>
    /// <returns>A reference to the original <see
    /// cref="IApplicationBuilder"/> for chaining calls.</returns>
    public static IApplicationBuilder UseOpenTelemetryPrometheusScrapingEndpoint(this IApplicationBuilder app, string path)
    {
        Guard.ThrowIfNull(path);
        return UseOpenTelemetryPrometheusScrapingEndpoint(app, meterProvider: null, predicate: null, path: path, configureBranchedPipeline: null, optionsName: null);
    }

    /// <summary>
    /// Adds OpenTelemetry Prometheus scraping endpoint middleware to an
    /// <see cref="IApplicationBuilder"/> instance.
    /// </summary>
    /// <remarks>Note: A branched pipeline is created for the supplied
    /// <paramref name="predicate"/>.</remarks>
    /// <param name="app">The <see cref="IApplicationBuilder"/> to add
    /// middleware to.</param>
    /// <param name="predicate">Predicate for deciding if a given
    /// <see cref="HttpContext"/> should be branched.</param>
    /// <returns>A reference to the original <see
    /// cref="IApplicationBuilder"/> for chaining calls.</returns>
    public static IApplicationBuilder UseOpenTelemetryPrometheusScrapingEndpoint(this IApplicationBuilder app, Func<HttpContext, bool> predicate)
    {
        Guard.ThrowIfNull(predicate);
        return UseOpenTelemetryPrometheusScrapingEndpoint(app, meterProvider: null, predicate: predicate, path: null, configureBranchedPipeline: null, optionsName: null);
    }

    /// <summary>
    /// Adds OpenTelemetry Prometheus scraping endpoint middleware to an
    /// <see cref="IApplicationBuilder"/> instance.
    /// </summary>
    /// <remarks>Note: A branched pipeline is created based on the <paramref
    /// name="predicate"/> or <paramref name="path"/>. If neither <paramref
    /// name="predicate"/> nor <paramref name="path"/> are provided then
    /// <see cref="PrometheusAspNetCoreOptions.ScrapeEndpointPath"/> is
    /// used.</remarks>
    /// <param name="app">The <see cref="IApplicationBuilder"/> to add
    /// middleware to.</param>
    /// <param name="meterProvider">Optional <see cref="MeterProvider"/>
    /// containing a Prometheus exporter otherwise the primary SDK provider
    /// will be resolved using application services.</param>
    /// <param name="predicate">Optional predicate for deciding if a given
    /// <see cref="HttpContext"/> should be branched. If supplied <paramref
    /// name="path"/> is ignored.</param>
    /// <param name="path">Optional path to use for the branched pipeline.
    /// Ignored if <paramref name="predicate"/> is supplied.</param>
    /// <param name="configureBranchedPipeline">Optional callback to
    /// configure the branched pipeline. Called before registration of the
    /// Prometheus middleware.</param>
    /// <param name="optionsName">Optional name used to retrieve <see
    /// cref="PrometheusAspNetCoreOptions"/>.</param>
    /// <returns>A reference to the original <see
    /// cref="IApplicationBuilder"/> for chaining calls.</returns>
    public static IApplicationBuilder UseOpenTelemetryPrometheusScrapingEndpoint(
        this IApplicationBuilder app,
        MeterProvider? meterProvider,
        Func<HttpContext, bool>? predicate,
        string? path,
        Action<IApplicationBuilder>? configureBranchedPipeline,
        string? optionsName)
    {
        // Note: Order is important here. MeterProvider is accessed before
        // GetOptions<PrometheusAspNetCoreOptions> so that any changes made to
        // PrometheusAspNetCoreOptions in deferred AddPrometheusExporter
        // configure actions are reflected.
        meterProvider ??= app.ApplicationServices.GetRequiredService<MeterProvider>();

        if (predicate == null)
        {
            if (path == null)
            {
                var options = app.ApplicationServices.GetRequiredService<IOptionsMonitor<PrometheusAspNetCoreOptions>>().Get(optionsName ?? Options.DefaultName);

                path = options.ScrapeEndpointPath ?? PrometheusAspNetCoreOptions.DefaultScrapeEndpointPath;
            }

            if (!path.StartsWith('/'))
            {
                path = $"/{path}";
            }

            return app.Map(
                new PathString(path),
                builder =>
                {
                    configureBranchedPipeline?.Invoke(builder);
                    builder.UseMiddleware<PrometheusExporterMiddleware>(meterProvider);
                });
        }

        return app.MapWhen(
            context => predicate(context),
            builder =>
            {
                configureBranchedPipeline?.Invoke(builder);
                builder.UseMiddleware<PrometheusExporterMiddleware>(meterProvider);
            });
    }
}

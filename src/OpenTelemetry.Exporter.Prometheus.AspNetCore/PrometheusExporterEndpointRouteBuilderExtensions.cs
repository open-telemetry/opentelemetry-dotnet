// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter;
using OpenTelemetry.Internal;
using OpenTelemetry.Metrics;

namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// Provides extension methods for <see cref="IEndpointRouteBuilder"/> to add
/// Prometheus scraping endpoint.
/// </summary>
public static class PrometheusExporterEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Adds OpenTelemetry Prometheus scraping endpoint middleware to an
    /// <see cref="IEndpointRouteBuilder"/> instance.
    /// </summary>
    /// <remarks>Note: A branched pipeline is created for the route
    /// specified by <see
    /// cref="PrometheusAspNetCoreOptions.ScrapeEndpointPath"/>.</remarks>
    /// <param name="endpoints">The <see cref="IEndpointRouteBuilder"/> to add
    /// middleware to.</param>
    /// <returns>A convention routes for the Prometheus scraping endpoint.</returns>
    public static IEndpointConventionBuilder MapPrometheusScrapingEndpoint(this IEndpointRouteBuilder endpoints)
        => MapPrometheusScrapingEndpoint(endpoints, path: null, meterProvider: null, configureBranchedPipeline: null, optionsName: null);

    /// <summary>
    /// Adds OpenTelemetry Prometheus scraping endpoint middleware to an
    /// <see cref="IEndpointRouteBuilder"/> instance.
    /// </summary>
    /// <param name="endpoints">The <see cref="IEndpointRouteBuilder"/> to add
    /// middleware to.</param>
    /// <param name="path">The path to use for the branched pipeline.</param>
    /// <returns>A convention routes for the Prometheus scraping endpoint.</returns>
    public static IEndpointConventionBuilder MapPrometheusScrapingEndpoint(this IEndpointRouteBuilder endpoints, string path)
    {
        Guard.ThrowIfNull(path);
        return MapPrometheusScrapingEndpoint(endpoints, path, meterProvider: null, configureBranchedPipeline: null, optionsName: null);
    }

    /// <summary>
    /// Adds OpenTelemetry Prometheus scraping endpoint middleware to an
    /// <see cref="IEndpointRouteBuilder"/> instance.
    /// </summary>
    /// <param name="endpoints">The <see cref="IEndpointRouteBuilder"/> to add
    /// middleware to.</param>
    /// <param name="path">Optional path to use for the branched pipeline.
    /// If not provided then <see cref="PrometheusAspNetCoreOptions.ScrapeEndpointPath"/>
    /// is used.</param>
    /// <param name="meterProvider">Optional <see cref="MeterProvider"/>
    /// containing a Prometheus exporter otherwise the primary SDK provider
    /// will be resolved using application services.</param>
    /// <param name="configureBranchedPipeline">Optional callback to
    /// configure the branched pipeline. Called before registration of the
    /// Prometheus middleware.</param>
    /// <param name="optionsName">Optional name used to retrieve <see
    /// cref="PrometheusAspNetCoreOptions"/>.</param>
    /// <returns>A convention routes for the Prometheus scraping endpoint.</returns>
    public static IEndpointConventionBuilder MapPrometheusScrapingEndpoint(
        this IEndpointRouteBuilder endpoints,
        string? path,
        MeterProvider? meterProvider,
        Action<IApplicationBuilder>? configureBranchedPipeline,
        string? optionsName)
    {
        Guard.ThrowIfNull(endpoints);

        var builder = endpoints.CreateApplicationBuilder();

        // Note: Order is important here. MeterProvider is accessed before
        // GetOptions<PrometheusAspNetCoreOptions> so that any changes made to
        // PrometheusAspNetCoreOptions in deferred AddPrometheusExporter
        // configure actions are reflected.
        meterProvider ??= endpoints.ServiceProvider.GetRequiredService<MeterProvider>();

        if (path == null)
        {
            var options = endpoints.ServiceProvider.GetRequiredService<IOptionsMonitor<PrometheusAspNetCoreOptions>>().Get(optionsName ?? Options.DefaultName);

            path = options.ScrapeEndpointPath ?? PrometheusAspNetCoreOptions.DefaultScrapeEndpointPath;
        }

#if NET11_0_OR_GREATER
        if (!path.StartsWith('/', StringComparison.Ordinal))
#else
        if (!path.StartsWith('/'))
#endif
        {
            path = $"/{path}";
        }

        configureBranchedPipeline?.Invoke(builder);

        builder.UseMiddleware<PrometheusExporterMiddleware>(meterProvider);

        return endpoints.Map(new PathString(path), builder.Build());
    }
}

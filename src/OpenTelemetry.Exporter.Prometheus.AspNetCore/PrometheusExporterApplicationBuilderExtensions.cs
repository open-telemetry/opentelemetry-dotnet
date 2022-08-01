// <copyright file="PrometheusExporterApplicationBuilderExtensions.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

#if NETCOREAPP3_1_OR_GREATER

using System;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Exporter.Prometheus;
using OpenTelemetry.Internal;
using OpenTelemetry.Metrics;

namespace Microsoft.AspNetCore.Builder
{
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
        /// cref="PrometheusExporterOptions.ScrapeEndpointPath"/>.</remarks>
        /// <param name="app">The <see cref="IApplicationBuilder"/> to add
        /// middleware to.</param>
        /// <returns>A reference to the original <see
        /// cref="IApplicationBuilder"/> for chaining calls.</returns>
        public static IApplicationBuilder UseOpenTelemetryPrometheusScrapingEndpoint(this IApplicationBuilder app)
            => UseOpenTelemetryPrometheusScrapingEndpoint(app, meterProvider: null, predicate: null, path: null, configureBranchedPipeline: null);

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
            return UseOpenTelemetryPrometheusScrapingEndpoint(app, meterProvider: null, predicate: null, path: path, configureBranchedPipeline: null);
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
            return UseOpenTelemetryPrometheusScrapingEndpoint(app, meterProvider: null, predicate: predicate, path: null, configureBranchedPipeline: null);
        }

        /// <summary>
        /// Adds OpenTelemetry Prometheus scraping endpoint middleware to an
        /// <see cref="IApplicationBuilder"/> instance.
        /// </summary>
        /// <remarks>Note: A branched pipeline is created based on the <paramref
        /// name="predicate"/> or <paramref name="path"/>. If neither <paramref
        /// name="predicate"/> nor <paramref name="path"/> are provided then
        /// <see cref="PrometheusExporterOptions.ScrapeEndpointPath"/> is
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
        /// <returns>A reference to the original <see
        /// cref="IApplicationBuilder"/> for chaining calls.</returns>
        public static IApplicationBuilder UseOpenTelemetryPrometheusScrapingEndpoint(
            this IApplicationBuilder app,
            MeterProvider meterProvider,
            Func<HttpContext, bool> predicate,
            string path,
            Action<IApplicationBuilder> configureBranchedPipeline)
        {
            // Note: Order is important here. MeterProvider is accessed before
            // GetOptions<PrometheusExporterOptions> so that any changes made to
            // PrometheusExporterOptions in deferred AddPrometheusExporter
            // configure actions are reflected.
            meterProvider ??= app.ApplicationServices.GetRequiredService<MeterProvider>();

            if (predicate == null)
            {
                if (path == null)
                {
                    var options = app.ApplicationServices.GetOptions<PrometheusExporterOptions>();

                    path = options.ScrapeEndpointPath ?? PrometheusExporterOptions.DefaultScrapeEndpointPath;
                }

                if (!path.StartsWith("/"))
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
}
#endif

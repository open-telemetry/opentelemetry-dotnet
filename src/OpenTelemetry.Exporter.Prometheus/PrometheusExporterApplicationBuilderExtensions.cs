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
using OpenTelemetry.Exporter;
using OpenTelemetry.Exporter.Prometheus;
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
        /// <param name="meterProvider">Optional <see cref="MeterProvider"/>
        /// containing a <see cref="PrometheusExporter"/> otherwise the primary
        /// SDK provider will be resolved using application services.</param>
        /// <param name="predicate">Optional predicate for deciding if a given
        /// <see cref="HttpContext"/> should be branched.</param>
        /// <param name="configureBranchedPipeline">Optional callback to
        /// configure the branched pipeline. Called before registration of the
        /// Prometheus middleware.</param>
        /// <returns>A reference to the original <see
        /// cref="IApplicationBuilder"/> for chaining calls.</returns>
        public static IApplicationBuilder UseOpenTelemetryPrometheusScrapingEndpoint(
            this IApplicationBuilder app,
            MeterProvider meterProvider = null,
            Func<HttpContext, bool> predicate = null,
            Action<IApplicationBuilder> configureBranchedPipeline = null)
        {
            var options = app.ApplicationServices.GetOptions<PrometheusExporterOptions>();

            string path = options.ScrapeEndpointPath ?? PrometheusExporterOptions.DefaultScrapeEndpointPath;
            if (!path.StartsWith("/"))
            {
                path = $"/{path}";
            }

            meterProvider ??= app.ApplicationServices.GetRequiredService<MeterProvider>();

            if (predicate != null)
            {
                app.MapWhen(
                    context => predicate(context) && context.Request.Path == path,
                    builder =>
                    {
                        configureBranchedPipeline?.Invoke(builder);
                        builder.UseMiddleware<PrometheusExporterMiddleware>(meterProvider);
                    });
            }
            else
            {
                app.Map(
                    new PathString(path),
                    builder =>
                    {
                        configureBranchedPipeline?.Invoke(builder);
                        builder.UseMiddleware<PrometheusExporterMiddleware>(meterProvider);
                    });
            }

            return app;
        }
    }
}
#endif

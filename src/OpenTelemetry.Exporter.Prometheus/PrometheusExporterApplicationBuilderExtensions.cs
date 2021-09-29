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
    /// Provides extension methods for <see cref="IApplicationBuilder"/> to add Prometheus Scraper Endpoint.
    /// </summary>
    public static class PrometheusExporterApplicationBuilderExtensions
    {
        /// <summary>
        /// Adds OpenTelemetry Prometheus scraping endpoint middleware to an
        /// <see cref="IApplicationBuilder"/> instance.
        /// </summary>
        /// <param name="app">The <see cref="IApplicationBuilder"/> to add
        /// middleware to.</param>
        /// <param name="meterProvider">Optional <see cref="MeterProvider"/>
        /// containing a <see cref="PrometheusExporter"/> otherwise the primary
        /// SDK provider will be resolved using application services.</param>
        /// <returns>A reference to the <see cref="IApplicationBuilder"/> instance after the operation has completed.</returns>
        public static IApplicationBuilder UseOpenTelemetryPrometheusScrapingEndpoint(this IApplicationBuilder app, MeterProvider meterProvider = null)
        {
            var options = app.ApplicationServices.GetOptions<PrometheusExporterOptions>();

            string path = options.ScrapeEndpointPath ?? PrometheusExporterOptions.DefaultScrapeEndpointPath;
            if (!path.StartsWith("/"))
            {
                path = $"/{path}";
            }

            return app.Map(
                new PathString(path),
                builder => builder.UseMiddleware<PrometheusExporterMiddleware>(meterProvider ?? app.ApplicationServices.GetRequiredService<MeterProvider>()));
        }
    }
}
#endif

// <copyright file="PrometheusRouteBuilderExtensions.cs" company="OpenTelemetry Authors">
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

#if NETSTANDARD2_0

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace OpenTelemetry.Exporter.Prometheus
{
    /// <summary>
    /// Provides extension methods for <see cref="IApplicationBuilder"/> to add Prometheus Scraper Endpoint.
    /// </summary>
    public static class PrometheusRouteBuilderExtensions
    {
        private const string DefaultPath = "/metrics";

        /// <summary>
        /// Use prometheus extension.
        /// </summary>
        /// <param name="app">The <see cref="IApplicationBuilder"/> to add middleware to.</param>
        /// <returns>A reference to the <see cref="IApplicationBuilder"/> instance after the operation has completed.</returns>
        public static IApplicationBuilder UsePrometheus(this IApplicationBuilder app)
        {
            var options = app.ApplicationServices.GetService(typeof(PrometheusExporterOptions)) as PrometheusExporterOptions;
            var path = new PathString(options?.Url ?? DefaultPath);
            return app.Map(
                new PathString(path),
                builder => builder.UseMiddleware<PrometheusExporterMiddleware>());
        }
    }
}
#endif

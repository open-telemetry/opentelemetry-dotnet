// <copyright file="PrometheusExporterMiddleware.cs" company="OpenTelemetry Authors">
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

using System;
using System.Threading.Tasks;

#if NETSTANDARD2_0
using Microsoft.AspNetCore.Http;

namespace OpenTelemetry.Exporter.Prometheus
{
    /// <summary>
    /// A middleware used to expose Prometheus metrics.
    /// </summary>
    public class PrometheusExporterMiddleware
    {
        private readonly RequestDelegate next;
        private readonly PrometheusExporter exporter;

        /// <summary>
        /// Initializes a new instance of the <see cref="PrometheusExporterMiddleware"/> class.
        /// </summary>
        /// <param name="next">The next middleware in the pipeline.</param>
        /// <param name="exporter">The <see cref="PrometheusExporter"/> instance.</param>
        public PrometheusExporterMiddleware(RequestDelegate next, PrometheusExporter exporter)
        {
            this.next = next ?? throw new ArgumentNullException(nameof(next));
            this.exporter = exporter ?? throw new ArgumentNullException(nameof(exporter));
        }

        /// <summary>
        /// Invoke.
        /// </summary>
        /// <param name="httpContext"> context. </param>
        /// <returns>Task. </returns>
        public async Task InvokeAsync(HttpContext httpContext)
        {
            if (httpContext is null)
            {
                throw new ArgumentNullException(nameof(httpContext));
            }

            var result = this.exporter.GetMetricsCollection();

            await httpContext.Response.WriteAsync(result).ConfigureAwait(false);
        }
    }
}
#endif

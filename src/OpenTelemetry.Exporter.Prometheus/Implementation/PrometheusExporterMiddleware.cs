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

#if NETCOREAPP3_1_OR_GREATER
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using OpenTelemetry.Internal;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Exporter.Prometheus
{
    /// <summary>
    /// ASP.NET Core middleware for exposing a Prometheus metrics scraping endpoint.
    /// </summary>
    internal sealed class PrometheusExporterMiddleware
    {
        private readonly PrometheusExporter exporter;

        /// <summary>
        /// Initializes a new instance of the <see cref="PrometheusExporterMiddleware"/> class.
        /// </summary>
        /// <param name="meterProvider"><see cref="MeterProvider"/>.</param>
        /// <param name="next"><see cref="RequestDelegate"/>.</param>
        public PrometheusExporterMiddleware(MeterProvider meterProvider, RequestDelegate next)
        {
            Guard.Null(meterProvider, nameof(meterProvider));

            if (!meterProvider.TryFindExporter(out PrometheusExporter exporter))
            {
                throw new ArgumentException("A PrometheusExporter could not be found configured on the provided MeterProvider.");
            }

            this.exporter = exporter;
        }

        internal PrometheusExporterMiddleware(PrometheusExporter exporter)
        {
            this.exporter = exporter;
        }

        /// <summary>
        /// Invoke.
        /// </summary>
        /// <param name="httpContext"> context.</param>
        /// <returns>Task.</returns>
        public async Task InvokeAsync(HttpContext httpContext)
        {
            Debug.Assert(httpContext != null, "httpContext should not be null");

            var response = httpContext.Response;

            try
            {
                var data = await this.exporter.CollectionManager.EnterCollect().ConfigureAwait(false);
                try
                {
                    if (data.Count > 0)
                    {
                        response.StatusCode = 200;
                        response.ContentType = "text/plain; charset=utf-8; version=0.0.4";

                        await response.Body.WriteAsync(data.Array, 0, data.Count).ConfigureAwait(false);
                    }
                    else
                    {
                        throw new InvalidOperationException("Collection failure.");
                    }
                }
                finally
                {
                    this.exporter.CollectionManager.ExitCollect();
                }
            }
            catch (Exception ex)
            {
                PrometheusExporterEventSource.Log.FailedExport(ex);
                if (!response.HasStarted)
                {
                    response.StatusCode = 500;
                }
            }

            this.exporter.OnExport = null;
        }
    }
}
#endif

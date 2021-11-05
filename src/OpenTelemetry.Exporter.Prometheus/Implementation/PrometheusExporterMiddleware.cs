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
using System.Runtime.CompilerServices;
using System.Threading;
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

        /// <summary>
        /// Invoke.
        /// </summary>
        /// <param name="httpContext"> context.</param>
        /// <returns>Task.</returns>
        public async Task InvokeAsync(HttpContext httpContext)
        {
            Debug.Assert(httpContext != null, "httpContext should not be null");

            var response = httpContext.Response;

            if (!this.exporter.TryEnterSemaphore())
            {
                response.StatusCode = 429;
                return;
            }

            try
            {
                this.exporter.Collect(Timeout.Infinite);

                await WriteMetricsToResponse(this.exporter, response).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (!response.HasStarted)
                {
                    response.StatusCode = 500;
                }

                PrometheusExporterEventSource.Log.FailedExport(ex);
            }
            finally
            {
                this.exporter.ReleaseSemaphore();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static async Task WriteMetricsToResponse(PrometheusExporter exporter, HttpResponse response)
        {
            response.StatusCode = 200;
            response.ContentType = PrometheusMetricsFormatHelper.ContentType;

            await exporter.WriteMetricsCollection(response.Body, exporter.Options.GetUtcNowDateTimeOffset).ConfigureAwait(false);
        }
    }
}
#endif

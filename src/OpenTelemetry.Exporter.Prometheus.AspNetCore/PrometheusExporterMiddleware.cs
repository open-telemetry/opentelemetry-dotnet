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

using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using OpenTelemetry.Exporter.Prometheus;
using OpenTelemetry.Exporter.Prometheus.AspNetCore;
using OpenTelemetry.Internal;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Exporter;

/// <summary>
/// ASP.NET Core middleware for exposing a Prometheus metrics scraping endpoint.
/// </summary>
internal sealed class PrometheusExporterMiddleware
{
    private const string OpenMetricsMediaType = "application/openmetrics-text";

    private readonly PrometheusExporter exporter;

    /// <summary>
    /// Initializes a new instance of the <see cref="PrometheusExporterMiddleware"/> class.
    /// </summary>
    /// <param name="meterProvider"><see cref="MeterProvider"/>.</param>
    /// <param name="next"><see cref="RequestDelegate"/>.</param>
    public PrometheusExporterMiddleware(MeterProvider meterProvider, RequestDelegate next)
    {
        Guard.ThrowIfNull(meterProvider);

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
            var openMetricsRequested = this.AcceptsOpenMetrics(httpContext.Request);
            var collectionResponse = await this.exporter.CollectionManager.EnterCollect(openMetricsRequested).ConfigureAwait(false);

            try
            {
                if (collectionResponse.View.Count > 0)
                {
                    response.StatusCode = 200;
#if NET8_0_OR_GREATER
                    response.Headers.Append("Last-Modified", collectionResponse.GeneratedAtUtc.ToString("R"));
#else
                    response.Headers.Add("Last-Modified", collectionResponse.GeneratedAtUtc.ToString("R"));
#endif
                    response.ContentType = openMetricsRequested
                        ? "application/openmetrics-text; version=1.0.0; charset=utf-8"
                        : "text/plain; charset=utf-8; version=0.0.4";

                    await response.Body.WriteAsync(collectionResponse.View.Array, 0, collectionResponse.View.Count).ConfigureAwait(false);
                }
                else
                {
                    // It's not expected to have no metrics to collect, but it's not necessarily a failure, either.
                    response.StatusCode = 200;
                    PrometheusExporterEventSource.Log.NoMetrics();
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

    private bool AcceptsOpenMetrics(HttpRequest request)
    {
        var acceptHeader = request.Headers.Accept;

        if (StringValues.IsNullOrEmpty(acceptHeader))
        {
            return false;
        }

        foreach (var accept in acceptHeader)
        {
            var value = accept.AsSpan();

            while (value.Length > 0)
            {
                var headerValue = value.SplitNext(',');
                var mediaType = headerValue.SplitNext(';');

                if (mediaType.Equals(OpenMetricsMediaType, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }
}

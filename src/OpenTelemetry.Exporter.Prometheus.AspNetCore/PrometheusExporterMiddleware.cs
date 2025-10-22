// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using OpenTelemetry.Exporter.Prometheus;
using OpenTelemetry.Internal;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Exporter;

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
        Guard.ThrowIfNull(meterProvider);
        Guard.ThrowIfNull(next);

        if (!meterProvider.TryFindExporter(out PrometheusExporter? exporter))
        {
            throw new ArgumentException("A PrometheusExporter could not be found configured on the provided MeterProvider.");
        }

        this.exporter = exporter;
    }

    internal PrometheusExporterMiddleware(PrometheusExporter exporter)
    {
        Debug.Assert(exporter != null, "exporter was null");

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
            var openMetricsRequested = AcceptsOpenMetrics(httpContext.Request);
            var collectionResponse = await this.exporter.CollectionManager.EnterCollect(openMetricsRequested, CancellationToken.None).ConfigureAwait(false);

            try
            {
                var dataView = openMetricsRequested ? collectionResponse.OpenMetricsView : collectionResponse.PlainTextView;

                if (dataView.Count > 0)
                {
                    response.StatusCode = 200;
#if NET
                    response.Headers.Append("Last-Modified", collectionResponse.GeneratedAtUtc.ToString("R"));
#else
                    response.Headers.Add("Last-Modified", collectionResponse.GeneratedAtUtc.ToString("R"));
#endif
                    response.ContentType = openMetricsRequested
                        ? "application/openmetrics-text; version=1.0.0; charset=utf-8"
                        : "text/plain; charset=utf-8; version=0.0.4";

                    await response.Body.WriteAsync(dataView.Array.AsMemory(0, dataView.Count)).ConfigureAwait(false);
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
    }

    private static bool AcceptsOpenMetrics(HttpRequest request)
    {
        var acceptHeader = request.Headers.Accept;

        if (StringValues.IsNullOrEmpty(acceptHeader))
        {
            return false;
        }

        foreach (var header in acceptHeader)
        {
            if (PrometheusHeadersParser.AcceptsOpenMetrics(header))
            {
                return true;
            }
        }

        return false;
    }
}

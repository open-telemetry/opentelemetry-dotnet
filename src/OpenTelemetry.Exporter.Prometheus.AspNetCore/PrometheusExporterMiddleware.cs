// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using OpenTelemetry.Exporter.Prometheus;
using OpenTelemetry.Internal;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Exporter;

/// <summary>
/// ASP.NET Core middleware for exposing a Prometheus metrics scraping endpoint.
/// </summary>
internal sealed class PrometheusExporterMiddleware
{
    private const string OpenMetricsEscapingScheme = "underscores";
    private const string OpenMetricsMediaType = "application/openmetrics-text";
    private const string OpenMetricsVersion = "1.0.0";
    private const string OpenMetricsContentType = $"application/openmetrics-text; version={OpenMetricsVersion}; charset=utf-8; escaping={OpenMetricsEscapingScheme}";

    private const string PrometheusTextMediaType = "text/plain";

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

    public async Task InvokeAsync(HttpContext httpContext)
    {
        Debug.Assert(httpContext != null, "httpContext should not be null");

        var response = httpContext.Response;

        try
        {
            using var requestCancelled = new CancellationTokenSource();

            int? scrapeTimeoutSeconds = null;
            if (httpContext.Request.Headers.TryGetValue("X-Prometheus-Scrape-Timeout-Seconds", out var value) &&
                int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedValue) &&
                parsedValue is > 0 and < int.MaxValue / 1_000)
            {
                scrapeTimeoutSeconds = parsedValue;
                requestCancelled.CancelAfter(TimeSpan.FromSeconds(scrapeTimeoutSeconds.Value));
            }

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(requestCancelled.Token, httpContext.RequestAborted);

            var openMetricsRequested = AcceptsOpenMetrics(httpContext.Request);
            var collectionResponse = await this.exporter.CollectionManager.EnterCollect(openMetricsRequested).ConfigureAwait(false);

            try
            {
                linkedCts.Token.ThrowIfCancellationRequested();

                var dataView = openMetricsRequested ? collectionResponse.OpenMetricsView : collectionResponse.PlainTextView;

                response.StatusCode = StatusCodes.Status200OK;

                if (dataView.Count > 0)
                {
                    response.Headers.Append("Last-Modified", collectionResponse.GeneratedAtUtc.ToString("R"));

                    response.ContentType = openMetricsRequested
                        ? OpenMetricsContentType
                        : "text/plain; charset=utf-8; version=0.0.4";

                    await response.Body.WriteAsync(dataView.Array.AsMemory(0, dataView.Count), linkedCts.Token).ConfigureAwait(false);
                }
                else
                {
                    // It's not expected to have no metrics to collect, but it's not necessarily a failure, either.
                    PrometheusExporterEventSource.Log.NoMetrics();
                }
            }
            catch (OperationCanceledException ex) when (ex.CancellationToken == linkedCts.Token)
            {
                if (scrapeTimeoutSeconds is { } timeout)
                {
                    PrometheusExporterEventSource.Log.ScrapeTimedOut(timeout);
                }

                if (!response.HasStarted)
                {
                    response.StatusCode = StatusCodes.Status408RequestTimeout;
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
                response.StatusCode = StatusCodes.Status500InternalServerError;
            }
        }
    }

    internal static bool AcceptsOpenMetrics(HttpRequest request)
    {
        var acceptHeader = request.GetTypedHeaders().Accept;

        if (acceptHeader is not { Count: > 0 })
        {
            return false;
        }

        double? bestOpenMetricsQuality = null;
        double? bestPrometheusQuality = null;

        foreach (var mediaType in acceptHeader)
        {
            var quality = mediaType.Quality ?? 1.0;

            if (quality is <= 0 or > 1)
            {
                continue;
            }

            if (string.Equals(mediaType.MediaType.Value, OpenMetricsMediaType, StringComparison.OrdinalIgnoreCase) &&
                HasSupportedOpenMetricsParameters(mediaType))
            {
                bestOpenMetricsQuality =
                    bestOpenMetricsQuality is not { } comparison || quality > comparison ?
                    quality :
                    bestOpenMetricsQuality ?? quality;
            }
            else if (string.Equals(mediaType.MediaType.Value, PrometheusTextMediaType, StringComparison.OrdinalIgnoreCase))
            {
                bestPrometheusQuality =
                    bestPrometheusQuality is not { } comparison || quality > comparison ?
                    quality :
                    bestPrometheusQuality ?? quality;
            }
        }

        return bestOpenMetricsQuality is { } openMetricsQuality &&
               (bestPrometheusQuality is not { } prometheusQuality || openMetricsQuality >= prometheusQuality);
    }

    private static bool HasSupportedOpenMetricsParameters(MediaTypeHeaderValue value)
    {
        var hasSupportedOpenMetricsEscaping = true;
        var hasSupportedOpenMetricsVersion = true;

        foreach (var parameter in value.Parameters)
        {
            if (string.Equals(parameter.Name.Value, "version", StringComparison.OrdinalIgnoreCase))
            {
                hasSupportedOpenMetricsVersion = string.Equals(parameter.Value.Value?.Trim('"'), OpenMetricsVersion, StringComparison.Ordinal);
            }
            else if (string.Equals(parameter.Name.Value, "escaping", StringComparison.OrdinalIgnoreCase))
            {
                hasSupportedOpenMetricsEscaping = string.Equals(parameter.Value.Value?.Trim('"'), OpenMetricsEscapingScheme, StringComparison.Ordinal);
            }
        }

        return hasSupportedOpenMetricsVersion && hasSupportedOpenMetricsEscaping;
    }
}

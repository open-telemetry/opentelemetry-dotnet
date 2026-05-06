// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
            var protocol = Negotiate(httpContext.Request);

            var collectionResponse = await this.exporter.CollectionManager.EnterCollect(protocol.IsOpenMetrics).ConfigureAwait(false);

            try
            {
                var dataView = protocol.IsOpenMetrics ? collectionResponse.OpenMetricsView : collectionResponse.PlainTextView;

                response.StatusCode = StatusCodes.Status200OK;

                if (dataView.Count > 0)
                {
                    response.Headers.Append("Last-Modified", collectionResponse.GeneratedAtUtc.ToString("R"));
                    response.ContentType = PrometheusProtocol.GetContentType(protocol);

                    await response.Body.WriteAsync(dataView.Array.AsMemory(0, dataView.Count)).ConfigureAwait(false);
                }
                else
                {
                    // It's not expected to have no metrics to collect, but it's not necessarily a failure, either.
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
                response.StatusCode = StatusCodes.Status500InternalServerError;
            }
        }
    }

    internal static PrometheusProtocol Negotiate(HttpRequest request)
    {
        var acceptHeader = request.GetTypedHeaders().Accept;

        if (acceptHeader is not { Count: > 0 })
        {
            return PrometheusProtocol.Fallback;
        }

        if (acceptHeader is { Count: 1 })
        {
            return TryParse(acceptHeader[0], out var protocol, out _)
                ? protocol.GetValueOrDefault(PrometheusProtocol.Fallback)
                : PrometheusProtocol.Fallback;
        }

        const int SupportedProtocols = 4;
        var preferences = new PriorityQueue<PrometheusProtocol, double>(SupportedProtocols);

        foreach (var mediaType in acceptHeader)
        {
            if (TryParse(mediaType, out var protocol, out var quality))
            {
                preferences.Enqueue(protocol.Value, -quality);
            }
        }

        // Use the first supported protocol that was parsed that has the highest quality factor
        return preferences.TryDequeue(out var preferred, out _)
            ? preferred
            : PrometheusProtocol.Fallback;
    }

    private static bool TryParse(
        MediaTypeHeaderValue value,
        [NotNullWhen(true)] out PrometheusProtocol? protocol,
        out double quality)
    {
        protocol = null;
        quality = default;

        bool isOpenMetrics;
        string mediaType;

        var supportedEscapingSchemes = PrometheusProtocol.SupportedEscapingSchemes;
        HashSet<Version> supportedVersions;

        if (string.Equals(value.MediaType.Value, PrometheusProtocol.OpenMetricsMediaType, StringComparison.OrdinalIgnoreCase))
        {
            isOpenMetrics = true;
            mediaType = PrometheusProtocol.OpenMetricsMediaType;
            supportedVersions = PrometheusProtocol.SupportedOpenMetricsVersions;
        }
        else if (string.Equals(value.MediaType.Value, PrometheusProtocol.PrometheusTextMediaType, StringComparison.OrdinalIgnoreCase))
        {
            isOpenMetrics = false;
            mediaType = PrometheusProtocol.PrometheusTextMediaType;
            supportedVersions = PrometheusProtocol.SupportedPrometheusVersions;
        }
        else
        {
            // Unsupported media type
            return false;
        }

        // Quality ignores values greater than one and returns null so we cannot
        // distinguish between an invalid quality and the quality not be provided.
        // Default to a value of 0.99 so that an invalid quality value will be treated
        // as a lower preference than a valid quality value of 1.0.
        quality = value.Quality ?? 0.99;

        if (quality <= 0)
        {
            return false;
        }

        string? escaping = null;
        Version? version = null;

        foreach (var parameter in value.Parameters)
        {
            if (string.Equals(parameter.Name.Value, "version", StringComparison.OrdinalIgnoreCase))
            {
                if (Version.TryParse(parameter.Value.Value?.Trim('"'), out var parsedVersion) &&
                    supportedVersions.Contains(parsedVersion))
                {
                    version = parsedVersion;
                }
                else
                {
                    // Unsupported version
                    return false;
                }
            }
            else if (string.Equals(parameter.Name.Value, "escaping", StringComparison.OrdinalIgnoreCase))
            {
                var escapedValue = parameter.Value.Value?.Trim('"');

                if (escapedValue == null || !supportedEscapingSchemes.Contains(escapedValue))
                {
                    // TODO Support other escaping schemes, including at least "allow-utf-8".
                    // For now we treat "allow-utf-8" as if it were "underscores" to avoid fallback
                    // to PrometheusText0.0.4 where it would previously match to OpenMetricsText1.0.0.
                    // See https://github.com/open-telemetry/opentelemetry-dotnet/issues/7246.
                    if (string.Equals(escapedValue, PrometheusProtocol.AllowUtf8Escaping, StringComparison.Ordinal))
                    {
                        escaping = PrometheusProtocol.UnderscoresEscaping;
                    }
                    else
                    {
                        // Unsupported escaping scheme
                        return false;
                    }
                }
                else
                {
                    escaping = escapedValue;
                }
            }
        }

        if (version is null)
        {
            // Use the oldest version if no version preference was specified
            version = isOpenMetrics ? PrometheusProtocol.OpenMetricsV0 : PrometheusProtocol.PrometheusVersion0;
        }
        else if (version.Major is not > 0)
        {
            // From https://prometheus.io/docs/instrumenting/content_negotiation/#content-type-response:
            // "The Content-Type header MUST include [...] For text formats version 1.0.0 and above, the escaping scheme parameter."
            escaping = null;
        }
        else
        {
            escaping ??= PrometheusProtocol.UnderscoresEscaping;
        }

        protocol = new(mediaType, escaping, version, isOpenMetrics);
        return true;
    }
}

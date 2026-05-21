// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter;

/// <summary>
/// Contains HTTP client name constants for the OpenTelemetry Protocol (OTLP) exporter.
/// </summary>
public static class OtlpExporterHttpClientConstants
{
    /// <summary>The <see cref="System.Net.Http.HttpClient"/> name used for the OTLP trace exporter.</summary>
    public const string TraceExporterHttpClientName = "OtlpTraceExporter";

    /// <summary>The <see cref="System.Net.Http.HttpClient"/> name used for the OTLP log exporter.</summary>
    public const string LogExporterHttpClientName = "OtlpLogExporter";

    /// <summary>The <see cref="System.Net.Http.HttpClient"/> name used for the OTLP metric exporter.</summary>
    public const string MetricExporterHttpClientName = "OtlpMetricExporter";
}

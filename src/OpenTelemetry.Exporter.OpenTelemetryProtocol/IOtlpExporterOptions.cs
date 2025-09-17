// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Net.Http;
#endif

namespace OpenTelemetry.Exporter;

/// <summary>
/// Describes the OpenTelemetry Protocol (OTLP) exporter options shared by all
/// signals.
/// </summary>
internal interface IOtlpExporterOptions
{
    /// <summary>
    /// Gets or sets the OTLP transport protocol.
    /// </summary>
    OtlpExportProtocol Protocol { get; set; }

    /// <summary>
    /// Gets or sets the target to which the exporter is going to send
    /// telemetry.
    /// </summary>
    /// <remarks>
    /// Notes:
    /// <list type="bullet">
    /// <item>When setting <see cref="Endpoint"/> the value must be a valid <see
    /// cref="Uri"/> with scheme (http or https) and host, and may contain a
    /// port and path.</item>
    /// <item>The default value when not set is based on the <see
    /// cref="Protocol"/> property:
    /// <list type="bullet">
    /// <item><c>http://localhost:4317</c> for <see
    /// cref="OtlpExportProtocol.Grpc"/>.</item>
    /// <item><c>http://localhost:4318</c> for <see
    /// cref="OtlpExportProtocol.HttpProtobuf"/></item>.
    /// </list>
    /// <item>When <see cref="Protocol"/> is set to <see
    /// cref="OtlpExportProtocol.HttpProtobuf"/> and <see cref="Endpoint"/> has
    /// not been set the default value (<c>http://localhost:4318</c>) will have
    /// a signal-specific path appended. The final default endpoint values will
    /// be constructed as:
    /// <list type="bullet">
    /// <item>Logging: <c>http://localhost:4318/v1/logs</c></item>
    /// <item>Metrics: <c>http://localhost:4318/v1/metrics</c></item>
    /// <item>Tracing: <c>http://localhost:4318/v1/traces</c></item>
    /// </list>
    /// </item>
    /// </item>
    /// </list>
    /// </remarks>
    Uri Endpoint { get; set; }

    /// <summary>
    /// Gets or sets optional headers for the connection.
    /// </summary>
    /// <remarks>
    /// Note: Refer to the  <see
    /// href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/protocol/exporter.md#specifying-headers-via-environment-variables">
    /// OpenTelemetry Specification</see> for details on the format of <see
    /// cref="Headers"/>.
    /// </remarks>
    string? Headers { get; set; }

    /// <summary>
    /// Gets or sets the max waiting time (in milliseconds) for the backend to
    /// process each batch. Default value: <c>10000</c>.
    /// </summary>
    int TimeoutMilliseconds { get; set; }

    /// <summary>
    /// Gets or sets the factory function called to create the <see
    /// cref="HttpClient"/> instance that will be used at runtime to
    /// transmit telemetry over HTTP. The returned instance will be reused
    /// for all export invocations.
    /// </summary>
    /// <remarks>
    /// Notes:
    /// <list type="bullet">
    /// <item>This is only invoked for the <see
    /// cref="OtlpExportProtocol.HttpProtobuf"/> protocol.</item>
    /// <item>The default behavior when using tracing registration extensions is
    /// if an <a
    /// href="https://docs.microsoft.com/dotnet/api/system.net.http.ihttpclientfactory">IHttpClientFactory</a>
    /// instance can be resolved through the application <see
    /// cref="IServiceProvider"/> then an <see cref="HttpClient"/> will be
    /// created through the factory with the name "OtlpTraceExporter" otherwise
    /// an <see cref="HttpClient"/> will be instantiated directly.</item>
    /// <item>The default behavior when using metrics registration extensions is
    /// if an <a
    /// href="https://docs.microsoft.com/dotnet/api/system.net.http.ihttpclientfactory">IHttpClientFactory</a>
    /// instance can be resolved through the application <see
    /// cref="IServiceProvider"/> then an <see cref="HttpClient"/> will be
    /// created through the factory with the name "OtlpMetricExporter" otherwise
    /// an <see cref="HttpClient"/> will be instantiated directly.</item>
    /// <item>
    /// The default behavior when using logging registration extensions is an
    /// <see cref="HttpClient"/> will be instantiated directly. <a
    /// href="https://docs.microsoft.com/dotnet/api/system.net.http.ihttpclientfactory">IHttpClientFactory</a>
    /// is not currently supported for logging.
    /// </item>
    /// </list>
    /// </remarks>
    Func<HttpClient> HttpClientFactory { get; set; }
}

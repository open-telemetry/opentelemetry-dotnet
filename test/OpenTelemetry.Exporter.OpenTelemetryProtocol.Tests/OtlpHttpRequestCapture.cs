// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Net.Http;
#endif
using OtlpLogs = OpenTelemetry.Proto.Logs.V1;
using OtlpLogsCollector = OpenTelemetry.Proto.Collector.Logs.V1;
using OtlpTrace = OpenTelemetry.Proto.Trace.V1;
using OtlpTraceCollector = OpenTelemetry.Proto.Collector.Trace.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

/// <summary>
/// Captures the last OTLP/HTTP protobuf request sent by an exporter configured with
/// <see cref="ConfigureExporter"/> or <see cref="ConfigureTransport"/>.
/// </summary>
internal sealed class OtlpHttpRequestCapture : IDisposable
{
    private readonly TestHttpMessageHandler handler = new();
    private readonly HttpClient httpClient;

    public OtlpHttpRequestCapture()
    {
        this.httpClient = new HttpClient(this.handler);
    }

    public void ConfigureExporter(OtlpExporterOptions options)
    {
        options.ExportProcessorType = ExportProcessorType.Simple;
        this.ConfigureTransport(options);
    }

    public void ConfigureTransport(IOtlpExporterOptions options)
    {
        options.Protocol = OtlpExportProtocol.HttpProtobuf;
        options.Endpoint = new Uri("http://localhost:4318");
        options.HttpClientFactory = () => this.httpClient;
    }

    public OtlpTrace.ScopeSpans GetSingleScopeSpans()
    {
        var request = OtlpTraceCollector.ExportTraceServiceRequest.Parser.ParseFrom(this.GetRequestContent());
        return Assert.Single(Assert.Single(request.ResourceSpans).ScopeSpans);
    }

    public OtlpLogs.LogRecord GetSingleLogRecord()
    {
        var request = OtlpLogsCollector.ExportLogsServiceRequest.Parser.ParseFrom(this.GetRequestContent());
        return Assert.Single(Assert.Single(Assert.Single(request.ResourceLogs).ScopeLogs).LogRecords);
    }

    public void Dispose()
    {
        this.httpClient.Dispose();
        this.handler.Dispose();
    }

    private byte[] GetRequestContent()
    {
        Assert.NotNull(this.handler.HttpRequestContent);
        return this.handler.HttpRequestContent;
    }
}

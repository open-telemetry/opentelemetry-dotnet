// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Google.Protobuf;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Collector.Metrics.V1;
using OpenTelemetry.Proto.Collector.Trace.V1;

namespace OpenTelemetry.Android.Tests;

/// <summary>
/// An in-process OTLP/HTTP receiver run on the test host. The Android test app,
/// running in the emulator, exports to this collector via the emulator's
/// <c>10.0.2.2</c> host-loopback alias. Decoded OTLP requests are captured so the
/// test can assert that traces, metrics and logs were exported by the SDK running
/// under the Android runtime.
/// </summary>
internal sealed class OtlpHttpCollector(WebApplication app) : IAsyncDisposable
{
    internal const int Port = 4318;

    private readonly Lock lockObject = new();
    private readonly List<ExportLogsServiceRequest> logsRequests = [];
    private readonly List<ExportMetricsServiceRequest> metricsRequests = [];
    private readonly List<ExportTraceServiceRequest> traceRequests = [];
    private readonly WebApplication app = app;
    private int rawLogHits;
    private int rawMetricHits;
    private int rawTraceHits;

    public static async Task<OtlpHttpCollector> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();

        builder.Logging.ClearProviders();

        // Bind on all interfaces so the emulator can reach the collector via
        // 10.0.2.2 (the alias for the host loopback).
        builder.WebHost.UseUrls($"http://0.0.0.0:{Port}");

        var app = builder.Build();

        var collector = new OtlpHttpCollector(app);

        app.MapPost("/v1/logs", collector.HandleLogsAsync);
        app.MapPost("/v1/metrics", collector.HandleMetricsAsync);
        app.MapPost("/v1/traces", collector.HandleTracesAsync);

        await app.StartAsync();

        return collector;
    }

    public IReadOnlyList<ExportLogsServiceRequest> GetLogsRequests()
    {
        lock (this.lockObject)
        {
            return [.. this.logsRequests];
        }
    }

    public IReadOnlyList<ExportMetricsServiceRequest> GetMetricsRequests()
    {
        lock (this.lockObject)
        {
            return [.. this.metricsRequests];
        }
    }

    public IReadOnlyList<ExportTraceServiceRequest> GetTraceRequests()
    {
        lock (this.lockObject)
        {
            return [.. this.traceRequests];
        }
    }

    public string GetRawHitSummary()
    {
        lock (this.lockObject)
        {
            return $"Raw endpoint hits: /v1/traces={this.rawTraceHits}, /v1/metrics={this.rawMetricHits}, /v1/logs={this.rawLogHits}.";
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (this.app is not null)
        {
            await this.app.StopAsync();
            await this.app.DisposeAsync();
        }
    }

    private static async Task<byte[]> ReadBodyAsync(HttpRequest request)
    {
        using var memory = new MemoryStream();

        await request.Body.CopyToAsync(memory);

        return memory.ToArray();
    }

    private static async Task WriteResponseAsync(HttpContext context, IMessage response)
    {
        context.Response.ContentType = "application/x-protobuf";
        await context.Response.Body.WriteAsync(response.ToByteArray());
    }

    private async Task HandleLogsAsync(HttpContext context)
    {
        var body = await ReadBodyAsync(context.Request);
        var request = ExportLogsServiceRequest.Parser.ParseFrom(body);

        lock (this.lockObject)
        {
            this.rawLogHits++;
            this.logsRequests.Add(request);
        }

        await WriteResponseAsync(context, new ExportLogsServiceResponse());
    }

    private async Task HandleMetricsAsync(HttpContext context)
    {
        var body = await ReadBodyAsync(context.Request);
        var request = ExportMetricsServiceRequest.Parser.ParseFrom(body);

        lock (this.lockObject)
        {
            this.rawMetricHits++;
            this.metricsRequests.Add(request);
        }

        await WriteResponseAsync(context, new ExportMetricsServiceResponse());
    }

    private async Task HandleTracesAsync(HttpContext context)
    {
        var body = await ReadBodyAsync(context.Request);
        var request = ExportTraceServiceRequest.Parser.ParseFrom(body);

        lock (this.lockObject)
        {
            this.rawTraceHits++;
            this.traceRequests.Add(request);
        }

        await WriteResponseAsync(context, new ExportTraceServiceResponse());
    }
}

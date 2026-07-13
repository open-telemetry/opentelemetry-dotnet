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
using OpenTelemetry.Tests;

namespace OpenTelemetry.BlazorWasm.Tests;

/// <summary>
/// An in-process OTLP/HTTP receiver that also serves the published Blazor
/// WebAssembly client from the same origin. Decoded OTLP requests are captured
/// so the test can assert that traces, metrics and logs were exported by the
/// SDK running under the browser WASM runtime.
/// </summary>
internal sealed class OtlpHttpCollector : IAsyncDisposable
{
    private readonly Lock lockObject = new();
    private readonly List<ExportLogsServiceRequest> logsRequests = [];
    private readonly List<ExportMetricsServiceRequest> metricsRequests = [];
    private readonly List<ExportTraceServiceRequest> traceRequests = [];
    private readonly WebApplication app;
    private int rawLogHits;
    private int rawMetricHits;
    private int rawTraceHits;

    private OtlpHttpCollector(WebApplication app, string baseUrl)
    {
        this.app = app;
        this.BaseUrl = baseUrl;
    }

    public string BaseUrl { get; }

    public static async Task<OtlpHttpCollector> StartAsync(string webRootPath)
    {
        var port = TcpPortProvider.GetOpenPort();
        var baseUrl = $"http://localhost:{port}";

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = Path.GetDirectoryName(webRootPath),
            WebRootPath = webRootPath,
        });

        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls(baseUrl);

        var app = builder.Build();

        var collector = new OtlpHttpCollector(app, baseUrl);

        // OTLP receiver endpoints. These must be mapped before the Blazor
        // fallback so the export requests are not served the index page.
        app.MapPost("/v1/logs", collector.HandleLogsAsync);
        app.MapPost("/v1/metrics", collector.HandleMetricsAsync);
        app.MapPost("/v1/traces", collector.HandleTracesAsync);

        // A simple endpoint the app's "Call HTTP endpoint" button targets so
        // that HTTP client instrumentation can be exercised from the browser.
        app.MapGet("/api/ping", () => Results.Text("pong"));

        // Serve the published Blazor WebAssembly client.
        app.UseBlazorFrameworkFiles();
        app.UseStaticFiles();
        app.MapFallbackToFile("index.html");

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

    public async ValueTask DisposeAsync()
    {
        if (this.app is not null)
        {
            await this.app.StopAsync();
            await this.app.DisposeAsync();
        }
    }

    public string GetRawHitSummary()
    {
        lock (this.lockObject)
        {
            return $"Raw endpoint hits: /v1/traces={this.rawTraceHits}, /v1/metrics={this.rawMetricHits}, /v1/logs={this.rawLogHits}.";
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

// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

extern alias OpenTelemetryProtocol;

using System.Diagnostics;
using System.Net;
#if NETFRAMEWORK
using System.Net.Http;
using System.Net.Http.Headers;
#endif
using BenchmarkDotNet.Attributes;
using Benchmarks.Helper;
using OpenTelemetry;
using OpenTelemetry.Internal;
using OpenTelemetryProtocol::OpenTelemetry.Exporter;
using OpenTelemetryProtocol::OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetryProtocol::OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
using OpenTelemetryProtocol::OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Transmission;

namespace Benchmarks.Exporter;

#pragma warning disable CA1001 // Types that own disposable fields should be disposable - handled by GlobalCleanup
public class OtlpExporterCompressionBenchmarks
#pragma warning restore CA1001 // Types that own disposable fields should be disposable - handled by GlobalCleanup
{
    private const int NumberOfBatches = 2;
    private const int NumberOfSpans = 10_000;

    private OtlpTraceExporter? exporter;
    private Activity? activity;
    private CircularBuffer<Activity>? activityBatch;

    [Params(OtlpExportCompression.None, OtlpExportCompression.GZip)]
    public OtlpExportCompression Compression { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        var options = new OtlpExporterOptions()
        {
            Compression = this.Compression,
            Protocol = OtlpExportProtocol.HttpProtobuf,
            HttpClientFactory = () => new HttpClient(new StubHttpClientHandler(), true),
        };

        this.exporter = new OtlpTraceExporter(
            options,
            new SdkLimitOptions(),
            new ExperimentalOptions(),
#pragma warning disable CA2000 // Dispose objects before losing scope
            new OtlpExporterTransmissionHandler(new OtlpGrpcExportClient(options, options.HttpClientFactory(), "opentelemetry.proto.collector.trace.v1.TraceService/Export"), options.TimeoutMilliseconds));
#pragma warning restore CA2000 // Dispose objects before losing scope

        this.activity = ActivityHelper.CreateTestActivity();
        this.activityBatch = new CircularBuffer<Activity>(NumberOfSpans);
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        this.activity?.Dispose();
        this.exporter?.Shutdown();
        this.exporter?.Dispose();
    }

    [Benchmark]
    public void OtlpExporter_Compression()
    {
        for (var i = 0; i < NumberOfBatches; i++)
        {
            for (var j = 0; j < NumberOfSpans; j++)
            {
                this.activityBatch!.Add(this.activity!);
            }

            this.exporter!.Export(new Batch<Activity>(this.activityBatch!, NumberOfSpans));
        }
    }

    private sealed class StubHttpClientHandler : DelegatingHandler
    {
#if NET
        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken) => CreateResponse();
#endif

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(CreateResponse());

        private static HttpResponseMessage CreateResponse()
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);

#if NET
            response.TrailingHeaders.Add("grpc-status", "0");
#else
            response.RequestMessage.Properties.Add("__ResponseTrailers", new ResponseTrailers()
            {
                { "grpc-status", "0" },
            });
#endif

            return response;
        }

#if NETFRAMEWORK
        private sealed class ResponseTrailers : HttpHeaders;
#endif
    }
}

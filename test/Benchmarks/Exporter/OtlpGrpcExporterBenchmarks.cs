// <copyright file="OtlpGrpcExporterBenchmarks.cs" company="OpenTelemetry Authors">
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

extern alias OpenTelemetryProtocol;

using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using Benchmarks.Helper;
using Grpc.Core;
using Moq;
using OpenTelemetry;
using OpenTelemetry.Internal;
using OpenTelemetryProtocol::OpenTelemetry.Exporter;
using OpenTelemetryProtocol::OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetryProtocol::OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
using OtlpCollector = OpenTelemetryProtocol::OpenTelemetry.Proto.Collector.Trace.V1;

namespace Benchmarks.Exporter;

public class OtlpGrpcExporterBenchmarks
{
    private OtlpTraceExporter exporter;
    private Activity activity;
    private CircularBuffer<Activity> activityBatch;

    [Params(1, 10, 100)]
    public int NumberOfBatches { get; set; }

    [Params(10000)]
    public int NumberOfSpans { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        var mockClient = new Mock<OtlpCollector.TraceService.TraceServiceClient>();
        mockClient
            .Setup(m => m.Export(
                It.IsAny<OtlpCollector.ExportTraceServiceRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(new OtlpCollector.ExportTraceServiceResponse());

        var options = new OtlpExporterOptions();
        this.exporter = new OtlpTraceExporter(
            options,
            new SdkLimitOptions(),
            new OtlpGrpcTraceExportClient(options, mockClient.Object));

        this.activity = ActivityHelper.CreateTestActivity();
        this.activityBatch = new CircularBuffer<Activity>(this.NumberOfSpans);
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        this.exporter.Shutdown();
        this.exporter.Dispose();
    }

    [Benchmark]
    public void OtlpExporter_Batching()
    {
        for (int i = 0; i < this.NumberOfBatches; i++)
        {
            for (int c = 0; c < this.NumberOfSpans; c++)
            {
                this.activityBatch.Add(this.activity);
            }

            this.exporter.Export(new Batch<Activity>(this.activityBatch, this.NumberOfSpans));
        }
    }
}

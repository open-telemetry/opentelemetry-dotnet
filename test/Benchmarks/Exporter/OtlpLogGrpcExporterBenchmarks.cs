// <copyright file="OtlpLogGrpcExporterBenchmarks.cs" company="OpenTelemetry Authors">
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

using BenchmarkDotNet.Attributes;
using Benchmarks.Helper;
using Grpc.Core;
using Moq;
using OpenTelemetry;
using OpenTelemetry.Internal;
using OpenTelemetry.Logs;
using OpenTelemetryProtocol::OpenTelemetry.Exporter;
using OpenTelemetryProtocol::OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetryProtocol::OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
using OtlpCollector = OpenTelemetryProtocol::OpenTelemetry.Proto.Collector.Logs.V1;

namespace Benchmarks.Exporter;

public class OtlpLogGrpcExporterBenchmarks
{
    private OtlpLogExporter exporter;
    private LogRecord logRecord;
    private CircularBuffer<LogRecord> logRecordBatch;

    [Params(1, 10, 100)]
    public int NumberOfBatches { get; set; }

    [Params(10000)]
    public int NumberOfLogs { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        var mockClient = new Mock<OtlpCollector.LogsService.LogsServiceClient>();
        mockClient
            .Setup(m => m.Export(
                It.IsAny<OtlpCollector.ExportLogsServiceRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(new OtlpCollector.ExportLogsServiceResponse());

        var options = new OtlpExporterOptions();
        this.exporter = new OtlpLogExporter(
            options,
            new SdkLimitOptions(),
            new OtlpGrpcLogExportClient(options, mockClient.Object));

        this.logRecord = LogRecordHelper.CreateTestLogRecord();
        this.logRecordBatch = new CircularBuffer<LogRecord>(this.NumberOfLogs);
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
            for (int c = 0; c < this.NumberOfLogs; c++)
            {
                this.logRecordBatch.Add(this.logRecord);
            }

            this.exporter.Export(new Batch<LogRecord>(this.logRecordBatch, this.NumberOfLogs));
        }
    }
}

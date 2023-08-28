// <copyright file="OtlpLogExporterBenchmarks.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Tests;
using OpenTelemetryProtocol::OpenTelemetry.Exporter;
using OpenTelemetryProtocol::OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetryProtocol::OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
using OtlpCollector = OpenTelemetryProtocol::OpenTelemetry.Proto.Collector.Logs.V1;

namespace Benchmarks.Exporter;

/*
BenchmarkDotNet v0.13.6, Windows 11 (10.0.22621.2134/22H2/2022Update/SunValley2) (Hyper-V)
AMD EPYC 7763, 1 CPU, 16 logical and 8 physical cores
.NET SDK 7.0.400
  [Host]     : .NET 7.0.10 (7.0.1023.36312), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.10 (7.0.1023.36312), X64 RyuJIT AVX2


|            Method |       Mean |     Error |    StdDev |   Gen0 |   Gen1 | Allocated |
|------------------ |-----------:|----------:|----------:|-------:|-------:|----------:|
| OtlpExporter_Http | 147.112 us | 2.9354 us | 2.4512 us | 0.7324 | 0.4883 |   12289 B |
| OtlpExporter_Grpc |   2.071 us | 0.0410 us | 0.0456 us | 0.0572 | 0.0534 |     968 B |
*/

public class OtlpLogExporterBenchmarks
{
    private readonly byte[] buffer = new byte[1024 * 1024];
    private OtlpLogExporter exporter;
    private LogRecord logRecord;
    private CircularBuffer<LogRecord> logRecordBatch;

    private IDisposable server;
    private string serverHost;
    private int serverPort;

    [GlobalSetup(Target = nameof(OtlpExporter_Grpc))]
    public void GlobalSetupGrpc()
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
        this.logRecordBatch = new CircularBuffer<LogRecord>(1);
    }

    [GlobalSetup(Target = nameof(OtlpExporter_Http))]
    public void GlobalSetupHttp()
    {
        this.server = TestHttpServer.RunServer(
            (ctx) =>
            {
                using (Stream receiveStream = ctx.Request.InputStream)
                {
                    while (true)
                    {
                        if (receiveStream.Read(this.buffer, 0, this.buffer.Length) == 0)
                        {
                            break;
                        }
                    }
                }

                ctx.Response.StatusCode = 200;
                ctx.Response.OutputStream.Close();
            },
            out this.serverHost,
            out this.serverPort);

        var options = new OtlpExporterOptions
        {
            Endpoint = new Uri($"http://{this.serverHost}:{this.serverPort}"),
        };
        this.exporter = new OtlpLogExporter(
            options,
            new SdkLimitOptions(),
            new OtlpHttpLogExportClient(options, options.HttpClientFactory()));

        this.logRecord = LogRecordHelper.CreateTestLogRecord();
        this.logRecordBatch = new CircularBuffer<LogRecord>(1);
        this.logRecordBatch.Add(this.logRecord);
    }

    [GlobalCleanup(Target = nameof(OtlpExporter_Grpc))]
    public void GlobalCleanupGrpc()
    {
        this.exporter.Shutdown();
        this.exporter.Dispose();
    }

    [GlobalCleanup(Target = nameof(OtlpExporter_Http))]
    public void GlobalCleanupHttp()
    {
        this.exporter.Shutdown();
        this.exporter.Dispose();
        this.server.Dispose();
    }

    [Benchmark]
    public void OtlpExporter_Http()
    {
        this.exporter.Export(new Batch<LogRecord>(this.logRecordBatch, 1));
    }

    [Benchmark]
    public void OtlpExporter_Grpc()
    {
        this.exporter.Export(new Batch<LogRecord>(this.logRecordBatch, 1));
    }
}

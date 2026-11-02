// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

extern alias OpenTelemetryProtocol;

using BenchmarkDotNet.Attributes;
using Benchmarks.Logs;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetryProtocol::OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetryProtocol::OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;

/*
Serializes a full export batch of log records, which includes grouping the
records by their instrumentation scope. The records are produced by
LoggerCount distinct ILogger categories in round-robin order so that
consecutive records usually belong to different scopes, as they do when
several components of an application log concurrently.
*/

namespace Benchmarks.Exporter;

[MemoryDiagnoser(false)]
public class ProtobufOtlpLogSerializationBenchmarks
{
    private const int BatchSize = 512;

    private readonly SdkLimitOptions sdkLimitOptions = new();
    private readonly ExperimentalOptions experimentalOptions = new();
    private readonly Resource resource = Resource.Empty;
    private readonly byte[] buffer = new byte[1024 * 1024];
    private LogRecord[] logRecords = null!;

    [Params(1, 8, 64)]
    public int LoggerCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var items = new List<LogRecord>(BatchSize);
        using var factory = LoggerFactory.Create(builder => builder
            .UseOpenTelemetry(logging => logging.AddInMemoryExporter(items)));

        var loggers = new ILogger[this.LoggerCount];
        for (var i = 0; i < loggers.Length; i++)
        {
            loggers[i] = factory.CreateLogger($"Benchmarks.Component{i}.Operations");
        }

        for (var i = 0; i < BatchSize; i++)
        {
            loggers[i % loggers.Length].HelloFrom("artichoke", 3.99);
        }

        this.logRecords = [.. items];
    }

    [Benchmark]
    public int WriteLogsData()
    {
        var buffer = this.buffer;
        var batch = new Batch<LogRecord>(this.logRecords, this.logRecords.Length);
        return ProtobufOtlpLogSerializer.WriteLogsData(ref buffer, 0, this.sdkLimitOptions, this.experimentalOptions, this.resource, batch);
    }
}

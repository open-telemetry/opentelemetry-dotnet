// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

extern alias OpenTelemetryProtocol;

using BenchmarkDotNet.Attributes;
using Benchmarks.Helper;
using OpenTelemetry.Logs;
using OpenTelemetryProtocol::OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetryProtocol::OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;

/*
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8457/25H2/2025Update/HudsonValley2)
AMD Ryzen 7 9800X3D 4.70GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.301
  [Host]     : .NET 10.0.9 (10.0.9, 10.0.926.27113), X64 RyuJIT x86-64-v4
  DefaultJob : .NET 10.0.9 (10.0.9, 10.0.926.27113), X64 RyuJIT x86-64-v4


| Method         | AttributeCount | KvListAttributeCount | Mean     | Error    | StdDev   | Median   | Allocated |
|--------------- |--------------- |--------------------- |---------:|---------:|---------:|---------:|----------:|
| WriteLogRecord | 4              | 0                    | 113.8 ns |  1.87 ns |  1.57 ns | 113.9 ns |      32 B |
| WriteLogRecord | 4              | 2                    | 168.9 ns |  3.15 ns |  8.31 ns | 166.4 ns |      32 B |
| WriteLogRecord | 4              | 4                    | 224.2 ns |  4.55 ns | 12.84 ns | 219.1 ns |      32 B |
| WriteLogRecord | 4              | 8                    | 254.4 ns |  5.11 ns | 10.99 ns | 252.6 ns |      32 B |
| WriteLogRecord | 8              | 0                    | 193.0 ns |  3.88 ns |  9.58 ns | 188.7 ns |      32 B |
| WriteLogRecord | 8              | 2                    | 239.4 ns |  2.89 ns |  2.26 ns | 239.3 ns |      32 B |
| WriteLogRecord | 8              | 4                    | 297.5 ns |  5.87 ns | 10.58 ns | 294.2 ns |      32 B |
| WriteLogRecord | 8              | 8                    | 340.9 ns |  8.21 ns | 23.03 ns | 332.2 ns |      32 B |
| WriteLogRecord | 16             | 0                    | 365.1 ns |  7.95 ns | 22.16 ns | 357.4 ns |      32 B |
| WriteLogRecord | 16             | 2                    | 427.9 ns | 11.96 ns | 34.70 ns | 417.5 ns |      32 B |
| WriteLogRecord | 16             | 4                    | 476.7 ns | 15.13 ns | 44.62 ns | 480.0 ns |      32 B |
| WriteLogRecord | 16             | 8                    | 471.3 ns |  9.11 ns | 12.16 ns | 466.0 ns |      32 B |
 */

namespace Benchmarks.Exporter;

[MemoryDiagnoser(false)]
public class ProtobufOtlpLogSerializerBenchmarks
{
    private static readonly KeyValuePair<string, object?>[] AllAttributes =
    [
        new("http.request.method", "GET"),
        new("http.route", "/api/orders/{id}"),
        new("http.response.status_code", 200L),
        new("url.full", "https://shop.example.com/api/orders/42"),
        new("url.scheme", "https"),
        new("server.address", "shop.example.com"),
        new("server.port", 443L),
        new("client.address", "203.0.113.7"),
        new("user_agent.original", "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7)"),
        new("network.protocol.version", "1.1"),
        new("enduser.id", "user-8f3a7b4c"),
        new("session.id", "9a8d1f3e-4b2c-4e15-9a4f-1b2c3d4e5f6a"),
        new("db.system", "postgresql"),
        new("db.namespace", "orders"),
        new("db.operation.name", "SELECT"),
        new("db.query.text", "SELECT * FROM orders WHERE id = $1"),
        new("messaging.system", "rabbitmq"),
        new("messaging.destination.name", "orders.events"),
        new("messaging.message.id", "msg-0abcdef1234567890"),
        new("exception.type", "System.TimeoutException"),
        new("exception.message", "The operation has timed out."),
        new("code.function", "ProcessOrder"),
        new("code.namespace", "Checkout.Api.Orders"),
        new("code.filepath", "/src/Checkout.Api/Orders/OrderProcessor.cs"),
        new("code.lineno", 142L),
        new("thread.id", 27L),
        new("thread.name", "worker-3"),
        new("retry.count", 2L),
        new("cache.hit", true),
        new("response.latency_ms", 18.7),
        new("feature.flag.new_checkout", true),
        new("tenant.id", "tenant-123456789012"),
    ];

    private readonly byte[] buffer = new byte[64 * 1024];
    private readonly SdkLimitOptions sdkLimitOptions = new();
    private readonly ExperimentalOptions experimentalOptions = new();
    private LogRecord logRecord = null!;

    [Params(4, 8, 16)]
    public int AttributeCount { get; set; }

    // 0 means no kvlist attribute is added; otherwise a single flat (depth-1)
    // kvlist attribute with this many entries is appended to the scalar ones.
    [Params(0, 2, 4, 8)]
    public int KvListAttributeCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        this.logRecord = LogRecordHelper.CreateTestLogRecord();

        var includeKvList = this.KvListAttributeCount > 0;
        var attributes = new KeyValuePair<string, object?>[this.AttributeCount + (includeKvList ? 1 : 0)];

        var count = Math.Min(this.AttributeCount, AllAttributes.Length);
        for (var i = 0; i < count; i++)
        {
            attributes[i] = AllAttributes[i];
        }

        for (var i = count; i < this.AttributeCount; i++)
        {
            attributes[i] = new KeyValuePair<string, object?>($"custom.attribute.{i}", Guid.NewGuid().ToString());
        }

        if (includeKvList)
        {
            attributes[this.AttributeCount] = new KeyValuePair<string, object?>("kvlist", BuildKvList(this.KvListAttributeCount));
        }

        this.logRecord.Attributes = attributes;
    }

    [Benchmark]
    public int WriteLogRecord()
        => ProtobufOtlpLogSerializer.WriteLogRecord(this.buffer, 0, this.sdkLimitOptions, this.experimentalOptions, this.logRecord);

    private static List<KeyValuePair<string, object?>> BuildKvList(int entryCount)
    {
        var list = new List<KeyValuePair<string, object?>>(entryCount);
        for (var i = 0; i < entryCount; i++)
        {
            object value = (i % 4) switch
            {
                0 => "value",
                1 => (long)i,
                2 => i % 2 == 0,
                _ => i * 1.5,
            };

            list.Add(new KeyValuePair<string, object?>($"key.{i}", value));
        }

        return list;
    }
}

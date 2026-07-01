// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

extern alias OpenTelemetryProtocol;

using BenchmarkDotNet.Attributes;
using Benchmarks.Helper;
using OpenTelemetry.Logs;
using OpenTelemetryProtocol::OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetryProtocol::OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;

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

// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

extern alias OpenTelemetryProtocol;

using System.Collections;
using BenchmarkDotNet.Attributes;
using Benchmarks.Helper;
using OpenTelemetry.Logs;
using OpenTelemetryProtocol::OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetryProtocol::OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;

namespace Benchmarks.Exporter;

[MemoryDiagnoser(false)]
public class ProtobufOtlpKvListShapeBenchmarks
{
    private readonly byte[] buffer = new byte[64 * 1024];
    private readonly SdkLimitOptions sdkLimitOptions = new();
    private readonly ExperimentalOptions experimentalOptions = new();
    private LogRecord logRecord = null!;

    [Params("ObjectArray", "ObjectList", "ObjectDictionary", "StringDictionary", "IntDictionary", "Hashtable", "NestedObjectDictionary")]
    public string Shape { get; set; } = "ObjectList";

    [Params(8)]
    public int EntryCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        this.logRecord = LogRecordHelper.CreateTestLogRecord();
        this.logRecord.Attributes =
        [
            new("http.request.method", "GET"),
            new("http.route", "/api/orders/{id}"),
            new("http.response.status_code", 200L),
            new("url.scheme", "https"),
            new("kvlist", BuildKvList(this.Shape, this.EntryCount)),
        ];
    }

    [Benchmark]
    public int WriteLogRecord()
        => ProtobufOtlpLogSerializer.WriteLogRecord(this.buffer, 0, this.sdkLimitOptions, this.experimentalOptions, this.logRecord);

    private static object BuildKvList(string shape, int entryCount)
    {
        switch (shape)
        {
            case "ObjectArray":
                var array = new KeyValuePair<string, object?>[entryCount];
                for (var i = 0; i < entryCount; i++)
                {
                    array[i] = new KeyValuePair<string, object?>($"key.{i}", ObjectValue(i));
                }

                return array;

            case "ObjectList":
                var list = new List<KeyValuePair<string, object?>>(entryCount);
                for (var i = 0; i < entryCount; i++)
                {
                    list.Add(new KeyValuePair<string, object?>($"key.{i}", ObjectValue(i)));
                }

                return list;

            case "ObjectDictionary":
                var objectDictionary = new Dictionary<string, object?>(entryCount);
                for (var i = 0; i < entryCount; i++)
                {
                    objectDictionary[$"key.{i}"] = ObjectValue(i);
                }

                return objectDictionary;

            case "StringDictionary":
                var stringDictionary = new Dictionary<string, string>(entryCount);
                for (var i = 0; i < entryCount; i++)
                {
                    stringDictionary[$"key.{i}"] = $"value.{i}";
                }

                return stringDictionary;

            case "IntDictionary":
                var intDictionary = new Dictionary<string, int>(entryCount);
                for (var i = 0; i < entryCount; i++)
                {
                    intDictionary[$"key.{i}"] = i;
                }

                return intDictionary;

            case "Hashtable":
                var hashtable = new Hashtable(entryCount);
                for (var i = 0; i < entryCount; i++)
                {
                    hashtable[$"key.{i}"] = ObjectValue(i);
                }

                return hashtable;

            case "NestedObjectDictionary":
                var outer = new Dictionary<string, object?>(entryCount);
                for (var i = 0; i < entryCount - 1; i++)
                {
                    outer[$"key.{i}"] = ObjectValue(i);
                }

                outer["nested"] = new Dictionary<string, object?>
                {
                    ["a"] = "value",
                    ["b"] = 1L,
                    ["c"] = true,
                };

                return outer;

            default:
                throw new NotSupportedException();
        }
    }

    private static object ObjectValue(int i)
        => (i % 4) switch
        {
            0 => "value",
            1 => (long)i,
            2 => i % 2 == 0,
            _ => i * 1.5,
        };
}

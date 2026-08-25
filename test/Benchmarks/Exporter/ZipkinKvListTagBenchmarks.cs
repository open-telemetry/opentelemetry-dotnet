// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

extern alias Zipkin;

using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Zipkin::OpenTelemetry.Exporter.Zipkin.Implementation;

namespace Benchmarks.Exporter;

#pragma warning disable CA1001 // Types that own disposable fields should be disposable - the benchmark disposes them in GlobalCleanup.
[MemoryDiagnoser(false)]
public class ZipkinKvListTagBenchmarks
{
    private readonly MemoryStream stream = new();
    private Utf8JsonWriter writer = null!;
    private object kvList = null!;

    [Params("Flat", "Nested")]
    public string Shape { get; set; } = "Flat";

    [GlobalSetup]
    public void Setup()
    {
        this.writer = new Utf8JsonWriter(this.stream);

        var dictionary = new Dictionary<string, object?>
        {
            ["name"] = "acme",
            ["tier"] = 2L,
            ["enabled"] = true,
            ["ratio"] = 1.5,
            ["region"] = "eu",
        };

        if (this.Shape == "Nested")
        {
            dictionary["nested"] = new Dictionary<string, object?>
            {
                ["a"] = "value",
                ["b"] = 1L,
                ["c"] = true,
            };
        }

        this.kvList = dictionary;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.writer.Dispose();
        this.stream.Dispose();
    }

    [Benchmark]
    public long WriteKvListTag()
    {
        this.stream.SetLength(0);
        this.writer.Reset(this.stream);

        var writerAlias = this.writer;
        writerAlias.WriteStartObject();
        ZipkinTagWriter.Instance.TryWriteTag(ref writerAlias, "kvlist", this.kvList);
        writerAlias.WriteEndObject();
        writerAlias.Flush();

        return this.stream.Length;
    }
}
#pragma warning restore CA1001

// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
extern alias OpenTelemetryProtocol;

using System.Buffers;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using OpenTelemetryProtocol::OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;

namespace Benchmarks.Exporter;

[MemoryDiagnoser]
public class OtlpUtf8SpanFormattableTagBenchmarks
{
    private readonly byte[] buffer = new byte[1024 * 64];
    private TestDTO[] toSerialize = [];

    [GlobalSetup]
    public void Setup()
    {
        this.toSerialize = [.. Enumerable.Range(0, 50).Select(i => new TestDTO(
            Id: $"test-{i:D4}",
            SeatId: $"seat-{i % 3}",
            FloorPrice: 1.25 + (i * 0.5),
            Currency: "USD",
            Formats: i % 2 == 0 ? ["flat", "tall"] : ["large"],
            Enabled: i % 4 != 0))];
    }

    [Benchmark(Baseline = true)]
    public int JsonStringTag()
    {
        var state = new ProtobufOtlpTagWriter.OtlpTagWriterState { Buffer = this.buffer };
        ProtobufOtlpTagWriter.WriteKeyValue(ref state, 1, "items", JsonSerializer.Serialize(this.toSerialize));
        return state.WritePosition;
    }

    [Benchmark]
    public int JsonUtf8SpanFormattableTag()
    {
        var state = new ProtobufOtlpTagWriter.OtlpTagWriterState { Buffer = this.buffer };
        ProtobufOtlpTagWriter.WriteKeyValue(ref state, 1, "items", new JsonTagValue<TestDTO[]>(this.toSerialize));
        return state.WritePosition;
    }

    internal sealed record TestDTO(string Id, string SeatId, double FloorPrice, string Currency, string[] Formats, bool Enabled);

    // Serializes at export time through a reused writer, so the only per-tag allocation is this wrapper.
    private sealed class JsonTagValue<T>(T value) : IUtf8SpanFormattable
    {
        [ThreadStatic]
        private static ArrayBufferWriter<byte>? bufferWriter;

        [ThreadStatic]
        private static Utf8JsonWriter? jsonWriter;

        public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
        {
            var output = bufferWriter ??= new ArrayBufferWriter<byte>();
            output.ResetWrittenCount();

            var writer = jsonWriter ??= new Utf8JsonWriter(output);
            writer.Reset(output);

            JsonSerializer.Serialize(writer, value);
            writer.Flush();

            bytesWritten = output.WrittenCount;
            return output.WrittenSpan.TryCopyTo(utf8Destination);
        }

        public override string ToString() => JsonSerializer.Serialize(value);
    }
}
#endif

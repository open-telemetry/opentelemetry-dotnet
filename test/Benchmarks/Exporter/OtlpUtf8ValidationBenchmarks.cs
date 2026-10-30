// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
extern alias OpenTelemetryProtocol;

using System.Buffers;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using BenchmarkDotNet.Attributes;
using OpenTelemetryProtocol::OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;

namespace Benchmarks.Exporter;

/// <summary>
/// Compares writing a JSON attribute as a string with writing it through <see cref="IUtf8SpanFormattable"/>,
/// whose path includes a <see cref="Utf8.IsValid(ReadOnlySpan{byte})"/> check, across attribute value
/// length limits, and measures that check on its own.
/// </summary>
[MemoryDiagnoser]
public class OtlpUtf8ValidationBenchmarks
{
    private static readonly ProtobufOtlpTagWriter Writer = new(new ProtobufOtlpTagWriter.OtlpArrayTagWriter(), enableUtf8Spans: true);

    private static readonly JsonSerializerOptions RawUtf8Options = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private readonly byte[] buffer = new byte[ProtobufSerializer.InitialBufferSize];
    private TestDTO[] items = [];
    private object tagValue = null!;
    private byte[] valueBytes = [];
    private int? tagValueMaxLength;

    [Params(1000)]
    public int ItemCount { get; set; }

    [Params(false, true)]
    public bool NonAscii { get; set; }

    /// <summary>
    /// Gets or sets the attribute value length limit in chars, where <c>0</c> means no limit.
    /// </summary>
    [Params(0, 16 * 1024, 64 * 1024)]
    public int MaxLength { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        this.items =
        [
            .. Enumerable.Range(0, this.ItemCount).Select(i => new TestDTO(
                Id: this.NonAscii ? $"\u30C6\u30B9\u30C8-{i:D4}" : $"test-{i:D4}",
                SeatId: this.NonAscii ? $"si\u00E8ge-{i % 3}" : $"seat-{i % 3}",
                FloorPrice: 1.25 + (i * 0.5),
                Currency: "USD",
                Formats: i % 2 == 0 ? ["flat", "tall"] : ["large"],
                Enabled: i % 4 != 0)),
        ];

        this.tagValue = new JsonTagValue<TestDTO[]>(this.items);
        this.valueBytes = JsonSerializer.SerializeToUtf8Bytes(this.items, RawUtf8Options);
        this.tagValueMaxLength = this.MaxLength == 0 ? null : this.MaxLength;
    }

    [Benchmark(Baseline = true)]
    public int StringTag()
    {
        var state = new ProtobufOtlpTagWriter.OtlpTagWriterState { Buffer = this.buffer };
        Writer.WriteKeyValueInternal(ref state, 1, "items", JsonSerializer.Serialize(this.items, RawUtf8Options), this.tagValueMaxLength);
        return state.WritePosition;
    }

    [Benchmark]
    public int Utf8SpanFormattableTag()
    {
        var state = new ProtobufOtlpTagWriter.OtlpTagWriterState { Buffer = this.buffer };
        Writer.WriteKeyValueInternal(ref state, 1, "items", this.tagValue, this.tagValueMaxLength);
        return state.WritePosition;
    }

    [Benchmark]
    public bool ValidationOnly()
        => Utf8.IsValid(this.valueBytes);

    internal sealed record TestDTO(string Id, string SeatId, double FloorPrice, string Currency, string[] Formats, bool Enabled);

    /// <summary>
    /// Serializes at export time through a reused writer, so the only per-tag allocation is this wrapper.
    /// </summary>
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

            var writer = jsonWriter ??= new Utf8JsonWriter(output, new JsonWriterOptions { Encoder = RawUtf8Options.Encoder });
            writer.Reset(output);

            JsonSerializer.Serialize(writer, value, RawUtf8Options);
            writer.Flush();

            bytesWritten = output.WrittenCount;
            return output.WrittenSpan.TryCopyTo(utf8Destination);
        }

        public override string ToString() => JsonSerializer.Serialize(value, RawUtf8Options);
    }
}
#endif

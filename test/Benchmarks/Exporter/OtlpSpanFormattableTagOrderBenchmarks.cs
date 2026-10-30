// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
extern alias OpenTelemetryProtocol;

using BenchmarkDotNet.Attributes;
using OpenTelemetryProtocol::OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;

namespace Benchmarks.Exporter;

/// <summary>
/// Compares writing the built-in types the tag writer formats through <see cref="ISpanFormattable"/>,
/// which also implement <see cref="IUtf8SpanFormattable"/>, with the UTF-8 span path disabled and
/// enabled. <see cref="long"/>, <see cref="double"/> and <see cref="bool"/> are controls, written as
/// native values either way.
/// </summary>
[MemoryDiagnoser]
public class OtlpSpanFormattableTagOrderBenchmarks
{
    private static readonly ProtobufOtlpTagWriter DisabledWriter = new(new ProtobufOtlpTagWriter.OtlpArrayTagWriter(), enableUtf8Spans: false);
    private static readonly ProtobufOtlpTagWriter EnabledWriter = new(new ProtobufOtlpTagWriter.OtlpArrayTagWriter(), enableUtf8Spans: true);

    private readonly byte[] buffer = new byte[ProtobufSerializer.InitialBufferSize];
    private object value = null!;
    private int? tagValueMaxLength;

    [Params("Int64", "Double", "Boolean", "UInt64", "DateTime", "DateTimeOffset", "TimeSpan", "Guid", "Decimal")]
    public string Type { get; set; } = null!;

    /// <summary>
    /// Gets or sets the attribute value length limit in chars, where <c>0</c> means no limit.
    /// </summary>
    [Params(0, 16 * 1024)]
    public int MaxLength { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        this.value = this.Type switch
        {
            "Int64" => 1234567890123L,
            "Double" => 3.14159,
            "Boolean" => true,
            "UInt64" => ulong.MaxValue,
            "DateTime" => new DateTime(2026, 10, 30, 13, 45, 30, 123, DateTimeKind.Utc),
            "DateTimeOffset" => new DateTimeOffset(2026, 10, 30, 13, 45, 30, 123, TimeSpan.FromHours(11)),
            "TimeSpan" => TimeSpan.FromMilliseconds(123456.789),
            "Guid" => new Guid("6f9619ff-8b86-d011-b42d-00c04fc964ff"),
            "Decimal" => 12345.6789m,
            _ => throw new NotSupportedException(this.Type),
        };

        this.tagValueMaxLength = this.MaxLength > 0 ? this.MaxLength : null;
    }

    [Benchmark(Baseline = true)]
    public int Disabled() => this.Write(DisabledWriter);

    [Benchmark]
    public int Enabled() => this.Write(EnabledWriter);

    private int Write(ProtobufOtlpTagWriter writer)
    {
        var state = new ProtobufOtlpTagWriter.OtlpTagWriterState { Buffer = this.buffer };
        writer.WriteKeyValueInternal(ref state, 1, "value", this.value, this.tagValueMaxLength);
        return state.WritePosition;
    }
}
#endif

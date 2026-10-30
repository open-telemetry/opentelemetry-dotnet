// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Google.Protobuf;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient.Grpc;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.FuzzTests;

/// <summary>
/// Fuzz tests for <see cref="GrpcStatusDeserializer"/>, a hand-written protobuf decoder
/// which parses the <c>grpc-status-details-bin</c> trailer sent by a (potentially misbehaving) server.
/// </summary>
public class GrpcStatusDeserializerFuzzTests
{
    private const int MaxTests = 200;
    private const string RetryInfoTypeUrl = "type.googleapis.com/google.rpc.RetryInfo";

    [Property(MaxTest = MaxTests)]
    public Property ArbitraryBytesNeverEscapeTheRetryDelayParser() => Prop.ForAll(
        Generators.ArbitraryBytesArbitrary(),
        (data) =>
        {
            var header = Convert.ToBase64String(data);

            // The retry delay parser must contain every failure, as it runs on the export path.
            var delay = GrpcStatusDeserializer.TryGetGrpcRetryDelay(header);

            return delay is null || delay.Value >= TimeSpan.Zero;
        });

    [Property(MaxTest = MaxTests)]
    public Property ArbitraryBytesOnlyFailWithDocumentedExceptions() => Prop.ForAll(
        Generators.ArbitraryBytesArbitrary(),
        (data) =>
        {
            try
            {
                _ = GrpcStatusDeserializer.DeserializeStatus(Convert.ToBase64String(data));
                return true;
            }
            catch (Exception ex) when (ex is EndOfStreamException or InvalidDataException)
            {
                return true;
            }
        });

    [Property(MaxTest = MaxTests)]
    public Property ArbitraryHeaderStringsNeverEscapeTheRetryDelayParser() => Prop.ForAll(
        Generators.ArbitraryHeaderArbitrary(),
        (header) =>
        {
            var delay = GrpcStatusDeserializer.TryGetGrpcRetryDelay(header);

            return delay is null || delay.Value >= TimeSpan.Zero;
        });

    [Property(MaxTest = MaxTests)]
    public Property ValidStatusRoundTripsTheRetryDelay() => Prop.ForAll(
        Generators.StatusArbitrary(),
        (status) =>
        {
            var actual = GrpcStatusDeserializer.TryGetGrpcRetryDelay(Convert.ToBase64String(status.Data));

            return actual == status.ExpectedRetryDelay;
        });

    [Property(MaxTest = MaxTests)]
    public Property ValidStatusRoundTripsCodeAndMessage() => Prop.ForAll(
        Generators.StatusArbitrary(),
        (status) =>
        {
            var actual = GrpcStatusDeserializer.DeserializeStatus(Convert.ToBase64String(status.Data));

            return actual is { } value &&
                   value.Code == status.Code &&
                   value.Message == status.Message &&
                   value.Details.Count == status.DetailCount;
        });

    [Property(MaxTest = MaxTests)]
    public Property DecoderAgreesWithReferenceImplementationOnMutatedMessages() => Prop.ForAll(
        Generators.MutatedStatusArbitrary(),
        (data) =>
        {
            var referenceAccepted = TryGetReferenceRetryDelay(data, out var expected);
            var actual = GrpcStatusDeserializer.TryGetGrpcRetryDelay(Convert.ToBase64String(data));

            // When a conformant protobuf decoder accepts the message, the exporter must read the same delay.
            // (Where the reference rejects the message, the exporter is free to fall back to no delay.)
            if (referenceAccepted && actual != expected)
            {
                throw new InvalidOperationException($"The reference decoder read a retry delay of {expected?.ToString() ?? "none"} but the exporter read {actual?.ToString() ?? "none"} from: {Convert.ToBase64String(data)}");
            }

            return true;
        });

    [Property(MaxTest = MaxTests)]
    public Property OversizedLengthPrefixOnAnyValueIsRejected() => Prop.ForAll(
        Generators.RetryDelayArbitrary(),
        Generators.OverflowingLengthArbitrary(),
        (retryDelay, extraHighBits) =>
        {
            // Encode a RetryInfo wrapped in an Any, but declare the length of the Any.value field
            // as (realLength + (extraHighBits << 32)). A conformant decoder rejects the message
            // (the declared length exceeds the data available), so no retry delay must be produced.
            var retryInfo = Generators.EncodeRetryInfo(retryDelay.Seconds, retryDelay.Nanos, hasDelay: true);
            var typeUrl = new Writer().String(1, RetryInfoTypeUrl).ToArray();

            var value = new Writer()
                .Tag(2, WireFormat.WireType.LengthDelimited)
                .RawVarint((uint)retryInfo.Length | ((ulong)extraHighBits << 32))
                .Raw(retryInfo)
                .ToArray();

            var any = new Writer().Raw(typeUrl).Raw(value).ToArray();
            var status = new Writer().Bytes(3, any).ToArray();

            var actual = GrpcStatusDeserializer.TryGetGrpcRetryDelay(Convert.ToBase64String(status));

            return actual is null;
        });

    [Property(MaxTest = MaxTests)]
    public Property OversizedLengthPrefixOnAnyTypeUrlIsRejected() => Prop.ForAll(
        Generators.RetryDelayArbitrary(),
        Generators.OverflowingLengthArbitrary(),
        (retryDelay, extraHighBits) =>
        {
            var retryInfo = Generators.EncodeRetryInfo(retryDelay.Seconds, retryDelay.Nanos, hasDelay: true);

            var typeUrlBytes = Encoding.UTF8.GetBytes(RetryInfoTypeUrl);
            var typeUrl = new Writer()
                .Tag(1, WireFormat.WireType.LengthDelimited)
                .RawVarint((uint)typeUrlBytes.Length | ((ulong)extraHighBits << 32))
                .Raw(typeUrlBytes)
                .ToArray();

            var value = new Writer().Bytes(2, retryInfo).ToArray();
            var any = new Writer().Raw(typeUrl).Raw(value).ToArray();
            var status = new Writer().Bytes(3, any).ToArray();

            var actual = GrpcStatusDeserializer.TryGetGrpcRetryDelay(Convert.ToBase64String(status));

            return actual is null;
        });

    [Property(MaxTest = MaxTests)]
    public Property KnownFieldNumbersWithAnUnexpectedWireTypeAreSkipped() => Prop.ForAll(
        Generators.StatusArbitrary(),
        Generators.StrayFieldArbitrary(),
        (status, stray) =>
        {
            // A field whose wire type is not the one its number is declared with is an unknown field (it is skipped, as
            // any other protobuf decoder would), so it must not change how the rest of the status is read.
            var data = stray.Concat(status.Data).ToArray();
            var actual = GrpcStatusDeserializer.TryGetGrpcRetryDelay(Convert.ToBase64String(data));

            return actual == status.ExpectedRetryDelay;
        });

    private static bool TryGetReferenceRetryDelay(byte[] data, out TimeSpan? retryDelay)
    {
        retryDelay = null;

        try
        {
            using var input = new CodedInputStream(data);
            ByteString? retryInfo = null;

            uint tag;
            while ((tag = ReadStrictTag(input)) != 0)
            {
                if (WireFormat.GetTagFieldNumber(tag) == 3 && WireFormat.GetTagWireType(tag) == WireFormat.WireType.LengthDelimited)
                {
                    var (typeUrl, value) = ReadAny(input.ReadBytes());

                    if (retryInfo is null && typeUrl is not null && typeUrl.EndsWith("/google.rpc.RetryInfo", StringComparison.Ordinal))
                    {
                        retryInfo = value ?? ByteString.Empty;
                    }
                }
                else if (WireFormat.GetTagFieldNumber(tag) == 1 && WireFormat.GetTagWireType(tag) == WireFormat.WireType.Varint)
                {
                    input.ReadInt32();
                }
                else if (WireFormat.GetTagFieldNumber(tag) == 2 && WireFormat.GetTagWireType(tag) == WireFormat.WireType.LengthDelimited)
                {
                    input.ReadString();
                }
                else
                {
                    input.SkipLastField();
                }
            }

            if (retryInfo is not null)
            {
                var delay = ReadRetryInfo(retryInfo);
                if (delay is { } d)
                {
                    var timeSpan = TimeSpan.FromSeconds(d.Seconds) + TimeSpan.FromTicks(d.Nanos / 100);
                    retryDelay = timeSpan < TimeSpan.Zero ? TimeSpan.Zero : timeSpan;
                }
            }

            return true;
        }
        catch (Exception ex) when (ex is InvalidProtocolBufferException or InvalidDataException or OverflowException or ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    // Google.Protobuf silently skips fields with the (invalid) wire types 6 and 7 and deprecated groups, which a status never contains.
    private static uint ReadStrictTag(CodedInputStream input)
    {
        var tag = input.ReadTag();

        if (tag != 0 && WireFormat.GetTagWireType(tag) is not (WireFormat.WireType.Varint or WireFormat.WireType.Fixed64 or WireFormat.WireType.LengthDelimited or WireFormat.WireType.Fixed32))
        {
            throw new InvalidDataException("Invalid wire type.");
        }

        return tag;
    }

    private static (string? TypeUrl, ByteString? Value) ReadAny(ByteString bytes)
    {
        using var input = new CodedInputStream(bytes.ToByteArray());
        string? typeUrl = null;
        ByteString? value = null;

        uint tag;
        while ((tag = ReadStrictTag(input)) != 0)
        {
            if (WireFormat.GetTagFieldNumber(tag) == 1 && WireFormat.GetTagWireType(tag) == WireFormat.WireType.LengthDelimited)
            {
                typeUrl = input.ReadString();
            }
            else if (WireFormat.GetTagFieldNumber(tag) == 2 && WireFormat.GetTagWireType(tag) == WireFormat.WireType.LengthDelimited)
            {
                value = input.ReadBytes();
            }
            else
            {
                input.SkipLastField();
            }
        }

        return (typeUrl, value);
    }

    private static (long Seconds, int Nanos)? ReadRetryInfo(ByteString bytes)
    {
        using var input = new CodedInputStream(bytes.ToByteArray());
        (long, int)? result = null;

        uint tag;
        while ((tag = ReadStrictTag(input)) != 0)
        {
            if (WireFormat.GetTagFieldNumber(tag) == 1 && WireFormat.GetTagWireType(tag) == WireFormat.WireType.LengthDelimited)
            {
                result = ReadDuration(input.ReadBytes());
            }
            else
            {
                input.SkipLastField();
            }
        }

        return result;
    }

    private static (long Seconds, int Nanos) ReadDuration(ByteString bytes)
    {
        using var input = new CodedInputStream(bytes.ToByteArray());
        long seconds = 0;
        var nanos = 0;

        uint tag;
        while ((tag = ReadStrictTag(input)) != 0)
        {
            if (WireFormat.GetTagFieldNumber(tag) == 1 && WireFormat.GetTagWireType(tag) == WireFormat.WireType.Varint)
            {
                seconds = input.ReadInt64();
            }
            else if (WireFormat.GetTagFieldNumber(tag) == 2 && WireFormat.GetTagWireType(tag) == WireFormat.WireType.Varint)
            {
                nanos = input.ReadInt32();
            }
            else
            {
                input.SkipLastField();
            }
        }

        return (seconds, nanos);
    }

    private static class Generators
    {
        public static Arbitrary<byte[]> ArbitraryBytesArbitrary() => Gen.Sized(size =>
            from length in Gen.Choose(0, Math.Min((size * 4) + 1, 512))
            from bytes in Gen.ArrayOf(Gen.Choose(0, 255).Select(static b => (byte)b), length)
            select bytes).ToArbitrary();

        public static Arbitrary<string?> ArbitraryHeaderArbitrary()
        {
            var base64Chars = Gen.Elements("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/=".ToCharArray());
            var anyChar = Gen.Choose(0, 0xFFFF).Select(static c => (char)c);

            var charGen = Gen.Frequency((4, base64Chars), (1, anyChar));

            var text = Gen.Sized(size =>
                from length in Gen.Choose(0, Math.Min((size * 2) + 1, 256))
                from chars in Gen.ArrayOf(charGen, length)
                select (string?)new string(chars));

            return Gen.OneOf(Gen.Constant((string?)null), Gen.Constant((string?)string.Empty), text).ToArbitrary();
        }

        public static Arbitrary<(long Seconds, int Nanos)> RetryDelayArbitrary()
        {
            var gen =
                from seconds in Gen.Choose(0, 3600)
                from nanos in Gen.Choose(0, 999_999_999)
                select ((long)seconds, nanos);

            return gen.ToArbitrary();
        }

        public static Arbitrary<int> OverflowingLengthArbitrary() => Gen.Choose(1, int.MaxValue).ToArbitrary();

        public static Arbitrary<StatusModel> StatusArbitrary()
        {
            var gen =
                from code in Gen.Choose(0, 16)
                from message in Gen.OneOf(
                    Gen.Constant((string?)null),
                    Gen.Elements((string?)string.Empty, "resource exhausted", "multi\nline \xE9\x4E2D"))
                from detailCount in Gen.Choose(0, 4)
                from retryPosition in Gen.Choose(0, detailCount)
                from includeRetry in Gen.Elements(true, false)
                from hasDelay in Gen.Elements(true, true, false)
                from seconds in Gen.OneOf(Gen.Choose(-5, 3600).Select(static s => (long)s), Gen.Choose(0, int.MaxValue).Select(static s => (long)s))
                from nanos in Gen.OneOf(Gen.Choose(0, 999_999_999), Gen.Choose(-999_999_999, 0))
                from unknownBeforeCode in Gen.ArrayOf(UnknownField(minFieldNumber: 4), 2)
                from otherDetails in Gen.ArrayOf(OtherDetail(), detailCount)
                select BuildStatus(
                    code,
                    message,
                    otherDetails,
                    includeRetry ? retryPosition : -1,
                    hasDelay,
                    seconds,
                    nanos,
                    unknownBeforeCode);

            return gen.ToArbitrary();
        }

        public static Arbitrary<byte[]> StrayFieldArbitrary()
        {
            // The fields of a Status (code = 1 varint, message = 2 and details = 3 length-delimited) with any other wire type.
            var gen =
                from field in Gen.Choose(1, 3)
                from wireType in Gen.Elements(
                    WireFormat.WireType.Varint,
                    WireFormat.WireType.Fixed64,
                    WireFormat.WireType.LengthDelimited,
                    WireFormat.WireType.Fixed32)
                where wireType != (field == 1 ? WireFormat.WireType.Varint : WireFormat.WireType.LengthDelimited)
                from payload in Gen.ArrayOf(Gen.Choose(0, 255).Select(static b => (byte)b), 8)
                from length in Gen.Choose(0, 8)
                select BuildUnknownField(field, wireType, varint: payload[0], payload, length);

            return gen.ToArbitrary();
        }

        public static Arbitrary<byte[]> MutatedStatusArbitrary()
        {
            var gen =
                from status in StatusArbitrary().Generator
                from mutation in Gen.Choose(0, 3)
                from index in Gen.Choose(0, Math.Max(status.Data.Length - 1, 0))
                from value in Gen.Choose(0, 255)
                from count in Gen.Choose(1, 4)
                select Mutate(status.Data, mutation, index, (byte)value, count);

            return gen.ToArbitrary();
        }

        public static byte[] EncodeRetryInfo(long seconds, int nanos, bool hasDelay)
        {
            var writer = new Writer();

            if (hasDelay)
            {
                var duration = new Writer();

                if (seconds != 0)
                {
                    duration.Int64(1, seconds);
                }

                if (nanos != 0)
                {
                    duration.Int32(2, nanos);
                }

                writer.Bytes(1, duration.ToArray());
            }

            return writer.ToArray();
        }

        private static byte[] Mutate(byte[] data, int mutation, int index, byte value, int count)
        {
            if (data.Length == 0)
            {
                return [value];
            }

            var copy = (byte[])data.Clone();

            switch (mutation)
            {
                case 0:
                    // Flip a byte.
                    for (var i = 0; i < count && index + i < copy.Length; i++)
                    {
                        copy[index + i] = (byte)(copy[index + i] ^ value);
                    }

                    return copy;
                case 1:
                    // Truncate.
                    return copy.AsSpan(0, index).ToArray();
                case 2:
                    // Insert bytes.
                    return [.. copy.AsSpan(0, index), .. Enumerable.Repeat(value, count), .. copy.AsSpan(index)];
                default:
                    // Overwrite with a continuation byte, producing long varints.
                    for (var i = 0; i < count && index + i < copy.Length; i++)
                    {
                        copy[index + i] = (byte)(value | 0x80);
                    }

                    return copy;
            }
        }

        private static Gen<byte[]> UnknownField(int minFieldNumber) =>
            from fieldNumber in Gen.Choose(minFieldNumber, 2000)
            from wireType in Gen.Elements(
                WireFormat.WireType.Varint,
                WireFormat.WireType.Fixed64,
                WireFormat.WireType.LengthDelimited,
                WireFormat.WireType.Fixed32)
            from varint in Gen.Choose(0, int.MaxValue)
            from payload in Gen.ArrayOf(Gen.Choose(0, 255).Select(static b => (byte)b), 8)
            from length in Gen.Choose(0, 8)
            select BuildUnknownField(fieldNumber, wireType, varint, payload, length);

        private static byte[] BuildUnknownField(int fieldNumber, WireFormat.WireType wireType, int varint, byte[] payload, int length)
        {
            var writer = new Writer().Tag(fieldNumber, wireType);

            switch (wireType)
            {
                case WireFormat.WireType.Varint:
                    writer.RawVarint((ulong)varint);
                    break;
                case WireFormat.WireType.Fixed64:
                    writer.Raw(payload);
                    break;
                case WireFormat.WireType.LengthDelimited:
                    writer.RawVarint((ulong)length).Raw(payload.AsSpan(0, length).ToArray());
                    break;
                default:
                    writer.Raw(payload.AsSpan(0, 4).ToArray());
                    break;
            }

            return writer.ToArray();
        }

        private static Gen<byte[]> OtherDetail() =>
            from typeUrl in Gen.Elements(
                "type.googleapis.com/google.rpc.ErrorInfo",
                "type.googleapis.com/google.rpc.DebugInfo",
                string.Empty)
            from value in Gen.ArrayOf(Gen.Choose(0, 255).Select(static b => (byte)b), 6)
            from unknown in UnknownField(minFieldNumber: 3)
            select new Writer().String(1, typeUrl).Bytes(2, value).Raw(unknown).ToArray();

        private static StatusModel BuildStatus(
            int code,
            string? message,
            byte[][] otherDetails,
            int retryPosition,
            bool hasDelay,
            long seconds,
            int nanos,
            byte[][] unknownFields)
        {
            var writer = new Writer();

            if (code != 0)
            {
                writer.Int32(1, code);
            }

            if (message is not null)
            {
                writer.String(2, message);
            }

            foreach (var unknown in unknownFields)
            {
                writer.Raw(unknown);
            }

            var detailCount = otherDetails.Length + (retryPosition >= 0 ? 1 : 0);

            TimeSpan? expected = null;

            for (var i = 0; i <= otherDetails.Length; i++)
            {
                if (i == retryPosition)
                {
                    var retryInfo = EncodeRetryInfo(seconds, nanos, hasDelay);
                    var any = new Writer().String(1, RetryInfoTypeUrl).Bytes(2, retryInfo).ToArray();
                    writer.Bytes(3, any);

                    if (hasDelay)
                    {
                        var delay = TimeSpan.FromSeconds(seconds) + TimeSpan.FromTicks(nanos / 100);
                        expected = delay < TimeSpan.Zero ? TimeSpan.Zero : delay;
                    }
                }

                if (i < otherDetails.Length)
                {
                    writer.Bytes(3, otherDetails[i]);
                }
            }

            return new StatusModel
            {
                Data = writer.ToArray(),
                Code = code,
                Message = message,
                DetailCount = detailCount,
                ExpectedRetryDelay = expected,
            };
        }
    }

    private sealed class Writer
    {
        private readonly List<byte> bytes = [];

        public Writer Tag(int fieldNumber, WireFormat.WireType wireType)
            => this.RawVarint(((uint)fieldNumber << 3) | (uint)wireType);

        public Writer RawVarint(ulong value)
        {
            while (value >= 0x80)
            {
                this.bytes.Add((byte)(value | 0x80));
                value >>= 7;
            }

            this.bytes.Add((byte)value);
            return this;
        }

        public Writer Raw(byte[] value)
        {
            this.bytes.AddRange(value);
            return this;
        }

        public Writer Int32(int fieldNumber, int value)
            => this.Tag(fieldNumber, WireFormat.WireType.Varint).RawVarint((ulong)(long)value);

        public Writer Int64(int fieldNumber, long value)
            => this.Tag(fieldNumber, WireFormat.WireType.Varint).RawVarint((ulong)value);

        public Writer String(int fieldNumber, string value)
            => this.Bytes(fieldNumber, Encoding.UTF8.GetBytes(value));

        public Writer Bytes(int fieldNumber, byte[] value)
            => this.Tag(fieldNumber, WireFormat.WireType.LengthDelimited).RawVarint((ulong)value.Length).Raw(value);

        public byte[] ToArray() => [.. this.bytes];
    }

    private sealed class StatusModel
    {
        public byte[] Data { get; set; } = [];

        public int Code { get; set; }

        public string? Message { get; set; }

        public int DetailCount { get; set; }

        public TimeSpan? ExpectedRetryDelay { get; set; }
    }
}

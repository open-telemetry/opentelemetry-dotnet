// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using System.Text;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient.Grpc;

internal static class GrpcStatusDeserializer
{
#pragma warning disable SA1310 // Field names should not contain underscore
    // Wire types in protocol buffers
    private const int WIRETYPE_VARINT = 0;
    private const int WIRETYPE_FIXED64 = 1;
    private const int WIRETYPE_LENGTH_DELIMITED = 2;
    private const int WIRETYPE_FIXED32 = 5;
#pragma warning restore SA1310 // Field names should not contain underscore

    internal static TimeSpan? TryGetGrpcRetryDelay(string? grpcStatusDetailsHeader)
    {
        try
        {
            var retryInfo = ExtractRetryInfo(grpcStatusDetailsHeader);
            if (retryInfo?.RetryDelay != null)
            {
                return TimeSpan.FromSeconds(retryInfo.Value.RetryDelay.Value.Seconds) +
                       TimeSpan.FromTicks(retryInfo.Value.RetryDelay.Value.Nanos / 100); // Convert nanos to ticks
            }
        }
        catch (Exception ex)
        {
            OpenTelemetryProtocolExporterEventSource.Log.GrpcRetryDelayParsingFailed(grpcStatusDetailsHeader, ex);
            return null;
        }

        return null;
    }

    // Marked as internal for test.
    internal static Status? DeserializeStatus(string? grpcStatusDetailsBin)
    {
        if (string.IsNullOrWhiteSpace(grpcStatusDetailsBin))
        {
            return null;
        }

        var status = new Status();
        byte[] data = Convert.FromBase64String(grpcStatusDetailsBin);
        using (var stream = new MemoryStream(data))
        {
            while (stream.Position < stream.Length)
            {
                var tag = DecodeTag(stream);
                var fieldNumber = tag >> 3;
                var wireType = tag & 0x7;

                switch (fieldNumber)
                {
                    case 1: // code
                        status.Code = DecodeInt32(stream);
                        break;
                    case 2: // message
                        status.Message = DecodeString(stream);
                        break;
                    case 3: // details
                        status.Details.Add(DecodeAny(stream));
                        break;
                    default:
                        SkipField(stream, wireType);
                        break;
                }
            }
        }

        return status;
    }

    // Marked as internal for test.
    internal static RetryInfo? ExtractRetryInfo(string? grpcStatusDetailsBin)
    {
        var status = DeserializeStatus(grpcStatusDetailsBin);
        if (status == null)
        {
            return null;
        }

        foreach (var detail in status.Value.Details)
        {
            if (detail.TypeUrl != null && detail.TypeUrl.EndsWith("/google.rpc.RetryInfo"))
            {
                return DeserializeRetryInfo(detail.Value!);
            }
        }

        return null;
    }

    private static RetryInfo? DeserializeRetryInfo(byte[] data)
    {
        RetryInfo? retryInfo = null;
        using (var stream = new MemoryStream(data))
        {
            while (stream.Position < stream.Length)
            {
                var tag = DecodeTag(stream);
                var fieldNumber = tag >> 3;
                var wireType = tag & 0x7;

                switch (fieldNumber)
                {
                    case 1: // retry_delay
                        retryInfo = new RetryInfo(DecodeDuration(stream));
                        break;
                    default:
                        SkipField(stream, wireType);
                        break;
                }
            }
        }

        return retryInfo;
    }

    private static Duration DecodeDuration(Stream stream)
    {
        var length = DecodeVarint(stream);
        var endPosition = stream.Position + length;
        long seconds = 0;
        int nanos = 0;

        while (stream.Position < endPosition)
        {
            var tag = DecodeTag(stream);
            var fieldNumber = tag >> 3;
            var wireType = tag & 0x7;

            switch (fieldNumber)
            {
                case 1: // seconds
                    seconds = DecodeInt64(stream);
                    break;
                case 2: // nanos
                    nanos = DecodeInt32(stream);
                    break;
                default:
                    SkipField(stream, wireType);
                    break;
            }
        }

        return new Duration(seconds, nanos);
    }

    private static Any DecodeAny(Stream stream)
    {
        var length = DecodeVarint(stream);
        var endPosition = stream.Position + length;

        string? typeUrl = null;
        byte[]? value = null;

        while (stream.Position < endPosition)
        {
            var tag = DecodeTag(stream);
            var fieldNumber = tag >> 3;
            var wireType = tag & 0x7;

            switch (fieldNumber)
            {
                case 1: // type_url
                    typeUrl = DecodeString(stream);
                    break;
                case 2: // value
                    value = DecodeBytes(stream);
                    break;
                default:
                    SkipField(stream, wireType);
                    break;
            }
        }

        return new Any(typeUrl, value);
    }

    private static uint DecodeTag(Stream stream)
    {
        return (uint)DecodeVarint(stream);
    }

    private static long DecodeVarint(Stream stream)
    {
        long result = 0;
        int shift = 0;

        while (true)
        {
            int b = stream.ReadByte();
            if (b == -1)
            {
                throw new EndOfStreamException();
            }

            result |= (long)(b & 0b_0111_1111) << shift;
            if ((b & 0b_1000_0000) == 0)
            {
                return result;
            }

            shift += 7;
            if (shift >= 64)
            {
                throw new InvalidDataException("Invalid varint");
            }
        }
    }

    private static int DecodeInt32(Stream stream) => (int)DecodeVarint(stream);

    private static long DecodeInt64(Stream stream) => DecodeVarint(stream);

    private static string DecodeString(Stream stream)
    {
        var bytes = DecodeBytes(stream);
        return Encoding.UTF8.GetString(bytes);
    }

    private static byte[] DecodeBytes(Stream stream)
    {
        var length = (int)DecodeVarint(stream);
        var buffer = new byte[length];
        int read = stream.Read(buffer, 0, length);
        if (read != length)
        {
            throw new EndOfStreamException();
        }

        return buffer;
    }

    private static void SkipField(Stream stream, uint wireType)
    {
        switch (wireType)
        {
            case WIRETYPE_VARINT:
                DecodeVarint(stream);
                break;
            case WIRETYPE_FIXED64:
                stream.Position += 8;
                break;
            case WIRETYPE_LENGTH_DELIMITED:
                var length = DecodeVarint(stream);
                stream.Position += length;
                break;
            case WIRETYPE_FIXED32:
                stream.Position += 4;
                break;
            default:
                throw new InvalidDataException($"Unknown wire type: {wireType}");
        }
    }

    internal readonly struct Duration
    {
        internal Duration(long seconds, int nanos)
        {
            this.Seconds = seconds;
            this.Nanos = nanos;
        }

        public long Seconds { get; }

        public int Nanos { get; }
    }

    internal readonly struct RetryInfo
    {
        public RetryInfo(Duration? retryDelay)
        {
            this.RetryDelay = retryDelay;
        }

        public Duration? RetryDelay { get; }
    }

    internal readonly struct Any
    {
        public Any(string? typeUrl, byte[]? value)
        {
            this.TypeUrl = typeUrl;
            this.Value = value;
        }

        public string? TypeUrl { get; }

        public byte[]? Value { get; }
    }

    internal struct Status
    {
        public Status()
        {
            this.Code = 0;
            this.Message = null;
            this.Details = [];
        }

        public int Code { get; set; }

        public string? Message { get; set; }

        public List<Any> Details { get; set; }
    }
}

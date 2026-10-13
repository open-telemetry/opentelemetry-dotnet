// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text;

namespace OpenTelemetry.Internal.Tests;

public class JsonStringArrayTagWriterTests
{
    private static readonly int[] SmallArray = [1, 2, 3];

    [Theory]
    [InlineData([new char[] { }, "[]"])]
    [InlineData([new char[] { 'a' }, """["a"]"""])]
    [InlineData([new char[] { '1', '2', '3' }, """["1","2","3"]"""])]
    public void CharArray(char[] data, string expectedValue)
        => VerifySerialization(data, expectedValue);

    [Theory]
    [InlineData([new string[] { }, "[]"])]
    [InlineData([new string[] { "one" }, """["one"]"""])]
#pragma warning disable SA1122 // Use string.Empty for empty strings
    [InlineData([new string[] { "" }, """[""]"""])]
#pragma warning restore SA1122 // Use string.Empty for empty strings
    [InlineData([new string[] { "a", "b", "c", "d" }, """["a","b","c","d"]"""])]
    [InlineData([new string[] { "\r\n", "\t", "\"" }, """["\r\n","\t","\u0022"]"""])]
    [InlineData([new string[] { "longlonglonglonglonglonglonglonglong" }, """["longlonglonglonglonglonglonglonglong"]"""])]
    public void StringArray(string[] data, string expectedValue)
        => VerifySerialization(data, expectedValue);

    [Theory]
    [InlineData([new bool[] { }, "[]"])]
    [InlineData([new bool[] { true }, "[true]"])]
    [InlineData([new bool[] { true, false, false, true }, "[true,false,false,true]"])]
    public void BooleanArray(bool[] data, string expectedValue)
        => VerifySerialization(data, expectedValue);

    [Theory]
    [InlineData([new byte[] { }, "[]"])]
    [InlineData([new byte[] { 0 }, "[0]"])]
    [InlineData([new byte[] { byte.MaxValue, byte.MinValue, 4, 13 }, "[255,0,4,13]"])]
    public void ByteArray(byte[] data, string expectedValue)
        => VerifySerialization(data, expectedValue);

    [Theory]
    [InlineData([new sbyte[] { }, "[]"])]
    [InlineData([new sbyte[] { 0 }, "[0]"])]
    [InlineData([new sbyte[] { sbyte.MaxValue, sbyte.MinValue, 4, 13 }, "[127,-128,4,13]"])]
    public void SByteArray(sbyte[] data, string expectedValue)
        => VerifySerialization(data, expectedValue);

    [Theory]
    [InlineData([new short[] { }, "[]"])]
    [InlineData([new short[] { 0 }, "[0]"])]
    [InlineData([new short[] { short.MaxValue, short.MinValue, 4, 13 }, "[32767,-32768,4,13]"])]
    public void ShortArray(short[] data, string expectedValue)
        => VerifySerialization(data, expectedValue);

    [Theory]
    [InlineData([new ushort[] { }, "[]"])]
    [InlineData([new ushort[] { 0 }, "[0]"])]
    [InlineData([new ushort[] { ushort.MaxValue, ushort.MinValue, 4, 13 }, "[65535,0,4,13]"])]
    public void UShortArray(ushort[] data, string expectedValue)
        => VerifySerialization(data, expectedValue);

    [Theory]
    [InlineData([new int[] { }, "[]"])]
    [InlineData([new int[] { 0 }, "[0]"])]
    [InlineData([new int[] { int.MaxValue, int.MinValue, 4, 13 }, "[2147483647,-2147483648,4,13]"])]
    public void IntArray(int[] data, string expectedValue)
        => VerifySerialization(data, expectedValue);

    [Theory]
    [InlineData([new uint[] { }, "[]"])]
    [InlineData([new uint[] { 0 }, "[0]"])]
    [InlineData([new uint[] { uint.MaxValue, uint.MinValue, 4, 13 }, "[4294967295,0,4,13]"])]
    public void UIntArray(uint[] data, string expectedValue)
        => VerifySerialization(data, expectedValue);

    [Theory]
    [InlineData([new long[] { }, "[]"])]
    [InlineData([new long[] { 0 }, "[0]"])]
    [InlineData([new long[] { long.MaxValue, long.MinValue, 4, 13 }, "[9223372036854775807,-9223372036854775808,4,13]"])]
    public void LongArray(long[] data, string expectedValue)
        => VerifySerialization(data, expectedValue);

    [Theory]
    [InlineData([new ulong[] { }, "[]"])]
    [InlineData([new ulong[] { 0 }, """["0"]"""])]
    [InlineData([new ulong[] { ulong.MaxValue, ulong.MinValue, 4, 13 }, """["18446744073709551615","0","4","13"]"""])]
    public void ULongArray(ulong[] data, string expectedValue)
        => VerifySerialization(data, expectedValue);

    [Theory]
    [InlineData([new float[] { }, "[]"])]
    [InlineData([new float[] { 0 }, "[0]"])]
    [InlineData([new float[] { float.MaxValue, float.MinValue, 4, 13 }, "[3.4028234663852886E+38,-3.4028234663852886E+38,4,13]"])]
#if NETFRAMEWORK
    [InlineData([new float[] { float.Epsilon }, "[1.4012984643248171E-45]"])]
#else
    [InlineData([new float[] { float.Epsilon }, "[1.401298464324817E-45]"])]
#endif
    public void FloatArray(float[] data, string expectedValue)
        => VerifySerialization(data, expectedValue);

    [Theory]
    [InlineData([new double[] { }, "[]"])]
    [InlineData([new double[] { 0 }, "[0]"])]
    [InlineData([new double[] { double.MaxValue, double.MinValue, 4, 13 }, "[1.7976931348623157E+308,-1.7976931348623157E+308,4,13]"])]
#if NETFRAMEWORK
    [InlineData([new double[] { double.Epsilon }, "[4.9406564584124654E-324]"])]
#else
    [InlineData([new double[] { double.Epsilon }, "[5E-324]"])]
#endif
    public void DoubleArray(double[] data, string expectedValue)
        => VerifySerialization(data, expectedValue);

    [Theory]
    [InlineData([new object?[] { }, "[]"])]
    [InlineData([new object?[] { null, float.MinValue, float.MaxValue, double.MinValue, double.MaxValue, int.MinValue, int.MaxValue, long.MinValue, long.MaxValue, true, false, "Hello world", new object[] { "inner array" } }, """[null,-3.4028234663852886E+38,3.4028234663852886E+38,-1.7976931348623157E+308,1.7976931348623157E+308,-2147483648,2147483647,-9223372036854775808,9223372036854775807,true,false,"Hello world",["inner array"]]"""])]
    [InlineData([new object?[] { new byte[] { 1, 2, 3 }, new sbyte[] { -1, 0, 127 } }, """["AQID",[-1,0,127]]"""])]
    public void ObjectArray(object?[] data, string expectedValue)
        => VerifySerialization(data, expectedValue);

    [Fact]
    public void ThreadStaticStreamCapacityIsReducedAfterLargeWrite()
    {
        var writer = new JsonStringArrayTagWriter<TestTagWriter.Tag>.JsonArrayTagWriter(long.MaxValue);
        var largeValue = new string('x', 128 * 1024);
        var largeState = VerifySerialization(writer, largeValue, $"""["{largeValue}"]""");

        Assert.True(largeState.Stream.Capacity > 64 * 1024);

        var reusedState = VerifySerialization(writer, "small", """["small"]""");

        Assert.Same(largeState.Stream, reusedState.Stream);
        Assert.NotSame(largeState.Writer, reusedState.Writer);
        Assert.True(reusedState.Stream.Capacity <= 64 * 1024);

        var nextState = VerifySerialization(writer, "small", """["small"]""");

        Assert.Same(reusedState.Writer, nextState.Writer);
    }

    [Fact]
    public void ThreadStaticWriterIsReplacedAfterLargeAbandonedWrite()
    {
        var writer = new JsonStringArrayTagWriter<TestTagWriter.Tag>.JsonArrayTagWriter(128 * 1024);
        var abandonedState = writer.BeginWriteArray();
        var value = new string('x', 96 * 1024);
        writer.WriteStringValue(ref abandonedState, value);

        Assert.Throws<ArgumentException>(() => writer.WriteStringValue(ref abandonedState, value));

        Assert.True(abandonedState.Writer.BytesPending > 64 * 1024);
        Assert.True(abandonedState.Stream.Capacity <= 64 * 1024);

        var reusedState = VerifySerialization(writer, "small", """["small"]""");

        Assert.NotSame(abandonedState.Writer, reusedState.Writer);
        Assert.Same(abandonedState.Stream, reusedState.Stream);

        // Discarding the old writer must not flush its rejected JSON into the stream.
        Assert.True(reusedState.Stream.Capacity <= 64 * 1024);
    }

    [Theory]
    [InlineData(new[] { "short" }, """["short"]""")]
    [InlineData(new[] { "012345678901234567890123456789012345678901234567890123456789" }, """["012345678901234567890123456789012345678901234567890123456789"]""")]
    [InlineData(new[] { "012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789" }, "TRUNCATED")]
    [InlineData(new[] { "\u0001\u0001\u0001\u0001\u0001\u0001\u0001\u0001\u0001\u0001\u0001\u0001\u0001\u0001\u0001\u0001\u0001\u0001\u0001\u0001" }, "TRUNCATED")]
    [InlineData(new[] { "0123456789", "0123456789", "0123456789", "0123456789", "0123456789", "0123456789", "0123456789" }, "TRUNCATED")]
    public void ArrayWhoseJsonExceedsMaximumLengthIsTruncated(string[] data, string expectedValue)
    {
        TestTagWriter.Tag tag = default;
        var result = TestTagWriter.Limited.TryWriteTag(ref tag, new KeyValuePair<string, object?>("array", data));

        Assert.True(result, $"TryWriteTag returned false instead of writing '{expectedValue}'.");
        Assert.Equal(expectedValue, tag.Value);
    }

    [Fact]
    public void GetEscapedLengthMatchesLengthWrittenByJsonWriter()
    {
        using var stream = new MemoryStream();
        var characters = new char[1];
        var mismatches = new List<string>();

        // Surrogates are only valid as pairs, so are covered separately.
        for (var c = 0; c <= char.MaxValue; c++)
        {
            if (char.IsSurrogate((char)c))
            {
                continue;
            }

            characters[0] = (char)c;

            var written = WrittenLength(stream, characters);
            var counted = TestTagWriter.GetEscapedLength(characters, long.MaxValue);

            if (written != counted)
            {
                mismatches.Add($"U+{c:X4}: JSON writer wrote {written} byte(s) but {counted} were counted.");
            }
        }

        Assert.True(mismatches.Count == 0, $"{mismatches.Count} character(s) were counted incorrectly: {string.Join(" ", mismatches.Take(10))}");

        characters = "\ud83d\ude00".ToCharArray();
        Assert.Equal(WrittenLength(stream, characters), TestTagWriter.GetEscapedLength(characters, long.MaxValue));

        static long WrittenLength(MemoryStream stream, char[] value)
        {
            stream.SetLength(0);
            using var writer = new System.Text.Json.Utf8JsonWriter(stream);
            writer.WriteStringValue(value);
            writer.Flush();

            // Excludes the quotes
            return stream.Length - 2;
        }
    }

    [Fact]
    public void GetEscapedLengthStopsCountingOnceLimitIsExceeded()
    {
        var value = new string('\u00e9', 1000);

        Assert.Equal(54, TestTagWriter.GetEscapedLength(value, 50));
        Assert.Equal(6000, TestTagWriter.GetEscapedLength(value, 6000));
    }

    [Fact]
    public void ArrayWhoseJsonExceedsMaximumLengthDoesNotAffectNextArray()
    {
        var oversized = new string('x', 1000).Split('x');

        TestTagWriter.Tag tag = default;
        var result = TestTagWriter.Limited.TryWriteTag(ref tag, new KeyValuePair<string, object?>("array", oversized));

        Assert.True(result, $"TryWriteTag returned false for an oversized array of {oversized.Length} element(s) instead of writing 'TRUNCATED'.");
        Assert.Equal("TRUNCATED", tag.Value);

        tag = default;
        result = TestTagWriter.Limited.TryWriteTag(ref tag, new KeyValuePair<string, object?>("array", SmallArray));

        Assert.True(result, $"TryWriteTag returned false for an array of {SmallArray.Length} element(s) written after an oversized array.");
        Assert.Equal("[1,2,3]", tag.Value);
    }

    private static void VerifySerialization(Array data, string expectedValue)
    {
        TestTagWriter.Tag tag = default;
        var result = TestTagWriter.Instance.TryWriteTag(ref tag, new KeyValuePair<string, object?>("array", data));

        Assert.True(result);
        Assert.Equal(expectedValue, tag.Value);
    }

    private static JsonStringArrayTagWriter<TestTagWriter.Tag>.JsonArrayTagWriterState VerifySerialization(
        JsonStringArrayTagWriter<TestTagWriter.Tag>.JsonArrayTagWriter writer,
        string value,
        string expectedValue)
    {
        var state = writer.BeginWriteArray();
        writer.WriteStringValue(ref state, value);
        writer.EndWriteArray(ref state);

        Assert.Equal(expectedValue, Encoding.UTF8.GetString(state.Stream.GetBuffer(), 0, (int)state.Stream.Length));

        return state;
    }

    private sealed class TestTagWriter : JsonStringArrayTagWriter<TestTagWriter.Tag>
    {
        private TestTagWriter()
        {
        }

        private TestTagWriter(long maxJsonLength)
            : base(maxJsonLength)
        {
        }

        public static TestTagWriter Instance { get; } = new();

        public static TestTagWriter Limited { get; } = new(80);

        protected override void WriteIntegralTag(ref Tag state, string key, long value)
            => throw new NotImplementedException();

        protected override void WriteFloatingPointTag(ref Tag state, string key, double value)
            => throw new NotImplementedException();

        protected override void WriteBooleanTag(ref Tag state, string key, bool value)
            => throw new NotImplementedException();

        protected override void WriteStringTag(ref Tag state, string key, ReadOnlySpan<char> value)
        {
            state.Key = key;
            state.Value = value.ToString();
        }

        protected override void WriteArrayTag(ref Tag state, string key, ArraySegment<byte> arrayUtf8JsonBytes)
        {
#pragma warning disable IDE0370 // Suppression is unnecessary
            state.Key = key;
            state.Value = Encoding.UTF8.GetString(arrayUtf8JsonBytes.Array!, 0, arrayUtf8JsonBytes.Count);
#pragma warning restore IDE0370 // Suppression is unnecessary
        }

        protected override void OnUnsupportedTagDropped(string tagKey, string tagValueTypeFullName)
        {
        }

        protected override bool TryWriteEmptyTag(ref Tag state, string key, object? value)
            => throw new NotImplementedException();

        protected override bool TryWriteByteArrayTag(ref Tag state, string key, ReadOnlySpan<byte> value) => false;

        protected override void WriteKvListTag(ref Tag state, string key, IEnumerable<KeyValuePair<string, object?>> kvList, int? tagValueMaxLength)
            => throw new NotImplementedException();

        public struct Tag
        {
            public string? Key;
            public string? Value;
        }
    }
}

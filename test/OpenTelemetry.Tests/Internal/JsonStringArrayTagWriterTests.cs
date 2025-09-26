// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using Xunit;

namespace OpenTelemetry.Internal.Tests;

public class JsonStringArrayTagWriterTests
{
    [Theory]
    [InlineData(new object[] { new char[] { }, "[]" })]
    [InlineData(new object[] { new char[] { 'a' }, """["a"]""" })]
    [InlineData(new object[] { new char[] { '1', '2', '3' }, """["1","2","3"]""" })]
    public void CharArray(char[] data, string expectedValue)
    {
        VerifySerialization(data, expectedValue);
    }

    [Theory]
    [InlineData(new object[] { new string[] { }, "[]" })]
    [InlineData(new object[] { new string[] { "one" }, """["one"]""" })]
    [InlineData(new object[] { new string[] { "" }, """[""]""" })]
    [InlineData(new object[] { new string[] { "a", "b", "c", "d" }, """["a","b","c","d"]""" })]
    [InlineData(new object[] { new string[] { "\r\n", "\t", "\"" }, """["\r\n","\t","\u0022"]""" })]
    [InlineData(new object[] { new string[] { "longlonglonglonglonglonglonglonglong" }, """["longlonglonglonglonglonglonglonglong"]""" })]
    public void StringArray(string[] data, string expectedValue)
    {
        VerifySerialization(data, expectedValue);
    }

    [Theory]
    [InlineData(new object[] { new bool[] { }, "[]" })]
    [InlineData(new object[] { new bool[] { true }, "[true]" })]
    [InlineData(new object[] { new bool[] { true, false, false, true }, "[true,false,false,true]" })]
    public void BooleanArray(bool[] data, string expectedValue)
    {
        VerifySerialization(data, expectedValue);
    }

    [Theory]
    [InlineData(new object[] { new byte[] { }, "[]" })]
    [InlineData(new object[] { new byte[] { 0 }, "[0]" })]
    [InlineData(new object[] { new byte[] { byte.MaxValue, byte.MinValue, 4, 13 }, "[255,0,4,13]" })]
    public void ByteArray(byte[] data, string expectedValue)
    {
        VerifySerialization(data, expectedValue);
    }

    [Theory]
    [InlineData(new object[] { new sbyte[] { }, "[]" })]
    [InlineData(new object[] { new sbyte[] { 0 }, "[0]" })]
    [InlineData(new object[] { new sbyte[] { sbyte.MaxValue, sbyte.MinValue, 4, 13 }, "[127,-128,4,13]" })]
    public void SByteArray(sbyte[] data, string expectedValue)
    {
        VerifySerialization(data, expectedValue);
    }

    [Theory]
    [InlineData(new object[] { new short[] { }, "[]" })]
    [InlineData(new object[] { new short[] { 0 }, "[0]" })]
    [InlineData(new object[] { new short[] { short.MaxValue, short.MinValue, 4, 13 }, "[32767,-32768,4,13]" })]
    public void ShortArray(short[] data, string expectedValue)
    {
        VerifySerialization(data, expectedValue);
    }

    [Theory]
    [InlineData(new object[] { new ushort[] { }, "[]" })]
    [InlineData(new object[] { new ushort[] { 0 }, "[0]" })]
    [InlineData(new object[] { new ushort[] { ushort.MaxValue, ushort.MinValue, 4, 13 }, "[65535,0,4,13]" })]
    public void UShortArray(ushort[] data, string expectedValue)
    {
        VerifySerialization(data, expectedValue);
    }

    [Theory]
    [InlineData(new object[] { new int[] { }, "[]" })]
    [InlineData(new object[] { new int[] { 0 }, "[0]" })]
    [InlineData(new object[] { new int[] { int.MaxValue, int.MinValue, 4, 13 }, "[2147483647,-2147483648,4,13]" })]
    public void IntArray(int[] data, string expectedValue)
    {
        VerifySerialization(data, expectedValue);
    }

    [Theory]
    [InlineData(new object[] { new uint[] { }, "[]" })]
    [InlineData(new object[] { new uint[] { 0 }, "[0]" })]
    [InlineData(new object[] { new uint[] { uint.MaxValue, uint.MinValue, 4, 13 }, "[4294967295,0,4,13]" })]
    public void UIntArray(uint[] data, string expectedValue)
    {
        VerifySerialization(data, expectedValue);
    }

    [Theory]
    [InlineData(new object[] { new long[] { }, "[]" })]
    [InlineData(new object[] { new long[] { 0 }, "[0]" })]
    [InlineData(new object[] { new long[] { long.MaxValue, long.MinValue, 4, 13 }, "[9223372036854775807,-9223372036854775808,4,13]" })]
    public void LongArray(long[] data, string expectedValue)
    {
        VerifySerialization(data, expectedValue);
    }

    [Theory]
    [InlineData(new object[] { new ulong[] { }, "[]" })]
    [InlineData(new object[] { new ulong[] { 0 }, """["0"]""" })]
    [InlineData(new object[] { new ulong[] { ulong.MaxValue, ulong.MinValue, 4, 13 }, """["18446744073709551615","0","4","13"]""" })]
    public void ULongArray(ulong[] data, string expectedValue)
    {
        VerifySerialization(data, expectedValue);
    }

    [Theory]
    [InlineData(new object[] { new float[] { }, "[]" })]
    [InlineData(new object[] { new float[] { 0 }, "[0]" })]
    [InlineData(new object[] { new float[] { float.MaxValue, float.MinValue, 4, 13 }, "[3.4028234663852886E+38,-3.4028234663852886E+38,4,13]" })]
#if NETFRAMEWORK
    [InlineData(new object[] { new float[] { float.Epsilon }, "[1.4012984643248171E-45]" })]
#else
    [InlineData(new object[] { new float[] { float.Epsilon }, "[1.401298464324817E-45]" })]
#endif
    public void FloatArray(float[] data, string expectedValue)
    {
        VerifySerialization(data, expectedValue);
    }

    [Theory]
    [InlineData(new object[] { new double[] { }, "[]" })]
    [InlineData(new object[] { new double[] { 0 }, "[0]" })]
    [InlineData(new object[] { new double[] { double.MaxValue, double.MinValue, 4, 13 }, "[1.7976931348623157E+308,-1.7976931348623157E+308,4,13]" })]
#if NETFRAMEWORK
    [InlineData(new object[] { new double[] { double.Epsilon }, "[4.9406564584124654E-324]" })]
#else
    [InlineData(new object[] { new double[] { double.Epsilon }, "[5E-324]" })]
#endif
    public void DoubleArray(double[] data, string expectedValue)
    {
        VerifySerialization(data, expectedValue);
    }

    [Theory]
    [InlineData(new object[] { new object?[] { }, "[]" })]
    [InlineData(new object[] { new object?[] { null, float.MinValue, float.MaxValue, double.MinValue, double.MaxValue, int.MinValue, int.MaxValue, long.MinValue, long.MaxValue, true, false, "Hello world", new object[] { "inner array" } }, """[null,-3.4028234663852886E+38,3.4028234663852886E+38,-1.7976931348623157E+308,1.7976931348623157E+308,-2147483648,2147483647,-9223372036854775808,9223372036854775807,true,false,"Hello world","System.Object[]"]""" })]
    public void ObjectArray(object?[] data, string expectedValue)
    {
        VerifySerialization(data, expectedValue);
    }

    private static void VerifySerialization(Array data, string expectedValue)
    {
        TestTagWriter.Tag tag = default;
        var result = TestTagWriter.Instance.TryWriteTag(ref tag, new KeyValuePair<string, object?>("array", data));

        Assert.True(result);
        Assert.Equal(expectedValue, tag.Value);
    }

    private sealed class TestTagWriter : JsonStringArrayTagWriter<TestTagWriter.Tag>
    {
        private TestTagWriter()
        {
        }

        public static TestTagWriter Instance { get; } = new();

        protected override void WriteIntegralTag(ref Tag tag, string key, long value)
        {
            throw new NotImplementedException();
        }

        protected override void WriteFloatingPointTag(ref Tag tag, string key, double value)
        {
            throw new NotImplementedException();
        }

        protected override void WriteBooleanTag(ref Tag tag, string key, bool value)
        {
            throw new NotImplementedException();
        }

        protected override void WriteStringTag(ref Tag tag, string key, ReadOnlySpan<char> value)
        {
            throw new NotImplementedException();
        }

        protected override void WriteArrayTag(ref Tag tag, string key, ArraySegment<byte> arrayUtf8JsonBytes)
        {
            tag.Key = key;
            tag.Value = Encoding.UTF8.GetString(arrayUtf8JsonBytes.Array!, 0, arrayUtf8JsonBytes.Count);
        }

        protected override void OnUnsupportedTagDropped(string tagKey, string tagValueTypeFullName)
        {
        }

        protected override bool TryWriteEmptyTag(ref Tag state, string key, object? value)
        {
            throw new NotImplementedException();
        }

        protected override bool TryWriteByteArrayTag(ref Tag consoleTag, string key, ReadOnlySpan<byte> value) => false;

        public struct Tag
        {
            public string? Key;
            public string? Value;
        }
    }
}

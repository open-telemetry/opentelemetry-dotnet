// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0License.
// </copyright>

using OpenTelemetry.Internal;
using Xunit;

namespace OpenTelemetry.Tests.Shared;

public class TagTransformerJsonHelperTest
{
    [Theory]
    [InlineData(new object[] { new char[] { } })]
    [InlineData(new object[] { new char[] { 'a' } })]
    [InlineData(new object[] { new char[] { '1', '2', '3' } })]
    public void CharArray(char[] data)
    {
        VerifySerialization(data);
    }

    [Theory]
    [InlineData(new object[] { new string[] { } })]
    [InlineData(new object[] { new string[] { "one" } })]
    [InlineData(new object[] { new string[] { "" } })]
    [InlineData(new object[] { new string[] { "a", "b", "c", "d" } })]
    [InlineData(new object[] { new string[] { "\r\n", "\t", "\"" } })]
    [InlineData(new object[] { new string[] { "longlonglonglonglonglonglonglonglong" } })]
    public void StringArray(string[] data)
    {
        VerifySerialization(data);
    }

    [Theory]
    [InlineData(new object[] { new bool[] { } })]
    [InlineData(new object[] { new bool[] { true } })]
    [InlineData(new object[] { new bool[] { true, false, false, true } })]
    public void BooleanArray(bool[] data)
    {
        VerifySerialization(data);
    }

    [Theory]
    [InlineData(new object[] { new byte[] { } })]
    [InlineData(new object[] { new byte[] { 0 } })]
    [InlineData(new object[] { new byte[] { byte.MaxValue, byte.MinValue, 4, 13 } })]
    public void ByteArray(byte[] data)
    {
        VerifySerialization(data);
    }

    [Theory]
    [InlineData(new object[] { new sbyte[] { } })]
    [InlineData(new object[] { new sbyte[] { 0 } })]
    [InlineData(new object[] { new sbyte[] { sbyte.MaxValue, sbyte.MinValue, 4, 13 } })]
    public void SByteArray(sbyte[] data)
    {
        VerifySerialization(data);
    }

    [Theory]
    [InlineData(new object[] { new short[] { } })]
    [InlineData(new object[] { new short[] { 0 } })]
    [InlineData(new object[] { new short[] { short.MaxValue, short.MinValue, 4, 13 } })]
    public void ShortArray(short[] data)
    {
        VerifySerialization(data);
    }

    [Theory]
    [InlineData(new object[] { new ushort[] { } })]
    [InlineData(new object[] { new ushort[] { 0 } })]
    [InlineData(new object[] { new ushort[] { ushort.MaxValue, ushort.MinValue, 4, 13 } })]
    public void UShortArray(ushort[] data)
    {
        VerifySerialization(data);
    }

    [Theory]
    [InlineData(new object[] { new int[] { } })]
    [InlineData(new object[] { new int[] { 0 } })]
    [InlineData(new object[] { new int[] { int.MaxValue, int.MinValue, 4, 13 } })]
    public void IntArray(int[] data)
    {
        VerifySerialization(data);
    }

    [Theory]
    [InlineData(new object[] { new uint[] { } })]
    [InlineData(new object[] { new uint[] { 0 } })]
    [InlineData(new object[] { new uint[] { uint.MaxValue, uint.MinValue, 4, 13 } })]
    public void UIntArray(uint[] data)
    {
        VerifySerialization(data);
    }

    [Theory]
    [InlineData(new object[] { new long[] { } })]
    [InlineData(new object[] { new long[] { 0 } })]
    [InlineData(new object[] { new long[] { long.MaxValue, long.MinValue, 4, 13 } })]
    public void LongArray(long[] data)
    {
        VerifySerialization(data);
    }

    [Theory]
    [InlineData(new object[] { new ulong[] { } })]
    [InlineData(new object[] { new ulong[] { 0 } })]
    [InlineData(new object[] { new ulong[] { ulong.MaxValue, ulong.MinValue, 4, 13 } })]
    public void ULongArray(ulong[] data)
    {
        VerifySerialization(data);
    }

    [Theory]
    [InlineData(new object[] { new float[] { } })]
    [InlineData(new object[] { new float[] { 0 } })]
    [InlineData(new object[] { new float[] { float.MaxValue, float.MinValue, 4, 13 } })]
    [InlineData(new object[] { new float[] { float.Epsilon } })]
    public void FloatArray(float[] data)
    {
        VerifySerialization(data);
    }

    [Theory]
    [InlineData(new object[] { new double[] { } })]
    [InlineData(new object[] { new double[] { 0 } })]
    [InlineData(new object[] { new double[] { double.MaxValue, double.MinValue, 4, 13 } })]
    [InlineData(new object[] { new double[] { double.Epsilon } })]
    public void DoubleArray(double[] data)
    {
        VerifySerialization(data);
    }

    private static void VerifySerialization(Array data)
    {
        var reflectionBasedResult = System.Text.Json.JsonSerializer.Serialize(data);
        var rawResult = TagTransformerJsonHelper.JsonSerializeArrayTag(data);

        Assert.Equal(reflectionBasedResult, rawResult);
    }
}

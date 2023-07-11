// <copyright file="TagTransformerJsonHelper.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System.Text;
using System.Text.Json;

namespace OpenTelemetry.Internal;

internal static class TagTransformerJsonHelper
{
    internal static string JsonSerializeArrayTag(Array array)
        => array switch
        {
            // This switch needs to support the same set of types as those in TagTransformer.TransformArrayTagInternal
            char[] charArray => WriteToString(charArray, WriteCharValue),
            string[] stringArray => WriteToString(stringArray, WriteStringValue),
            bool[] booleanArray => WriteToString(booleanArray, WriteBooleanValue),

            // Runtime allows casting byte[] to sbyte[] and vice versa, so pattern match to sbyte[] doesn't work since it goes through byte[]
            // Similarly (array is sbyte[]) doesn't help either. Only real type comparison works.
            // This is true for byte/sbyte, short/ushort, int/uint and so on
            byte[] byteArray => array.GetType() == typeof(sbyte[]) ? WriteToString((sbyte[])array, WriteSByteValue) : WriteToString(byteArray, WriteByteValue),
            short[] shortArray => array.GetType() == typeof(ushort[]) ? WriteToString((ushort[])array, WriteUShortValue) : WriteToString(shortArray, WriteShortValue),
            int[] intArray => array.GetType() == typeof(uint[]) ? WriteToString((uint[])array, WriteUIntValue) : WriteToString(intArray, WriteIntValue),

            long[] longArray => WriteToString(longArray, WriteLongValue),
            float[] floatArray => WriteToString(floatArray, WriteFloatValue),
            double[] doubleArray => WriteToString(doubleArray, WriteDoubleValue),

            _ => throw new NotSupportedException($"Unexpected serialization of array of type {array.GetType()}."),
        };

    private static void WriteCharValue(char v, Utf8JsonWriter writer)
    {
        Span<char> charSpan = stackalloc char[1];
        charSpan[0] = v;
        writer.WriteStringValue(charSpan);
    }

    private static void WriteStringValue(string v, Utf8JsonWriter writer) => writer.WriteStringValue(v);

    private static void WriteBooleanValue(bool v, Utf8JsonWriter writer) => writer.WriteBooleanValue(v);

    private static void WriteByteValue(byte v, Utf8JsonWriter writer) => writer.WriteNumberValue(v);

    private static void WriteSByteValue(sbyte v, Utf8JsonWriter writer) => writer.WriteNumberValue(v);

    private static void WriteShortValue(short v, Utf8JsonWriter writer) => writer.WriteNumberValue(v);

    private static void WriteUShortValue(ushort v, Utf8JsonWriter writer) => writer.WriteNumberValue(v);

    private static void WriteIntValue(int v, Utf8JsonWriter writer) => writer.WriteNumberValue(v);

    private static void WriteUIntValue(uint v, Utf8JsonWriter writer) => writer.WriteNumberValue(v);

    private static void WriteLongValue(long v, Utf8JsonWriter writer) => writer.WriteNumberValue(v);

    private static void WriteFloatValue(float v, Utf8JsonWriter writer) => writer.WriteNumberValue(v);

    private static void WriteDoubleValue(double v, Utf8JsonWriter writer) => writer.WriteNumberValue(v);

    private static string WriteToString<T>(T[] data, Action<T, Utf8JsonWriter> writeAction)
    {
        MemoryStream ms = new MemoryStream();
        using (Utf8JsonWriter writer = new Utf8JsonWriter(ms))
        {
            writer.WriteStartArray();
            foreach (T item in data)
            {
                writeAction(item, writer);
            }

            writer.WriteEndArray();
        }

        return Encoding.UTF8.GetString(ms.ToArray());
    }
}

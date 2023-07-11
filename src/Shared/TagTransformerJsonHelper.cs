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

// The class has to be partial so that JSON source generator can provide code for the JsonSerializerContext
#pragma warning disable SA1601 // Partial elements should be documented
internal static partial class TagTransformerJsonHelper
{
    internal static string JsonSerializeArrayTag(Array array)
        => array switch
        {
            // This switch needs to support the same set of types as those in TagTransformer.TransformArrayTagInternal
            char[] _ => WriteToString(array, WriteCharArray),
            string[] _ => WriteToString(array, WriteStringArray),
            bool[] _ => WriteToString(array, WriteBooleanArray),

            // Runtime allows casting byte[] to sbyte[] and vice versa, so pattern match to sbyte[] doesn't work since it goes through byte[]
            // Similarly (array is sbyte[]) doesn't help either. Only real type comparison works.
            // This is true for byte/sbyte, short/ushort, int/uint and so on
            byte[] _ => array.GetType() == typeof(sbyte[]) ? WriteToString(array, WriteSByteArray) : WriteToString(array, WriteByteArray),
            short[] _ => array.GetType() == typeof(ushort[]) ? WriteToString(array, WriteUShortArray) : WriteToString(array, WriteShortArray),
            int[] _ => array.GetType() == typeof(uint[]) ? WriteToString(array, WriteUIntArray) : WriteToString(array, WriteIntArray),

            long[] _ => WriteToString(array, WriteLongArray),
            float[] _ => WriteToString(array, WriteFloatArray),
            double[] _ => WriteToString(array, WriteDoubleArray),

            _ => throw new NotSupportedException($"Unexpected serialization of array of type {array.GetType()}."),
        };

    private static void WriteCharArray(object data, Utf8JsonWriter writer)
    {
        writer.WriteStartArray();
        Span<char> charSpan = stackalloc char[1];
        foreach (var item in (char[])data)
        {
            charSpan[0] = item;
            writer.WriteStringValue(charSpan);
        }

        writer.WriteEndArray();
    }

    private static void WriteStringArray(object data, Utf8JsonWriter writer)
    {
        writer.WriteStartArray();
        foreach (var item in (string[])data)
        {
            writer.WriteStringValue(item);
        }

        writer.WriteEndArray();
    }

    private static void WriteBooleanArray(object data, Utf8JsonWriter writer)
    {
        writer.WriteStartArray();
        foreach (var item in (bool[])data)
        {
            writer.WriteBooleanValue(item);
        }

        writer.WriteEndArray();
    }

    private static void WriteByteArray(object data, Utf8JsonWriter writer)
    {
        writer.WriteStartArray();
        foreach (var item in (byte[])data)
        {
            writer.WriteNumberValue(item);
        }

        writer.WriteEndArray();
    }

    private static void WriteSByteArray(object data, Utf8JsonWriter writer)
    {
        writer.WriteStartArray();
        foreach (var item in (sbyte[])data)
        {
            writer.WriteNumberValue(item);
        }

        writer.WriteEndArray();
    }

    private static void WriteShortArray(object data, Utf8JsonWriter writer)
    {
        writer.WriteStartArray();
        foreach (var item in (short[])data)
        {
            writer.WriteNumberValue(item);
        }

        writer.WriteEndArray();
    }

    private static void WriteUShortArray(object data, Utf8JsonWriter writer)
    {
        writer.WriteStartArray();
        foreach (var item in (ushort[])data)
        {
            writer.WriteNumberValue(item);
        }

        writer.WriteEndArray();
    }

    private static void WriteIntArray(object data, Utf8JsonWriter writer)
    {
        writer.WriteStartArray();
        foreach (var item in (int[])data)
        {
            writer.WriteNumberValue(item);
        }

        writer.WriteEndArray();
    }

    private static void WriteUIntArray(object data, Utf8JsonWriter writer)
    {
        writer.WriteStartArray();
        foreach (var item in (uint[])data)
        {
            writer.WriteNumberValue(item);
        }

        writer.WriteEndArray();
    }

    private static void WriteLongArray(object data, Utf8JsonWriter writer)
    {
        writer.WriteStartArray();
        foreach (var item in (long[])data)
        {
            writer.WriteNumberValue(item);
        }

        writer.WriteEndArray();
    }

    private static void WriteFloatArray(object data, Utf8JsonWriter writer)
    {
        writer.WriteStartArray();
        foreach (var item in (float[])data)
        {
            writer.WriteNumberValue(item);
        }

        writer.WriteEndArray();
    }

    private static void WriteDoubleArray(object data, Utf8JsonWriter writer)
    {
        writer.WriteStartArray();
        foreach (var item in (double[])data)
        {
            writer.WriteNumberValue(item);
        }

        writer.WriteEndArray();
    }

    private static string WriteToString(object data, Action<object, Utf8JsonWriter> writeAction)
    {
        MemoryStream ms = new MemoryStream();
        using (Utf8JsonWriter writer = new Utf8JsonWriter(ms))
        {
            writeAction(data, writer);
        }

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    //[JsonSerializable(typeof(object))]
    //[JsonSerializable(typeof(char[]))]
    //[JsonSerializable(typeof(string[]))]
    //[JsonSerializable(typeof(bool[]))]
    //[JsonSerializable(typeof(byte[]))]
    //[JsonSerializable(typeof(sbyte[]))]
    //[JsonSerializable(typeof(short[]))]
    //[JsonSerializable(typeof(ushort[]))]
    //[JsonSerializable(typeof(int[]))]
    //[JsonSerializable(typeof(uint[]))]
    //[JsonSerializable(typeof(long[]))]
    //[JsonSerializable(typeof(float[]))]
    //[JsonSerializable(typeof(double[]))]
    //private sealed partial class ArrayTagJsonContext : JsonSerializerContext
    //{
    //}
}

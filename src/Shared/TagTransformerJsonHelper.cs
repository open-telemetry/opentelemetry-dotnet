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

using System.Buffers;
using System.Text;
using System.Text.Json;

namespace OpenTelemetry.Internal;

#nullable enable

internal static class TagTransformerJsonHelper
{
    internal static string JsonSerializeArrayTag(Array array)
        => array switch
        {
            // This switch needs to support the same set of types as those in TagTransformer.TransformArrayTagInternal
            char[] charArray => WriteToString(charArray, WriteCharValue, 1),
            string[] stringArray => WriteToString(stringArray, WriteStringValue, 10),
            bool[] booleanArray => WriteToString(booleanArray, WriteBooleanValue, 5),

            // Runtime allows casting byte[] to sbyte[] and vice versa, so pattern match to sbyte[] doesn't work since it goes through byte[]
            // Similarly (array is sbyte[]) doesn't help either. Only real type comparison works.
            // This is true for byte/sbyte, short/ushort, int/uint and so on
            byte[] byteArray => array.GetType() == typeof(sbyte[]) ? WriteToString((sbyte[])array, WriteSByteValue, 3) : WriteToString(byteArray, WriteByteValue, 3),
            short[] shortArray => array.GetType() == typeof(ushort[]) ? WriteToString((ushort[])array, WriteUShortValue, 5) : WriteToString(shortArray, WriteShortValue, 5),
            int[] intArray => array.GetType() == typeof(uint[]) ? WriteToString((uint[])array, WriteUIntValue, 10) : WriteToString(intArray, WriteIntValue, 10),
            long[] longArray => array.GetType() == typeof(ulong[]) ? WriteToString((ulong[])array, WriteULongValue, 20) : WriteToString(longArray, WriteLongValue, 20),

            float[] floatArray => WriteToString(floatArray, WriteFloatValue, 15),
            double[] doubleArray => WriteToString(doubleArray, WriteDoubleValue, 23),

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

    private static void WriteULongValue(ulong v, Utf8JsonWriter writer) => writer.WriteNumberValue(v);

    private static void WriteFloatValue(float v, Utf8JsonWriter writer) => writer.WriteNumberValue(v);

    private static void WriteDoubleValue(double v, Utf8JsonWriter writer) => writer.WriteNumberValue(v);

    private static string WriteToString<T>(T[] data, Action<T, Utf8JsonWriter> writeAction, int elementSizeHint)
    {
        // Some rough estimate of the necessary size. We multiple the length of the array (+1 to be on the safe side)
        // by the estimated size of an element and add 2 for the brackets.
        using PooledByteBufferWriter bufferWriter = new PooledByteBufferWriter(((data.Length + 1) * (elementSizeHint + 1)) + 2);
        using (Utf8JsonWriter writer = new Utf8JsonWriter(bufferWriter))
        {
            writer.WriteStartArray();
            foreach (T item in data)
            {
                writeAction(item, writer);
            }

            writer.WriteEndArray();
        }

        var m = bufferWriter.WrittenMemory;
        return Encoding.UTF8.GetString(m.Buffer, 0, m.Length);
    }

    internal sealed class PooledByteBufferWriter : IBufferWriter<byte>, IDisposable
    {
        private const int MinimumBufferSize = 256;

        // Value copied from Array.MaxLength in System.Private.CoreLib/src/libraries/System.Private.CoreLib/src/System/Array.cs.
        private const int MaximumBufferSize = 0X7FFFFFC7;

        // This class allows two possible configurations: if rentedBuffer is not null then
        // it can be used as an IBufferWriter and holds a buffer that should eventually be
        // returned to the shared pool. If rentedBuffer is null, then the instance is in a
        // cleared/disposed state and it must re-rent a buffer before it can be used again.
        private byte[]? rentedBuffer;
        private int index;

        public PooledByteBufferWriter(int initialCapacity)
        {
            this.rentedBuffer = ArrayPool<byte>.Shared.Rent(initialCapacity);
            this.index = 0;
        }

        public (byte[] Buffer, int Length) WrittenMemory => (this.rentedBuffer!, this.index);

        // Returns the rented buffer back to the pool
        public void Dispose()
        {
            if (this.rentedBuffer == null)
            {
                return;
            }

            this.rentedBuffer.AsSpan(0, this.index).Clear();
            this.index = 0;
            byte[] toReturn = this.rentedBuffer;
            this.rentedBuffer = null;
            ArrayPool<byte>.Shared.Return(toReturn);
        }

        public void Advance(int count)
        {
            this.index += count;
        }

        public Memory<byte> GetMemory(int sizeHint = MinimumBufferSize)
        {
            this.CheckAndResizeBuffer(sizeHint);
            return this.rentedBuffer.AsMemory(this.index);
        }

        public Span<byte> GetSpan(int sizeHint = MinimumBufferSize)
        {
            this.CheckAndResizeBuffer(sizeHint);
            return this.rentedBuffer.AsSpan(this.index);
        }

        private void CheckAndResizeBuffer(int sizeHint)
        {
            int currentLength = this.rentedBuffer!.Length;
            int availableSpace = currentLength - this.index;

            // If we've reached ~1GB written, grow to the maximum buffer
            // length to avoid incessant minimal growths causing perf issues.
            if (this.index >= MaximumBufferSize / 2)
            {
                sizeHint = Math.Max(sizeHint, MaximumBufferSize - currentLength);
            }

            if (sizeHint > availableSpace)
            {
                int growBy = Math.Max(sizeHint, currentLength);

                int newSize = currentLength + growBy;

                if ((uint)newSize > MaximumBufferSize)
                {
                    newSize = currentLength + sizeHint;
                    if ((uint)newSize > MaximumBufferSize)
                    {
                        throw new OutOfMemoryException();
                    }
                }

                byte[] oldBuffer = this.rentedBuffer;

                this.rentedBuffer = ArrayPool<byte>.Shared.Rent(newSize);

                Span<byte> oldBufferAsSpan = oldBuffer.AsSpan(0, this.index);
                oldBufferAsSpan.CopyTo(this.rentedBuffer);
                oldBufferAsSpan.Clear();
                ArrayPool<byte>.Shared.Return(oldBuffer);
            }
        }
    }
}

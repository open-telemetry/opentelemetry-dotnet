// <copyright file="VarInt.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
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

namespace OpenTelemetry.Internal
{
    using System;
    using System.IO;

    public static class VarInt
    {
        /// <summary>
        /// Maximum encoded size of 32-bit positive integers (in bytes).
        /// </summary>
        public const int MaxVarintSize = 5;

        /// <summary>
        /// maximum encoded size of 64-bit longs, and negative 32-bit ints (in bytes).
        /// </summary>
        public const int MaxVarlongSize = 10;

        public static int VarIntSize(int i)
        {
            var result = 0;
            var ui = (uint)i;
            do
            {
                result++;
                ui = ui >> 7;
            }
            while (ui != 0);
            return result;
        }

        public static int GetVarInt(byte[] src, int offset, int[] dst)
        {
            var result = 0;
            var shift = 0;
            int b;
            do
            {
                if (shift >= 32)
                {
                    // Out of range
                    throw new ArgumentOutOfRangeException(nameof(src), "varint too long.");
                }

                // Get 7 bits from next byte
                b = src[offset++];
                result |= (b & 0x7F) << shift;
                shift += 7;
            }
            while ((b & 0x80) != 0);
            dst[0] = result;
            return offset;
        }

        /// <summary>
        /// Writes an into into an array at a specific offset.
        /// </summary>
        /// <param name="v">The value to write.</param>
        /// <param name="sink">The array to write to.</param>
        /// <param name="offset">The offset at which to place the value.</param>
        /// <returns>The offset.</returns>
        public static int PutVarInt(int v, byte[] sink, int offset)
        {
            var uv = (uint)v;
            do
            {
                // Encode next 7 bits + terminator bit
                var bits = uv & 0x7F;
                uv >>= 7;
                var b = (byte)(bits + ((uv != 0) ? 0x80 : 0));
                sink[offset++] = b;
            }
            while (uv != 0);
            return offset;
        }

        /// <summary>
        /// Gets an integer from a stream.
        /// </summary>
        /// <param name="inputStream">The stream to read from.</param>
        /// <returns>The int.</returns>
        public static int GetVarInt(Stream inputStream)
        {
            var result = 0;
            var shift = 0;
            int b;
            do
            {
                if (shift >= 32)
                {
                    // Out of range
                    throw new ArgumentOutOfRangeException(nameof(inputStream), "varint too long.");
                }

                // Get 7 bits from next byte
                b = inputStream.ReadByte();
                result |= (b & 0x7F) << shift;
                shift += 7;
            }
            while ((b & 0x80) != 0);
            return result;
        }

        /// <summary>
        /// Writes an integer to a stream.
        /// </summary>
        /// <param name="v">The value.</param>
        /// <param name="outputStream">The stream to write to.</param>
        public static void PutVarInt(int v, Stream outputStream)
        {
            var bytes = new byte[VarIntSize(v)];
            PutVarInt(v, bytes, 0);
            outputStream.Write(bytes, 0, bytes.Length);
        }
    }
}

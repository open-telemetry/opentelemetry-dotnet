// <copyright file="VarInt.cs" company="OpenCensus Authors">
// Copyright 2018, OpenCensus Authors
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

namespace OpenCensus.Internal
{
    using System;
    using System.IO;

    public static class VarInt
    {
        /** Maximum encoded size of 32-bit positive integers (in bytes) */
        public const int MaxVarintSize = 5;

        /** maximum encoded size of 64-bit longs, and negative 32-bit ints (in bytes) */
        public const int MaxVarlongSize = 10;

        public static int VarIntSize(int i)
        {
            int result = 0;
            uint ui = (uint)i;
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
            int result = 0;
            int shift = 0;
            int b;
            do
            {
                if (shift >= 32)
                {
                    // Out of range
                    throw new ArgumentOutOfRangeException("varint too long");
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

        public static int PutVarInt(int v, byte[] sink, int offset)
        {
            uint uv = (uint)v;
            do
            {
                // Encode next 7 bits + terminator bit
                uint bits = uv & 0x7F;
                uv >>= 7;
                byte b = (byte)(bits + ((uv != 0) ? 0x80 : 0));
                sink[offset++] = b;
            }
            while (uv != 0);
            return offset;
        }

        // public static int getVarInt(ByteBuffer src)
        // {
        //    int tmp;
        //    if ((tmp = src.get()) >= 0)
        //    {
        //        return tmp;
        //    }
        //    int result = tmp & 0x7f;
        //    if ((tmp = src.get()) >= 0)
        //    {
        //        result |= tmp << 7;
        //    }
        //    else
        //    {
        //        result |= (tmp & 0x7f) << 7;
        //        if ((tmp = src.get()) >= 0)
        //        {
        //            result |= tmp << 14;
        //        }
        //        else
        //        {
        //            result |= (tmp & 0x7f) << 14;
        //            if ((tmp = src.get()) >= 0)
        //            {
        //                result |= tmp << 21;
        //            }
        //            else
        //            {
        //                result |= (tmp & 0x7f) << 21;
        //                result |= (tmp = src.get()) << 28;
        //                while (tmp < 0)
        //                {
        //                    // We get into this loop only in the case of overflow.
        //                    // By doing this, we can call getVarInt() instead of
        //                    // getVarLong() when we only need an int.
        //                    tmp = src.get();
        //                }
        //            }
        //        }
        //    }
        //    return result;
        // }

        // public static void putVarInt(int v, ByteBuffer sink)
        // {
        //    while (true)
        //    {
        //        int bits = v & 0x7f;
        //        v >>>= 7;
        //        if (v == 0)
        //        {
        //            sink.put((byte)bits);
        //            return;
        //        }
        //        sink.put((byte)(bits | 0x80));
        //    }
        // }

        /**
         * Reads a varint from the given InputStream and returns the decoded value as an int.
         *
         * @param inputStream the InputStream to read from
         */
        public static int GetVarInt(Stream inputStream)
        {
            int result = 0;
            int shift = 0;
            int b;
            do
            {
                if (shift >= 32)
                {
                    // Out of range
                    throw new ArgumentOutOfRangeException("varint too long");
                }

                // Get 7 bits from next byte
                b = inputStream.ReadByte();
                result |= (b & 0x7F) << shift;
                shift += 7;
            }
            while ((b & 0x80) != 0);
            return result;
        }

        public static void PutVarInt(int v, Stream outputStream)
        {
            byte[] bytes = new byte[VarIntSize(v)];
            PutVarInt(v, bytes, 0);
            outputStream.Write(bytes, 0, bytes.Length);
        }

        public static int VarLongSize(long v)
        {
            int result = 0;
            ulong uv = (ulong)v;
            do
            {
                result++;
                uv >>= 7;
            }
            while (uv != 0);
            return result;
        }

        // public static long GetVarLong(ByteBuffer src)
        // {
        //    long tmp;
        //    if ((tmp = src.get()) >= 0)
        //    {
        //        return tmp;
        //    }
        //    long result = tmp & 0x7f;
        //    if ((tmp = src.get()) >= 0)
        //    {
        //        result |= tmp << 7;
        //    }
        //    else
        //    {
        //        result |= (tmp & 0x7f) << 7;
        //        if ((tmp = src.get()) >= 0)
        //        {
        //            result |= tmp << 14;
        //        }
        //        else
        //        {
        //            result |= (tmp & 0x7f) << 14;
        //            if ((tmp = src.get()) >= 0)
        //            {
        //                result |= tmp << 21;
        //            }
        //            else
        //            {
        //                result |= (tmp & 0x7f) << 21;
        //                if ((tmp = src.get()) >= 0)
        //                {
        //                    result |= tmp << 28;
        //                }
        //                else
        //                {
        //                    result |= (tmp & 0x7f) << 28;
        //                    if ((tmp = src.get()) >= 0)
        //                    {
        //                        result |= tmp << 35;
        //                    }
        //                    else
        //                    {
        //                        result |= (tmp & 0x7f) << 35;
        //                        if ((tmp = src.get()) >= 0)
        //                        {
        //                            result |= tmp << 42;
        //                        }
        //                        else
        //                        {
        //                            result |= (tmp & 0x7f) << 42;
        //                            if ((tmp = src.get()) >= 0)
        //                            {
        //                                result |= tmp << 49;
        //                            }
        //                            else
        //                            {
        //                                result |= (tmp & 0x7f) << 49;
        //                                if ((tmp = src.get()) >= 0)
        //                                {
        //                                    result |= tmp << 56;
        //                                }
        //                                else
        //                                {
        //                                    result |= (tmp & 0x7f) << 56;
        //                                    result |= ((long)src.get()) << 63;
        //                                }
        //                            }
        //                        }
        //                    }
        //                }
        //            }
        //        }
        //    }
        //    return result;
        // }

        // public static void PutVarLong(long v, ByteBuffer sink)
        // {
        //    while (true)
        //    {
        //        int bits = ((int)v) & 0x7f;
        //        v >>>= 7;
        //        if (v == 0)
        //        {
        //            sink.put((byte)bits);
        //            return;
        //        }
        //        sink.put((byte)(bits | 0x80));
        //    }
        // }
    }
}

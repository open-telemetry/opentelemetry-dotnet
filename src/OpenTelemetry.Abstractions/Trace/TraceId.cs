// <copyright file="TraceId.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace
{
    using System;
    using OpenTelemetry.Utils;

    /// <summary>
    /// Trace ID.
    /// </summary>
    public sealed class TraceId : IComparable<TraceId>
    {
        public const int Size = 16;
        private static readonly TraceId InvalidTraceId = new TraceId(new byte[Size]);

        private readonly byte[] bytes;

        private TraceId(byte[] bytes)
        {
            this.bytes = bytes;
        }

        public static TraceId Invalid
        {
            get
            {
                return InvalidTraceId;
            }
        }

        /// <summary>
        /// Gets the bytes representation of a trace id.
        /// </summary>
        public byte[] Bytes
        {
            get
            {
                var copyOf = new byte[Size];
                Buffer.BlockCopy(this.bytes, 0, copyOf, 0, Size);
                return copyOf;
            }
        }

        /// <summary>
        /// Gets a value indicating whether trace if is valid.
        /// </summary>
        public bool IsValid
        {
            get
            {
                return !Arrays.Equals(this.bytes, InvalidTraceId.bytes);
            }
        }

        /// <summary>
        /// Gets the lower long of the trace ID.
        /// </summary>
        public long LowerLong
        {
            get
            {
                long result = 0;
                for (var i = 0; i < 8; i++)
                {
                    result <<= 8;
#pragma warning disable CS0675 // Bitwise-or operator used on a sign-extended operand
                    result |= this.bytes[i] & 0xff;
#pragma warning restore CS0675 // Bitwise-or operator used on a sign-extended operand
                }

                if (result < 0)
                {
                    return -result;
                }

                return result;
            }
        }

        public static TraceId FromBytes(byte[] buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException("buffer");
            }

            if (buffer.Length != Size)
            {
                throw new ArgumentOutOfRangeException(string.Format("Invalid size: expected {0}, got {1}", Size, buffer.Length));
            }

            var bytesCopied = new byte[Size];
            Buffer.BlockCopy(buffer, 0, bytesCopied, 0, Size);
            return new TraceId(bytesCopied);
        }

        public static TraceId FromBytes(byte[] src, int srcOffset)
        {
            var bytes = new byte[Size];
            Buffer.BlockCopy(src, srcOffset, bytes, 0, Size);
            return new TraceId(bytes);
        }

        public static TraceId FromLowerBase16(string src)
        {
            if (src.Length != 2 * Size)
            {
                throw new ArgumentOutOfRangeException(string.Format("Invalid size: expected {0}, got {1}", 2 * Size, src.Length));
            }

            var bytes = Arrays.StringToByteArray(src);
            return new TraceId(bytes);
        }

        public static TraceId GenerateRandomId(IRandomGenerator random)
        {
            var bytes = new byte[Size];
            do
            {
                random.NextBytes(bytes);
            }
            while (Arrays.Equals(bytes, InvalidTraceId.bytes));
            return new TraceId(bytes);
        }

        /// <summary>
        /// Copy trace ID as bytes into the destination bytes array at a given offset.
        /// </summary>
        /// <param name="dest">Destination bytes array.</param>
        /// <param name="destOffset">Desitnation bytes array offset.</param>
        public void CopyBytesTo(byte[] dest, int destOffset)
        {
            Buffer.BlockCopy(this.bytes, 0, dest, destOffset, Size);
        }

        /// <summary>
        /// Gets the lower base 16 representaiton of the trace id.
        /// </summary>
        /// <returns>Canonical string representation of a trace id.</returns>
        public string ToLowerBase16()
        {
            var bytes = this.Bytes;
            var result = Arrays.ByteArrayToString(bytes);
            return result;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (obj == this)
            {
                return true;
            }

            if (!(obj is TraceId))
            {
                return false;
            }

            var that = (TraceId)obj;
            return Arrays.Equals(this.bytes, that.bytes);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return Arrays.GetHashCode(this.bytes);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return "TraceId{"
               + "bytes=" + this.ToLowerBase16()
               + "}";
        }

        public int CompareTo(TraceId other)
        {
            var that = other as TraceId;
            for (var i = 0; i < Size; i++)
            {
                if (this.bytes[i] != that.bytes[i])
                {
                    var b1 = (sbyte)this.bytes[i];
                    var b2 = (sbyte)that.bytes[i];

                    return b1 < b2 ? -1 : 1;
                }
            }

            return 0;
        }
    }
}

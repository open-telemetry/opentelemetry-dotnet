// <copyright file="SpanId.cs" company="OpenTelemetry Authors">
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
    /// Span identifier.
    /// </summary>
    public sealed class SpanId : IComparable<SpanId>
    {
        public const int Size = 8;

        private static readonly SpanId InvalidSpanId = new SpanId(new byte[Size]);

        private readonly byte[] bytes;

        private SpanId(byte[] bytes)
        {
            this.bytes = bytes;
        }

        public static SpanId Invalid
        {
            get
            {
                return InvalidSpanId;
            }
        }

        /// <summary>
        /// Gets the span identifier as bytes.
        /// </summary>
        public byte[] Bytes
        {
            get
            {
                byte[] copyOf = new byte[Size];
                Buffer.BlockCopy(this.bytes, 0, copyOf, 0, Size);
                return copyOf;
            }
        }

        /// <summary>
        /// Gets a value indicating whether span identifier is valid.
        /// </summary>
        public bool IsValid
        {
            get { return !Arrays.Equals(this.bytes, InvalidSpanId.bytes); }
        }

        public static SpanId FromBytes(byte[] buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException("buffer");
            }

            if (buffer.Length != Size)
            {
                throw new ArgumentOutOfRangeException(string.Format("Invalid size: expected {0}, got {1}", Size, buffer.Length));
            }

            byte[] bytesCopied = new byte[Size];
            Buffer.BlockCopy(buffer, 0, bytesCopied, 0, Size);
            return new SpanId(bytesCopied);
        }

        public static SpanId FromBytes(byte[] src, int srcOffset)
        {
            byte[] bytes = new byte[Size];
            Buffer.BlockCopy(src, srcOffset, bytes, 0, Size);
            return new SpanId(bytes);
        }

        public static SpanId FromLowerBase16(string src)
        {
            if (src.Length != 2 * Size)
            {
                throw new ArgumentOutOfRangeException(string.Format("Invalid size: expected {0}, got {1}", 2 * Size, src.Length));
            }

            byte[] bytes = Arrays.StringToByteArray(src);
            return new SpanId(bytes);
        }

        public static SpanId GenerateRandomId(IRandomGenerator random)
        {
            byte[] bytes = new byte[Size];
            do
            {
                random.NextBytes(bytes);
            }
            while (Arrays.Equals(bytes, InvalidSpanId.bytes));
            return new SpanId(bytes);
        }

        /// <summary>
        /// Copy span id as bytes into destination byte array.
        /// </summary>
        /// <param name="dest">Destination byte array.</param>
        /// <param name="destOffset">Offset to start writing from.</param>
        public void CopyBytesTo(byte[] dest, int destOffset)
        {
            Buffer.BlockCopy(this.bytes, 0, dest, destOffset, Size);
        }

        /// <summary>
        /// Gets the span identifier as a string.
        /// </summary>
        /// <returns>String representation of Span identifier.</returns>
        public string ToLowerBase16()
        {
            var bytes = this.Bytes;
            return Arrays.ByteArrayToString(bytes);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (obj == this)
            {
                return true;
            }

            if (!(obj is SpanId))
            {
                return false;
            }

            SpanId that = (SpanId)obj;
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
            return "SpanId{"
                + "bytes=" + this.ToLowerBase16()
                + "}";
        }

        /// <inheritdoc />
        public int CompareTo(SpanId other)
        {
            for (int i = 0; i < Size; i++)
            {
                if (this.bytes[i] != other.bytes[i])
                {
                    sbyte b1 = (sbyte)this.bytes[i];
                    sbyte b2 = (sbyte)other.bytes[i];

                    return b1 < b2 ? -1 : 1;
                }
            }

            return 0;
        }
    }
}

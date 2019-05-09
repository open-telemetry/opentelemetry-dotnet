// <copyright file="TraceOptions.cs" company="OpenCensus Authors">
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

namespace OpenCensus.Trace
{
    using System;

    /// <summary>
    /// Trace options.
    /// </summary>
    public sealed class TraceOptions
    {
        /// <summary>
        /// Size of trace options flag.
        /// </summary>
        public const int Size = 1;

        /// <summary>
        /// Default trace options. Nothing set.
        /// </summary>
        public static readonly TraceOptions Default = new TraceOptions(DefaultOptions);

        /// <summary>
        /// Sampled trace options.
        /// </summary>
        public static readonly TraceOptions Sampled = new TraceOptions(1);

        internal const byte DefaultOptions = 0;

        internal const byte IsSampledBit = 0x1;

        private byte options;

        internal TraceOptions(byte options)
        {
            this.options = options;
        }

        /// <summary>
        /// Gets the bytes representation of a trace options.
        /// </summary>
        public byte[] Bytes
        {
            get
            {
                byte[] bytes = new byte[Size];
                bytes[0] = this.options;
                return bytes;
            }
        }

        /// <summary>
        /// Gets a value indicating whether span is sampled or not.
        /// </summary>
        public bool IsSampled
        {
            get
            {
                return this.HasOption(IsSampledBit);
            }
        }

        internal sbyte Options
        {
            get { return (sbyte)this.options; }
        }

        /// <summary>
        /// Deserializes trace options from bytes.
        /// </summary>
        /// <param name="buffer">Buffer to deserialize.</param>
        /// <returns>Trace options deserialized from the bytes array.</returns>
        public static TraceOptions FromBytes(byte[] buffer)
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
            return new TraceOptions(bytesCopied[0]);
        }

        /// <summary>
        /// Trace options from bytes with the given offset.
        /// </summary>
        /// <param name="src">Buffer to sdeserialize trace optiosn from.</param>
        /// <param name="srcOffset">Buffer offset.</param>
        /// <returns>Trace options deserialized from the buffer.</returns>
        public static TraceOptions FromBytes(byte[] src, int srcOffset)
        {
            if (srcOffset < 0 || srcOffset >= src.Length)
            {
                throw new IndexOutOfRangeException("srcOffset");
            }

            return new TraceOptions(src[srcOffset]);
        }

        /// <summary>
        /// Gets the trace options builder.
        /// </summary>
        /// <returns>Trace options builder.</returns>
        public static TraceOptionsBuilder Builder()
        {
            return new TraceOptionsBuilder(DefaultOptions);
        }

        /// <summary>
        /// Trace options builder pre-initialized from the given trace options instance.
        /// </summary>
        /// <param name="traceOptions">Trace options to pre-initialize the builder.</param>
        /// <returns>Trace options builder.</returns>
        public static TraceOptionsBuilder Builder(TraceOptions traceOptions)
        {
            return new TraceOptionsBuilder(traceOptions.options);
        }

        /// <summary>
        /// Serializes trace options into bytes array at a given offset.
        /// </summary>
        /// <param name="dest">Destination to serialize value to.</param>
        /// <param name="destOffset">Destintion offset.</param>
        public void CopyBytesTo(byte[] dest, int destOffset)
        {
            if (destOffset < 0 || destOffset >= dest.Length)
            {
                throw new IndexOutOfRangeException("destOffset");
            }

            dest[destOffset] = this.options;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (obj == this)
            {
                return true;
            }

            if (!(obj is TraceOptions))
            {
                return false;
            }

            TraceOptions that = (TraceOptions)obj;
            return this.options == that.options;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            int result = (31 * 1) + this.options;
            return result;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return "TraceOptions{"
                + "sampled=" + this.IsSampled
                + "}";
        }

        private bool HasOption(int mask)
        {
            return (this.options & mask) != 0;
        }

        private void ClearOption(int mask)
        {
            this.options = (byte)(this.options & ~mask);
        }

        private void SetOption(int mask)
        {
            this.options = (byte)(this.options | mask);
        }
    }
}

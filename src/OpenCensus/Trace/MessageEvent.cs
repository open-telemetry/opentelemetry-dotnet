// <copyright file="MessageEvent.cs" company="OpenCensus Authors">
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
    public class MessageEvent : IMessageEvent
    {
        internal MessageEvent(MessageEventType type, long messageId, long uncompressedMessageSize, long compressedMessageSize)
        {
            this.Type = type;
            this.MessageId = messageId;
            this.UncompressedMessageSize = uncompressedMessageSize;
            this.CompressedMessageSize = compressedMessageSize;
        }

        public MessageEventType Type { get; }

        public long MessageId { get; }

        public long UncompressedMessageSize { get; }

        public long CompressedMessageSize { get; }

        public static MessageEventBuilder Builder(MessageEventType type, long messageId)
        {
            return new MessageEventBuilder()
                    .SetType(type)
                    .SetMessageId(messageId)

                    // We need to set a value for the message size because the autovalue requires all
                    // primitives to be initialized.
                    .SetUncompressedMessageSize(0)
                    .SetCompressedMessageSize(0);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return "MessageEvent{"
                + "type=" + this.Type + ", "
                + "messageId=" + this.MessageId + ", "
                + "uncompressedMessageSize=" + this.UncompressedMessageSize + ", "
                + "compressedMessageSize=" + this.CompressedMessageSize
                + "}";
        }

    /// <inheritdoc/>
        public override bool Equals(object o)
        {
            if (o == this)
            {
                return true;
            }

            if (o is MessageEvent that)
            {
                return this.Type.Equals(that.Type)
                     && (this.MessageId == that.MessageId)
                     && (this.UncompressedMessageSize == that.UncompressedMessageSize)
                     && (this.CompressedMessageSize == that.CompressedMessageSize);
            }

            return false;
        }

    /// <inheritdoc/>
        public override int GetHashCode()
        {
            long h = 1;
            h *= 1000003;
            h ^= this.Type.GetHashCode();
            h *= 1000003;
            h ^= (this.MessageId >> 32) ^ this.MessageId;
            h *= 1000003;
            h ^= (this.UncompressedMessageSize >> 32) ^ this.UncompressedMessageSize;
            h *= 1000003;
            h ^= (this.CompressedMessageSize >> 32) ^ this.CompressedMessageSize;
            return (int)h;
        }
    }
}

// <copyright file="MessageEventBuilder.cs" company="OpenCensus Authors">
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

    public class MessageEventBuilder
    {
        private MessageEventType? type;
        private long? messageId;
        private long? uncompressedMessageSize;
        private long? compressedMessageSize;

        internal MessageEventBuilder()
        {
        }

        internal MessageEventBuilder(
            MessageEventType type,
            long messageId,
            long uncompressedMessageSize,
            long compressedMessageSize)
        {
            this.type = type;
            this.messageId = messageId;
            this.uncompressedMessageSize = uncompressedMessageSize;
            this.compressedMessageSize = compressedMessageSize;
        }

        public MessageEventBuilder SetUncompressedMessageSize(long uncompressedMessageSize)
        {
            this.uncompressedMessageSize = uncompressedMessageSize;
            return this;
        }

        public MessageEventBuilder SetCompressedMessageSize(long compressedMessageSize)
        {
            this.compressedMessageSize = compressedMessageSize;
            return this;
        }

        public IMessageEvent Build()
        {
            string missing = string.Empty;
            if (!this.type.HasValue)
            {
                missing += " type";
            }

            if (!this.messageId.HasValue)
            {
                missing += " messageId";
            }

            if (!this.uncompressedMessageSize.HasValue)
            {
                missing += " uncompressedMessageSize";
            }

            if (!this.compressedMessageSize.HasValue)
            {
                missing += " compressedMessageSize";
            }

            if (!string.IsNullOrEmpty(missing))
            {
                throw new ArgumentOutOfRangeException("Missing required properties:" + missing);
            }

            return new MessageEvent(
                this.type.Value,
                this.messageId.Value,
                this.uncompressedMessageSize.Value,
                this.compressedMessageSize.Value);
        }

        internal MessageEventBuilder SetType(MessageEventType type)
        {
            this.type = type;
            return this;
        }

        internal MessageEventBuilder SetMessageId(long messageId)
        {
            this.messageId = messageId;
            return this;
        }
    }
}

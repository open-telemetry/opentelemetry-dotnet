// <copyright file="MessageEventTest.cs" company="OpenCensus Authors">
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

namespace OpenCensus.Trace.Test
{
    using Xunit;

    public class MessageEventTest
    {

        [Fact]
        public void BuildMessageEvent_WithRequiredFields()
        {
            IMessageEvent messageEvent = MessageEvent.Builder(MessageEventType.Sent, 1L).Build();
            Assert.Equal(MessageEventType.Sent, messageEvent.Type);
            Assert.Equal(1L, messageEvent.MessageId);
            Assert.Equal(0L, messageEvent.UncompressedMessageSize);
        }

        [Fact]
        public void BuildMessageEvent_WithUncompressedMessageSize()
        {
            IMessageEvent messageEvent =
                MessageEvent.Builder(MessageEventType.Sent, 1L).SetUncompressedMessageSize(123L).Build();
            Assert.Equal(MessageEventType.Sent, messageEvent.Type);
            Assert.Equal(1L, messageEvent.MessageId);
            Assert.Equal(123L, messageEvent.UncompressedMessageSize);
        }

        [Fact]
        public void BuildMessageEvent_WithCompressedMessageSize()
        {
            IMessageEvent messageEvent =
                MessageEvent.Builder(MessageEventType.Sent, 1L).SetCompressedMessageSize(123L).Build();
            Assert.Equal(MessageEventType.Sent, messageEvent.Type);
            Assert.Equal(1L, messageEvent.MessageId);
            Assert.Equal(123L, messageEvent.CompressedMessageSize);
        }

        [Fact]
        public void BuildMessageEvent_WithAllValues()
        {
            IMessageEvent messageEvent =
                MessageEvent.Builder(MessageEventType.Received, 1L)
                    .SetUncompressedMessageSize(123L)
                    .SetCompressedMessageSize(63L)
                    .Build();
            Assert.Equal(MessageEventType.Received, messageEvent.Type);
            Assert.Equal(1L, messageEvent.MessageId);
            Assert.Equal(123L, messageEvent.UncompressedMessageSize);
            Assert.Equal(63L, messageEvent.CompressedMessageSize);
        }

        [Fact]
        public void MessageEvent_ToString()
        {
            IMessageEvent messageEvent =
                MessageEvent.Builder(MessageEventType.Sent, 1L)
                    .SetUncompressedMessageSize(123L)
                    .SetCompressedMessageSize(63L)
                    .Build();
            Assert.Contains("type=Sent", messageEvent.ToString());
            Assert.Contains("messageId=1", messageEvent.ToString());
            Assert.Contains("compressedMessageSize=63", messageEvent.ToString());
            Assert.Contains("uncompressedMessageSize=123", messageEvent.ToString());
        }
    }
}


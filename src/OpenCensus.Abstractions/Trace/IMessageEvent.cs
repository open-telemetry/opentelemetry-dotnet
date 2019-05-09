// <copyright file="IMessageEvent.cs" company="OpenCensus Authors">
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
    /// <summary>
    /// Message event happened during the span execution.
    /// </summary>
    public interface IMessageEvent
    {
        /// <summary>
        /// Gets ths type of the event.
        /// </summary>
        MessageEventType Type { get; }

        /// <summary>
        /// Gets the message identitifer associated with the event.
        /// </summary>
        long MessageId { get; }

        /// <summary>
        /// Gets the uncompressed message size.
        /// </summary>
        long UncompressedMessageSize { get; }

        /// <summary>
        /// Gets the compressed message size.
        /// </summary>
        long CompressedMessageSize { get; }
    }
}

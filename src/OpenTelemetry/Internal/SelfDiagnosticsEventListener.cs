// <copyright file="SelfDiagnosticsEventListener.cs" company="OpenTelemetry Authors">
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

using System;
using System.Collections.ObjectModel;
using System.Diagnostics.Tracing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace OpenTelemetry.Internal
{
    /// <summary>
    /// SelfDiagnosticsEventListener class enables the events from OpenTelemetry event sources
    /// and write the events to a local file in a circular way.
    /// </summary>
    internal class SelfDiagnosticsEventListener : EventListener
    {
        // Buffer size of the log line. A UTF-16 encoded character in C# can take up to 4 bytes if encoded in UTF-8.
        private const int BUFFERSIZE = 4 * 5120;
        private const string EventSourceNamePrefix = "OpenTelemetry-";
        private readonly EventLevel logLevel;
        private readonly SelfDiagnosticsConfigRefresher configRefresher;
        private readonly ThreadLocal<byte[]> writeBuffer = new ThreadLocal<byte[]>(() => null);

        public SelfDiagnosticsEventListener(EventLevel logLevel, SelfDiagnosticsConfigRefresher configRefresher)
        {
            this.logLevel = logLevel;
            this.configRefresher = configRefresher ?? throw new ArgumentNullException(nameof(configRefresher));
        }

        /// <summary>
        /// Encode a string into the designated position in a buffer of bytes, which will be written as log.
        /// If isParameter is true, wrap "{}" around the string.
        /// The buffer should not be filled to full, leaving at least one byte empty space to fill a '\n' later.
        /// If the buffer cannot hold all characters, truncate the string and replace extra content with "...".
        /// The buffer is not guaranteed to be filled until the last byte due to variable encoding length of UTF-8,
        /// in order to prioritize speed over space.
        /// </summary>
        /// <param name="str">The string to be encoded.</param>
        /// <param name="isParameter">Whether the string is a parameter. If true, "{}" will be wrapped around the string.</param>
        /// <param name="buffer">The byte array to contain the resulting sequence of bytes.</param>
        /// <param name="position">The position at which to start writing the resulting sequence of bytes.</param>
        /// <returns>The position of the buffer after the last byte of the resulting sequence.</returns>
        internal static int EncodeInBuffer(string str, bool isParameter, byte[] buffer, int position)
        {
            int charCount = str.Length;
            int ellipses = isParameter ? "{...}\n".Length : "...\n".Length;

            // Ensure there is space for "{...}\n" or "...\n".
            if (buffer.Length - position - ellipses < 0)
            {
                return position;
            }

            int estimateOfCharacters = (buffer.Length - position - ellipses) / 2;

            // Ensure the UTF-16 encoded string can fit in buffer UTF-8 encoding.
            // And leave space for "{...}\n" or "...\n".
            if (charCount > estimateOfCharacters)
            {
                charCount = estimateOfCharacters;
            }

            if (isParameter)
            {
                buffer[position++] = (byte)'{';
            }

            position += Encoding.UTF8.GetBytes(str, 0, charCount, buffer, position);
            if (charCount != str.Length)
            {
                buffer[position++] = (byte)'.';
                buffer[position++] = (byte)'.';
                buffer[position++] = (byte)'.';
            }

            if (isParameter)
            {
                buffer[position++] = (byte)'}';
            }

            return position;
        }

        internal void WriteEvent(string eventMessage, ReadOnlyCollection<object> payload)
        {
            try
            {
                var buffer = this.writeBuffer.Value;
                if (buffer == null)
                {
                    buffer = new byte[BUFFERSIZE];  // TODO: handle OOM
                    this.writeBuffer.Value = buffer;
                }

                var timestamp = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
                var pos = Encoding.UTF8.GetBytes(timestamp, 0, timestamp.Length, buffer, 0);
                buffer[pos++] = (byte)':';
                pos = EncodeInBuffer(eventMessage, false, buffer, pos);
                if (payload != null)
                {
                    // Not using foreach because it can cause allocations
                    for (int i = 0; i < payload.Count; ++i)
                    {
                        object obj = payload.ElementAt(i);
                        if (obj != null)
                        {
                            pos = EncodeInBuffer(obj.ToString(), true, buffer, pos);
                        }
                        else
                        {
                            pos = EncodeInBuffer("null", true, buffer, pos);
                        }
                    }
                }

                buffer[pos++] = (byte)'\n';
                int byteCount = pos - 0;
                if (this.configRefresher.TryGetLogStream(byteCount, out Stream stream, out int availableByteCount))
                {
                    if (availableByteCount >= byteCount)
                    {
                        stream.Write(buffer, 0, byteCount);
                    }
                    else
                    {
                        stream.Write(buffer, 0, availableByteCount);
                        stream.Seek(0, SeekOrigin.Begin);
                        stream.Write(buffer, availableByteCount, byteCount - availableByteCount);
                    }
                }
            }
            catch (Exception)
            {
                // One concurrent condition: memory mapped file is disposed in other thread after TryGetLogStream() finishes.
                // In this case, silently fail.
            }
        }

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name.StartsWith(EventSourceNamePrefix, StringComparison.Ordinal))
            {
#if NET452
                this.EnableEvents(eventSource, this.logLevel, (EventKeywords)(-1));
#else
                this.EnableEvents(eventSource, this.logLevel, EventKeywords.All);
#endif
            }

            base.OnEventSourceCreated(eventSource);
        }

        /// <summary>
        /// This method records the events from event sources to a local file, which is provided as a stream object by
        /// SelfDiagnosticsConfigRefresher class. The file size is bound to a upper limit. Once the write position
        /// reaches the end, it will be reset to the beginning of the file.
        /// </summary>
        /// <param name="eventData">Data of the EventSource event.</param>
        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            // TODO: retrieve the file stream object from configRefresher and write to it
            this.WriteEvent(eventData.Message, eventData.Payload);
        }
    }
}

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
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.Tracing;
using System.IO;
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
        private readonly object lockObj = new object();
        private readonly EventLevel logLevel;
        private readonly SelfDiagnosticsConfigRefresher configRefresher;
        private readonly ThreadLocal<byte[]> writeBuffer = new ThreadLocal<byte[]>(() => null);
        private readonly List<EventSource> eventSourcesBeforeConstructor = new List<EventSource>();

        private bool disposedValue = false;

        public SelfDiagnosticsEventListener(EventLevel logLevel, SelfDiagnosticsConfigRefresher configRefresher)
        {
            Guard.Null(configRefresher, nameof(configRefresher));

            this.logLevel = logLevel;
            this.configRefresher = configRefresher;

            List<EventSource> eventSources;
            lock (this.lockObj)
            {
                eventSources = this.eventSourcesBeforeConstructor;
                this.eventSourcesBeforeConstructor = null;
            }

            foreach (var eventSource in eventSources)
            {
                this.EnableEvents(eventSource, this.logLevel, EventKeywords.All);
            }
        }

        /// <inheritdoc/>
        public override void Dispose()
        {
            this.Dispose(true);
            base.Dispose();
            GC.SuppressFinalize(this);
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
            if (string.IsNullOrEmpty(str))
            {
                return position;
            }

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
                    buffer = new byte[BUFFERSIZE];
                    this.writeBuffer.Value = buffer;
                }

                var pos = this.DateTimeGetBytes(DateTime.UtcNow, buffer, 0);
                buffer[pos++] = (byte)':';
                pos = EncodeInBuffer(eventMessage, false, buffer, pos);
                if (payload != null)
                {
                    // Not using foreach because it can cause allocations
                    for (int i = 0; i < payload.Count; ++i)
                    {
                        object obj = payload[i];
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
                // Fail to allocate memory for buffer, or
                // A concurrent condition: memory mapped file is disposed in other thread after TryGetLogStream() finishes.
                // In this case, silently fail.
            }
        }

        /// <summary>
        /// Write the <c>datetime</c> formatted string into <c>bytes</c> byte-array starting at <c>byteIndex</c> position.
        /// <para>
        /// [DateTimeKind.Utc]
        /// format: yyyy - MM - dd T HH : mm : ss . fffffff Z (i.e. 2020-12-09T10:20:50.4659412Z).
        /// </para>
        /// <para>
        /// [DateTimeKind.Local]
        /// format: yyyy - MM - dd T HH : mm : ss . fffffff +|- HH : mm (i.e. 2020-12-09T10:20:50.4659412-08:00).
        /// </para>
        /// <para>
        /// [DateTimeKind.Unspecified]
        /// format: yyyy - MM - dd T HH : mm : ss . fffffff (i.e. 2020-12-09T10:20:50.4659412).
        /// </para>
        /// </summary>
        /// <remarks>
        /// The bytes array must be large enough to write 27-33 charaters from the byteIndex starting position.
        /// </remarks>
        /// <param name="datetime">DateTime.</param>
        /// <param name="bytes">Array of bytes to write.</param>
        /// <param name="byteIndex">Starting index into bytes array.</param>
        /// <returns>The number of bytes written.</returns>
        internal int DateTimeGetBytes(DateTime datetime, byte[] bytes, int byteIndex)
        {
            int num;
            int pos = byteIndex;

            num = datetime.Year;
            bytes[pos++] = (byte)('0' + ((num / 1000) % 10));
            bytes[pos++] = (byte)('0' + ((num / 100) % 10));
            bytes[pos++] = (byte)('0' + ((num / 10) % 10));
            bytes[pos++] = (byte)('0' + (num % 10));

            bytes[pos++] = (byte)'-';

            num = datetime.Month;
            bytes[pos++] = (byte)('0' + ((num / 10) % 10));
            bytes[pos++] = (byte)('0' + (num % 10));

            bytes[pos++] = (byte)'-';

            num = datetime.Day;
            bytes[pos++] = (byte)('0' + ((num / 10) % 10));
            bytes[pos++] = (byte)('0' + (num % 10));

            bytes[pos++] = (byte)'T';

            num = datetime.Hour;
            bytes[pos++] = (byte)('0' + ((num / 10) % 10));
            bytes[pos++] = (byte)('0' + (num % 10));

            bytes[pos++] = (byte)':';

            num = datetime.Minute;
            bytes[pos++] = (byte)('0' + ((num / 10) % 10));
            bytes[pos++] = (byte)('0' + (num % 10));

            bytes[pos++] = (byte)':';

            num = datetime.Second;
            bytes[pos++] = (byte)('0' + ((num / 10) % 10));
            bytes[pos++] = (byte)('0' + (num % 10));

            bytes[pos++] = (byte)'.';

            num = (int)(Math.Round(datetime.TimeOfDay.TotalMilliseconds * 10000) % 10000000);
            bytes[pos++] = (byte)('0' + ((num / 1000000) % 10));
            bytes[pos++] = (byte)('0' + ((num / 100000) % 10));
            bytes[pos++] = (byte)('0' + ((num / 10000) % 10));
            bytes[pos++] = (byte)('0' + ((num / 1000) % 10));
            bytes[pos++] = (byte)('0' + ((num / 100) % 10));
            bytes[pos++] = (byte)('0' + ((num / 10) % 10));
            bytes[pos++] = (byte)('0' + (num % 10));

            switch (datetime.Kind)
            {
                case DateTimeKind.Utc:
                    bytes[pos++] = (byte)'Z';
                    break;

                case DateTimeKind.Local:
                    TimeSpan ts = TimeZoneInfo.Local.GetUtcOffset(datetime);

                    bytes[pos++] = (byte)(ts.Hours >= 0 ? '+' : '-');

                    num = Math.Abs(ts.Hours);
                    bytes[pos++] = (byte)('0' + ((num / 10) % 10));
                    bytes[pos++] = (byte)('0' + (num % 10));

                    bytes[pos++] = (byte)':';

                    num = ts.Minutes;
                    bytes[pos++] = (byte)('0' + ((num / 10) % 10));
                    bytes[pos++] = (byte)('0' + (num % 10));
                    break;

                case DateTimeKind.Unspecified:
                default:
                    // Skip
                    break;
            }

            return pos - byteIndex;
        }

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name.StartsWith(EventSourceNamePrefix, StringComparison.Ordinal))
            {
                // If there are EventSource classes already initialized as of now, this method would be called from
                // the base class constructor before the first line of code in SelfDiagnosticsEventListener constructor.
                // In this case logLevel is always its default value, "LogAlways".
                // Thus we should save the event source and enable them later, when code runs in constructor.
                if (this.eventSourcesBeforeConstructor != null)
                {
                    lock (this.lockObj)
                    {
                        if (this.eventSourcesBeforeConstructor != null)
                        {
                            this.eventSourcesBeforeConstructor.Add(eventSource);
                            return;
                        }
                    }
                }

                this.EnableEvents(eventSource, this.logLevel, EventKeywords.All);
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
            this.WriteEvent(eventData.Message, eventData.Payload);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (this.disposedValue)
            {
                return;
            }

            if (disposing)
            {
                this.writeBuffer.Dispose();
            }

            this.disposedValue = true;
        }
    }
}

// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.ObjectModel;
using System.Diagnostics.Tracing;
using System.Text;

namespace OpenTelemetry.Internal;

/// <summary>
/// SelfDiagnosticsEventListener class enables the events from OpenTelemetry event sources
/// and write the events to a local file in a circular way.
/// </summary>
internal sealed class SelfDiagnosticsEventListener : EventListener
{
    // Buffer size of the log line. A UTF-16 encoded character in C# can take up to 4 bytes if encoded in UTF-8.
    private const int BUFFERSIZE = 4 * 5120;
    private const string EventSourceNamePrefix = "OpenTelemetry-";
    private readonly Lock lockObj = new();
    private readonly EventLevel logLevel;
    private readonly SelfDiagnosticsConfigRefresher configRefresher;
    private readonly bool formatMessage;
    private readonly ThreadLocal<byte[]?> writeBuffer = new(() => null);
    private readonly List<EventSource>? eventSourcesBeforeConstructor = [];

    private bool disposedValue;

    public SelfDiagnosticsEventListener(EventLevel logLevel, SelfDiagnosticsConfigRefresher configRefresher, bool formatMessage = false)
    {
        Guard.ThrowIfNull(configRefresher);

        this.logLevel = logLevel;
        this.configRefresher = configRefresher;
        this.formatMessage = formatMessage;

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
    internal static int EncodeInBuffer(string? str, bool isParameter, byte[] buffer, int position)
    {
        if (string.IsNullOrEmpty(str))
        {
            return position;
        }

        int charCount = str!.Length;
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
    /// The bytes array must be large enough to write 27-33 characters from the byteIndex starting position.
    /// </remarks>
    /// <param name="datetime">DateTime.</param>
    /// <param name="bytes">Array of bytes to write.</param>
    /// <param name="byteIndex">Starting index into bytes array.</param>
    /// <returns>The number of bytes written.</returns>
    internal static int DateTimeGetBytes(DateTime datetime, byte[] bytes, int byteIndex)
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

    internal void WriteEvent(string? eventMessage, ReadOnlyCollection<object?>? payload)
    {
        try
        {
            var buffer = this.writeBuffer.Value;
            if (buffer == null)
            {
                buffer = new byte[BUFFERSIZE];
                this.writeBuffer.Value = buffer;
            }

            var pos = DateTimeGetBytes(DateTime.UtcNow, buffer, 0);
            buffer[pos++] = (byte)':';

            if (this.formatMessage && eventMessage != null && payload != null && payload.Count > 0)
            {
                // Use string.Format to format the message with parameters
                string messageToWrite = string.Format(System.Globalization.CultureInfo.InvariantCulture, eventMessage, payload.ToArray());
                pos = EncodeInBuffer(messageToWrite, false, buffer, pos);
            }
            else
            {
                pos = EncodeInBuffer(eventMessage, false, buffer, pos);
                if (payload != null)
                {
                    // Not using foreach because it can cause allocations
                    for (int i = 0; i < payload.Count; ++i)
                    {
                        object? obj = payload[i];
                        if (obj != null)
                        {
                            pos = EncodeInBuffer(obj.ToString() ?? "null", true, buffer, pos);
                        }
                        else
                        {
                            pos = EncodeInBuffer("null", true, buffer, pos);
                        }
                    }
                }
            }

            buffer[pos++] = (byte)'\n';
            int byteCount = pos - 0;
#pragma warning disable CA2000 // Dispose objects before losing scope
            if (this.configRefresher.TryGetLogStream(byteCount, out Stream? stream, out int availableByteCount))
#pragma warning restore CA2000 // Dispose objects before losing scope
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
#pragma warning disable CA1508 // Avoid dead conditional code - see previous comment
                    if (this.eventSourcesBeforeConstructor != null)
#pragma warning restore CA1508 // Avoid dead conditional code - see previous comment
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
        // Note: The EventSource check here works around a bug in EventListener.
        // See: https://github.com/open-telemetry/opentelemetry-dotnet/pull/5046
        if (eventData.EventSource.Name.StartsWith(EventSourceNamePrefix, StringComparison.OrdinalIgnoreCase))
        {
            this.WriteEvent(eventData.Message, eventData.Payload);
        }
    }

    private void Dispose(bool disposing)
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

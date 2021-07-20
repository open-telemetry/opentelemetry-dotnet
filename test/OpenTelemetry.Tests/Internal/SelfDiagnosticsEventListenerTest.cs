// <copyright file="SelfDiagnosticsEventListenerTest.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics.Tracing;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Text;
using Moq;
using Xunit;

namespace OpenTelemetry.Internal.Tests
{
    public class SelfDiagnosticsEventListenerTest
    {
        private const string LOGFILEPATH = "Diagnostics.log";
        private const string Ellipses = "...\n";
        private const string EllipsesWithBrackets = "{...}\n";

        [Fact]
        public void SelfDiagnosticsEventListener_constructor_Invalid_Input()
        {
            // no configRefresher object
            Assert.Throws<ArgumentNullException>(() =>
            {
                _ = new SelfDiagnosticsEventListener(EventLevel.Error, null);
            });
        }

        [Fact]
        public void SelfDiagnosticsEventListener_EventSourceSetup_LowerSeverity()
        {
            var configRefresherMock = new Mock<SelfDiagnosticsConfigRefresher>();
            var listener = new SelfDiagnosticsEventListener(EventLevel.Error, configRefresherMock.Object);

            // Emitting a Verbose event. Or any EventSource event with lower severity than Error.
            OpenTelemetrySdkEventSource.Log.ActivityStarted("Activity started", "1");
            configRefresherMock.Verify(refresher => refresher.TryGetLogStream(It.IsAny<int>(), out It.Ref<Stream>.IsAny, out It.Ref<int>.IsAny), Times.Never());
        }

        [Fact]
        public void SelfDiagnosticsEventListener_EventSourceSetup_HigherSeverity()
        {
            var configRefresherMock = new Mock<SelfDiagnosticsConfigRefresher>();
            configRefresherMock.Setup(configRefresher => configRefresher.TryGetLogStream(It.IsAny<int>(), out It.Ref<Stream>.IsAny, out It.Ref<int>.IsAny))
                            .Returns(true);
            var listener = new SelfDiagnosticsEventListener(EventLevel.Error, configRefresherMock.Object);

            // Emitting an Error event. Or any EventSource event with higher than or equal to to Error severity.
            OpenTelemetrySdkEventSource.Log.TracerProviderException("TestEvent", "Exception Details");
            configRefresherMock.Verify(refresher => refresher.TryGetLogStream(It.IsAny<int>(), out It.Ref<Stream>.IsAny, out It.Ref<int>.IsAny));
        }

        [Fact]
        public void SelfDiagnosticsEventListener_WriteEvent()
        {
            // Arrange
            var configRefresherMock = new Mock<SelfDiagnosticsConfigRefresher>();
            var memoryMappedFile = MemoryMappedFile.CreateFromFile(LOGFILEPATH, FileMode.Create, null, 1024);
            Stream stream = memoryMappedFile.CreateViewStream();
            string eventMessage = "Event Message";
            int timestampPrefixLength = "2020-08-14T20:33:24.4788109Z:".Length;
            byte[] bytes = Encoding.UTF8.GetBytes(eventMessage);
            int availableByteCount = 100;
            configRefresherMock.Setup(configRefresher => configRefresher.TryGetLogStream(timestampPrefixLength + bytes.Length + 1, out stream, out availableByteCount))
                .Returns(true);
            var listener = new SelfDiagnosticsEventListener(EventLevel.Error, configRefresherMock.Object);

            // Act: call WriteEvent method directly
            listener.WriteEvent(eventMessage, null);

            // Assert
            configRefresherMock.Verify(refresher => refresher.TryGetLogStream(timestampPrefixLength + bytes.Length + 1, out stream, out availableByteCount));
            stream.Dispose();
            memoryMappedFile.Dispose();
            AssertFileOutput(LOGFILEPATH, eventMessage);
        }

        [Fact]
        public void SelfDiagnosticsEventListener_DateTimeGetBytes()
        {
            var configRefresherMock = new Mock<SelfDiagnosticsConfigRefresher>();
            var listener = new SelfDiagnosticsEventListener(EventLevel.Error, configRefresherMock.Object);

            // Check DateTimeKind of Utc, Local, and Unspecified
            DateTime[] datetimes = new DateTime[]
            {
                DateTime.SpecifyKind(DateTime.Parse("1996-12-01T14:02:31.1234567-08:00"), DateTimeKind.Utc),
                DateTime.SpecifyKind(DateTime.Parse("1996-12-01T14:02:31.1234567-08:00"), DateTimeKind.Local),
                DateTime.SpecifyKind(DateTime.Parse("1996-12-01T14:02:31.1234567-08:00"), DateTimeKind.Unspecified),
                DateTime.UtcNow,
                DateTime.Now,
            };

            // Expect to match output string from DateTime.ToString("O")
            string[] expected = new string[datetimes.Length];
            for (int i = 0; i < datetimes.Length; i++)
            {
                expected[i] = datetimes[i].ToString("O");
            }

            byte[] buffer = new byte[40 * datetimes.Length];
            int pos = 0;

            // Get string after DateTimeGetBytes() write into a buffer
            string[] results = new string[datetimes.Length];
            for (int i = 0; i < datetimes.Length; i++)
            {
                int len = listener.DateTimeGetBytes(datetimes[i], buffer, pos);
                results[i] = Encoding.Default.GetString(buffer, pos, len);
                pos += len;
            }

            Assert.Equal(expected, results);
        }

        [Fact]
        public void SelfDiagnosticsEventListener_EmitEvent_OmitAsConfigured()
        {
            // Arrange
            var configRefresherMock = new Mock<SelfDiagnosticsConfigRefresher>();
            var memoryMappedFile = MemoryMappedFile.CreateFromFile(LOGFILEPATH, FileMode.Create, null, 1024);
            Stream stream = memoryMappedFile.CreateViewStream();
            configRefresherMock.Setup(configRefresher => configRefresher.TryGetLogStream(It.IsAny<int>(), out stream, out It.Ref<int>.IsAny))
                .Returns(true);
            var listener = new SelfDiagnosticsEventListener(EventLevel.Error, configRefresherMock.Object);

            // Act: emit an event with severity lower than configured
            OpenTelemetrySdkEventSource.Log.ActivityStarted("ActivityStart", "123");

            // Assert
            configRefresherMock.Verify(refresher => refresher.TryGetLogStream(It.IsAny<int>(), out stream, out It.Ref<int>.IsAny), Times.Never());
            stream.Dispose();
            memoryMappedFile.Dispose();

            using FileStream file = File.Open(LOGFILEPATH, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var buffer = new byte[256];
            file.Read(buffer, 0, buffer.Length);
            Assert.Equal('\0', (char)buffer[0]);
        }

        [Fact]
        public void SelfDiagnosticsEventListener_EmitEvent_CaptureAsConfigured()
        {
            // Arrange
            var configRefresherMock = new Mock<SelfDiagnosticsConfigRefresher>();
            var memoryMappedFile = MemoryMappedFile.CreateFromFile(LOGFILEPATH, FileMode.Create, null, 1024);
            Stream stream = memoryMappedFile.CreateViewStream();
            configRefresherMock.Setup(configRefresher => configRefresher.TryGetLogStream(It.IsAny<int>(), out stream, out It.Ref<int>.IsAny))
                .Returns(true);
            var listener = new SelfDiagnosticsEventListener(EventLevel.Error, configRefresherMock.Object);

            // Act: emit an event with severity equal to configured
            OpenTelemetrySdkEventSource.Log.TracerProviderException("TestEvent", "Exception Details");

            // Assert
            configRefresherMock.Verify(refresher => refresher.TryGetLogStream(It.IsAny<int>(), out stream, out It.Ref<int>.IsAny));
            stream.Dispose();
            memoryMappedFile.Dispose();

            var expectedLog = "Unknown error in TracerProvider '{0}': '{1}'.{TestEvent}{Exception Details}";
            AssertFileOutput(LOGFILEPATH, expectedLog);
        }

        [Fact]
        public void SelfDiagnosticsEventListener_EncodeInBuffer_Null()
        {
            byte[] buffer = new byte[20];
            int startPos = 0;
            int endPos = SelfDiagnosticsEventListener.EncodeInBuffer(null, false, buffer, startPos);
            Assert.Equal(startPos, endPos);
        }

        [Fact]
        public void SelfDiagnosticsEventListener_EncodeInBuffer_Empty()
        {
            byte[] buffer = new byte[20];
            int startPos = 0;
            int endPos = SelfDiagnosticsEventListener.EncodeInBuffer(string.Empty, false, buffer, startPos);
            byte[] expected = Encoding.UTF8.GetBytes(string.Empty);
            AssertBufferOutput(expected, buffer, startPos, endPos);
        }

        [Fact]
        public void SelfDiagnosticsEventListener_EncodeInBuffer_EnoughSpace()
        {
            byte[] buffer = new byte[20];
            int startPos = buffer.Length - Ellipses.Length - 6;  // Just enough space for "abc" even if "...\n" needs to be added.
            int endPos = SelfDiagnosticsEventListener.EncodeInBuffer("abc", false, buffer, startPos);

            // '\n' will be appended to the original string "abc" after EncodeInBuffer is called.
            // The byte where '\n' will be placed should not be touched within EncodeInBuffer, so it stays as '\0'.
            byte[] expected = Encoding.UTF8.GetBytes("abc\0");
            AssertBufferOutput(expected, buffer, startPos, endPos + 1);
        }

        [Fact]
        public void SelfDiagnosticsEventListener_EncodeInBuffer_NotEnoughSpaceForFullString()
        {
            byte[] buffer = new byte[20];
            int startPos = buffer.Length - Ellipses.Length - 5;  // Just not space for "abc" if "...\n" needs to be added.

            // It's a quick estimate by assumption that most Unicode characters takes up to 2 16-bit UTF-16 chars,
            // which can be up to 4 bytes when encoded in UTF-8.
            int endPos = SelfDiagnosticsEventListener.EncodeInBuffer("abc", false, buffer, startPos);
            byte[] expected = Encoding.UTF8.GetBytes("ab...\0");
            AssertBufferOutput(expected, buffer, startPos, endPos + 1);
        }

        [Fact]
        public void SelfDiagnosticsEventListener_EncodeInBuffer_NotEvenSpaceForTruncatedString()
        {
            byte[] buffer = new byte[20];
            int startPos = buffer.Length - Ellipses.Length;  // Just enough space for "...\n".
            int endPos = SelfDiagnosticsEventListener.EncodeInBuffer("abc", false, buffer, startPos);
            byte[] expected = Encoding.UTF8.GetBytes("...\0");
            AssertBufferOutput(expected, buffer, startPos, endPos + 1);
        }

        [Fact]
        public void SelfDiagnosticsEventListener_EncodeInBuffer_NotEvenSpaceForTruncationEllipses()
        {
            byte[] buffer = new byte[20];
            int startPos = buffer.Length - Ellipses.Length + 1;  // Not enough space for "...\n".
            int endPos = SelfDiagnosticsEventListener.EncodeInBuffer("abc", false, buffer, startPos);
            Assert.Equal(startPos, endPos);
        }

        [Fact]
        public void SelfDiagnosticsEventListener_EncodeInBuffer_IsParameter_EnoughSpace()
        {
            byte[] buffer = new byte[20];
            int startPos = buffer.Length - EllipsesWithBrackets.Length - 6;  // Just enough space for "abc" even if "...\n" need to be added.
            int endPos = SelfDiagnosticsEventListener.EncodeInBuffer("abc", true, buffer, startPos);
            byte[] expected = Encoding.UTF8.GetBytes("{abc}\0");
            AssertBufferOutput(expected, buffer, startPos, endPos + 1);
        }

        [Fact]
        public void SelfDiagnosticsEventListener_EncodeInBuffer_IsParameter_NotEnoughSpaceForFullString()
        {
            byte[] buffer = new byte[20];
            int startPos = buffer.Length - EllipsesWithBrackets.Length - 5;  // Just not space for "...\n".
            int endPos = SelfDiagnosticsEventListener.EncodeInBuffer("abc", true, buffer, startPos);
            byte[] expected = Encoding.UTF8.GetBytes("{ab...}\0");
            AssertBufferOutput(expected, buffer, startPos, endPos + 1);
        }

        [Fact]
        public void SelfDiagnosticsEventListener_EncodeInBuffer_IsParameter_NotEvenSpaceForTruncatedString()
        {
            byte[] buffer = new byte[20];
            int startPos = buffer.Length - EllipsesWithBrackets.Length;  // Just enough space for "{...}\n".
            int endPos = SelfDiagnosticsEventListener.EncodeInBuffer("abc", true, buffer, startPos);
            byte[] expected = Encoding.UTF8.GetBytes("{...}\0");
            AssertBufferOutput(expected, buffer, startPos, endPos + 1);
        }

        [Fact]
        public void SelfDiagnosticsEventListener_EncodeInBuffer_IsParameter_NotEvenSpaceForTruncationEllipses()
        {
            byte[] buffer = new byte[20];
            int startPos = buffer.Length - EllipsesWithBrackets.Length + 1;  // Not enough space for "{...}\n".
            int endPos = SelfDiagnosticsEventListener.EncodeInBuffer("abc", true, buffer, startPos);
            Assert.Equal(startPos, endPos);
        }

        private static void AssertFileOutput(string filePath, string eventMessage)
        {
            using FileStream file = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var buffer = new byte[256];
            file.Read(buffer, 0, buffer.Length);
            string logLine = Encoding.UTF8.GetString(buffer);
            string logMessage = ParseLogMessage(logLine);
            Assert.StartsWith(eventMessage, logMessage);
        }

        private static string ParseLogMessage(string logLine)
        {
            int timestampPrefixLength = "2020-08-14T20:33:24.4788109Z:".Length;
            Assert.Matches(@"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{7}Z:", logLine.Substring(0, timestampPrefixLength));
            return logLine.Substring(timestampPrefixLength);
        }

        private static void AssertBufferOutput(byte[] expected, byte[] buffer, int startPos, int endPos)
        {
            Assert.Equal(expected.Length, endPos - startPos);
            for (int i = 0, j = startPos; j < endPos; ++i, ++j)
            {
                Assert.Equal(expected[i], buffer[j]);
            }
        }
    }
}

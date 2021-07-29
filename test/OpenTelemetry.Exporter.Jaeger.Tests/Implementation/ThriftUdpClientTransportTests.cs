// <copyright file="ThriftUdpClientTransportTests.cs" company="OpenTelemetry Authors">
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
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Thrift.Transport;
using Xunit;

namespace OpenTelemetry.Exporter.Jaeger.Implementation.Tests
{
    public class ThriftUdpClientTransportTests : IDisposable
    {
        private readonly Mock<IJaegerClient> mockClient = new Mock<IJaegerClient>();
        private MemoryStream testingMemoryStream = new MemoryStream();

        public void Dispose()
        {
            this.testingMemoryStream?.Dispose();
        }

        [Fact]
        public void Constructor_ShouldConnectClient()
        {
            var host = "host, yo";
            var port = 4528;

            new JaegerThriftClientTransport(host, port, this.testingMemoryStream, this.mockClient.Object);

            this.mockClient.Verify(t => t.Connect(host, port), Times.Once);
        }

        [Fact]
        public void Close_ShouldCloseClient()
        {
            var host = "host, yo";
            var port = 4528;

            var transport = new JaegerThriftClientTransport(host, port, this.testingMemoryStream, this.mockClient.Object);
            transport.Close();

            this.mockClient.Verify(t => t.Close(), Times.Once);
        }

        [Fact]
        public async Task Write_ShouldWriteToMemoryStream()
        {
            var host = "host, yo";
            var port = 4528;
            var writeBuffer = new byte[] { 0x20, 0x10, 0x40, 0x30, 0x18, 0x14, 0x10, 0x28 };
            var readBuffer = new byte[8];

            var transport = new JaegerThriftClientTransport(host, port, this.testingMemoryStream, this.mockClient.Object);

            transport.Write(writeBuffer);
            this.testingMemoryStream.Seek(0, SeekOrigin.Begin);
            var size = await this.testingMemoryStream.ReadAsync(readBuffer, 0, 8, CancellationToken.None);

            Assert.Equal(8, size);
            Assert.Equal(writeBuffer, readBuffer);
        }

        [Fact]
        public void Flush_ShouldReturnWhenNothingIsInTheStream()
        {
            var host = "host, yo";
            var port = 4528;

            var transport = new JaegerThriftClientTransport(host, port, this.testingMemoryStream, this.mockClient.Object);
            var tInfo = transport.Flush();

            this.mockClient.Verify(t => t.Send(It.IsAny<byte[]>()), Times.Never);
        }

        [Fact]
        public void Flush_ShouldSendStreamBytes()
        {
            var host = "host, yo";
            var port = 4528;
            var streamBytes = new byte[] { 0x20, 0x10, 0x40, 0x30, 0x18, 0x14, 0x10, 0x28 };
            this.testingMemoryStream = new MemoryStream(streamBytes);

            var transport = new JaegerThriftClientTransport(host, port, this.testingMemoryStream, this.mockClient.Object);
            var tInfo = transport.Flush();

            this.mockClient.Verify(t => t.Send(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>()), Times.Once);
        }

        [Fact]
        public void Flush_ShouldThrowWhenClientDoes()
        {
            var host = "host, yo";
            var port = 4528;
            var streamBytes = new byte[] { 0x20, 0x10, 0x40, 0x30, 0x18, 0x14, 0x10, 0x28 };
            this.testingMemoryStream = new MemoryStream(streamBytes);

            this.mockClient.Setup(t => t.Send(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>())).Throws(new Exception("message, yo"));

            var transport = new JaegerThriftClientTransport(host, port, this.testingMemoryStream, this.mockClient.Object);

            var ex = Assert.Throws<TTransportException>(() => transport.Flush());

            Assert.Equal("Cannot flush closed transport. message, yo", ex.Message);
        }

        [Fact]
        public void Dispose_ShouldCloseClientAndDisposeMemoryStream()
        {
            var host = "host, yo";
            var port = 4528;

            var transport = new JaegerThriftClientTransport(host, port, this.testingMemoryStream, this.mockClient.Object);
            transport.Dispose();

            this.mockClient.Verify(t => t.Dispose(), Times.Once);
            Assert.False(this.testingMemoryStream.CanRead);
            Assert.False(this.testingMemoryStream.CanSeek);
            Assert.False(this.testingMemoryStream.CanWrite);
        }

        [Fact]
        public void Dispose_ShouldNotTryToDisposeResourcesMoreThanOnce()
        {
            var host = "host, yo";
            var port = 4528;

            var transport = new JaegerThriftClientTransport(host, port, this.testingMemoryStream, this.mockClient.Object);
            transport.Dispose();
            transport.Dispose();

            this.mockClient.Verify(t => t.Dispose(), Times.Once);
            Assert.False(this.testingMemoryStream.CanRead);
            Assert.False(this.testingMemoryStream.CanSeek);
            Assert.False(this.testingMemoryStream.CanWrite);
        }
    }
}

// <copyright file="ThriftUdpClientTransportTests.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
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
using OpenTelemetry.Exporter.Jaeger.Implementation;
using Thrift.Transports;
using Xunit;

namespace OpenTelemetry.Exporter.Jaeger.Tests.Implementation
{
    public class ThriftUdpClientTransportTests: IDisposable
    {
        private MemoryStream testingMemoryStream = new MemoryStream();
        private readonly Mock<IJaegerUdpClient> mockClient = new Mock<IJaegerUdpClient>();

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
        public async Task ReadAsync_ShouldResultInNotImplementedException()
        {
            var host = "host, yo";
            var port = 4528;

            var transport = new JaegerThriftClientTransport(host, port, this.testingMemoryStream, this.mockClient.Object);
            var newBuffer = new byte[8];

            await Assert.ThrowsAsync<NotImplementedException>(async () => await transport.ReadAsync(newBuffer, 0, 7, CancellationToken.None));
        }

        [Fact]
        public async Task WriteAsync_ShouldWriteToMemoryStream()
        {
            var host = "host, yo";
            var port = 4528;
            var writeBuffer = new byte[] { 0x20, 0x10, 0x40, 0x30, 0x18, 0x14, 0x10, 0x28 };
            var readBuffer = new byte[8];

            var transport = new JaegerThriftClientTransport(host, port, this.testingMemoryStream, this.mockClient.Object);

            await transport.WriteAsync(writeBuffer, CancellationToken.None);
            this.testingMemoryStream.Seek(0, SeekOrigin.Begin);
            var size = await this.testingMemoryStream.ReadAsync(readBuffer, 0, 8, CancellationToken.None);

            Assert.Equal(8, size);
            Assert.Equal(writeBuffer, readBuffer);
        }

        [Fact]
        public void FlushAsync_ShouldReturnWhenNothingIsInTheStream()
        {
            var host = "host, yo";
            var port = 4528;

            var transport = new JaegerThriftClientTransport(host, port, this.testingMemoryStream, this.mockClient.Object);
            var tInfo = transport.FlushAsync();

            Assert.True(tInfo.IsCompleted);
            this.mockClient.Verify(t => t.SendAsync(It.IsAny<byte[]>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public void FlushAsync_ShouldSendStreamBytes()
        {
            var host = "host, yo";
            var port = 4528;
            var streamBytes = new byte[] { 0x20, 0x10, 0x40, 0x30, 0x18, 0x14, 0x10, 0x28 };
            this.testingMemoryStream = new MemoryStream(streamBytes);

            var transport = new JaegerThriftClientTransport(host, port, this.testingMemoryStream, this.mockClient.Object);
            var tInfo = transport.FlushAsync();

            Assert.True(tInfo.IsCompleted);
            this.mockClient.Verify(t => t.SendAsync(It.IsAny<byte[]>(), 8), Times.Once);
        }

        [Fact]
        public async Task FlushAsync_ShouldThrowWhenClientDoes()
        {
            var host = "host, yo";
            var port = 4528;
            var streamBytes = new byte[] { 0x20, 0x10, 0x40, 0x30, 0x18, 0x14, 0x10, 0x28 };
            this.testingMemoryStream = new MemoryStream(streamBytes);

            //this.mockClient.Setup(t => t.SendAsync(It.IsAny<byte[]>(), It.IsAny<int>())).Throws<Exception>("message, yo");
            this.mockClient.Setup(t => t.SendAsync(It.IsAny<byte[]>(), It.IsAny<int>())).Throws(new Exception("message, yo"));

            var transport = new JaegerThriftClientTransport(host, port, this.testingMemoryStream, this.mockClient.Object);
            var ex = await Assert.ThrowsAsync<TTransportException>(() => transport.FlushAsync());

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

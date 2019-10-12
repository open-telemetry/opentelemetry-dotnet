// <copyright file="JaegerThriftClientTransport.cs" company="OpenTelemetry Authors">
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
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Thrift.Transports;

namespace OpenTelemetry.Exporter.Jaeger.Implementation
{
    public class JaegerThriftClientTransport : TClientTransport
    {
        private readonly IJaegerUdpClient udpClient;
        private readonly MemoryStream byteStream;
        private bool isDisposed = false;

        public JaegerThriftClientTransport(string host, int port)
            : this(host, port, new MemoryStream(), new JaegerUdpClient())
        {
        }

        public JaegerThriftClientTransport(string host, int port, MemoryStream stream, IJaegerUdpClient client)
        {
            this.byteStream = stream;
            this.udpClient = client;
            this.udpClient.Connect(host, port);
        }

        public override bool IsOpen => this.udpClient.Connected;

        public override void Close()
        {
            this.udpClient.Close();
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            var bytes = this.byteStream.ToArray();

            if (bytes.Length == 0)
            {
                return Task.CompletedTask;
            }

            this.byteStream.SetLength(0);

            try
            {
                return this.udpClient.SendAsync(bytes, bytes.Length);
            }
            catch (SocketException se)
            {
                throw new TTransportException(TTransportException.ExceptionType.Unknown, $"Cannot flush because of socket exception. UDP Packet size was {bytes.Length}. Exception message: {se.Message}");
            }
            catch (Exception e)
            {
                throw new TTransportException(TTransportException.ExceptionType.Unknown, $"Cannot flush closed transport. {e.Message}");
            }
        }

        public override Task OpenAsync(CancellationToken cancellationToken)
        {
            // Do nothing
            return Task.CompletedTask;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int length, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public override Task WriteAsync(byte[] buffer, CancellationToken cancellationToken)
        {
            return this.WriteAsync(buffer, 0, buffer.Length, cancellationToken);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int length, CancellationToken cancellationToken)
        {
            return this.byteStream.WriteAsync(buffer, offset, length, cancellationToken);
        }

        public override string ToString()
        {
            return $"{nameof(JaegerThriftClientTransport)}(Client={this.udpClient.RemoteEndPoint})";
        }

        protected override void Dispose(bool disposing)
        {
            if (!this.isDisposed && disposing)
            {
                this.byteStream?.Dispose();
                this.udpClient?.Dispose();
            }

            this.isDisposed = true;
        }
    }
}

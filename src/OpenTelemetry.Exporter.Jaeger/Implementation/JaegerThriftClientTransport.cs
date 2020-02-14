﻿// <copyright file="JaegerThriftClientTransport.cs" company="OpenTelemetry Authors">
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
        private readonly IJaegerClient client;
        private readonly MemoryStream byteStream;
        private bool isDisposed = false;

        public JaegerThriftClientTransport(string host, int port)
            : this(host, port, new MemoryStream(), new JaegerUdpClient())
        {
        }

        public JaegerThriftClientTransport(string host, int port, MemoryStream stream, IJaegerClient client)
        {
            this.byteStream = stream;
            this.client = client;
            this.client.Connect(host, port);
        }

        public override bool IsOpen => this.client.Connected;

        public override void Close()
        {
            this.client.Close();
        }

#if NETSTANDARD2_1
        public override async ValueTask FlushAsync(CancellationToken cancellationToken)
#else
        public override async Task FlushAsync(CancellationToken cancellationToken)
#endif
        {
            // GetBuffer returns the underlying storage, which saves an allocation over ToArray.
            if (!this.byteStream.TryGetBuffer(out var buffer))
            {
                buffer = new ArraySegment<byte>(this.byteStream.ToArray(), 0, (int)this.byteStream.Length);
            }

            if (buffer.Count == 0)
            {
                return;
            }

            try
            {
                await this.client.SendAsync(buffer.Array, buffer.Offset, buffer.Count).ConfigureAwait(false);
            }
            catch (SocketException se)
            {
                throw new TTransportException(TTransportException.ExceptionType.Unknown, $"Cannot flush because of socket exception. UDP Packet size was {buffer.Count}. Exception message: {se.Message}");
            }
            catch (Exception e)
            {
                throw new TTransportException(TTransportException.ExceptionType.Unknown, $"Cannot flush closed transport. {e.Message}");
            }
            finally
            {
                this.byteStream.SetLength(0);
            }
        }

#if NETSTANDARD2_1
        public override async ValueTask OpenAsync(CancellationToken cancellationToken)
#else
        public override async Task OpenAsync(CancellationToken cancellationToken)
#endif
        {
            if (cancellationToken.IsCancellationRequested)
            {
                await Task.FromCanceled(cancellationToken).ConfigureAwait(false);
            }
        }

#if NETSTANDARD2_1
        public override ValueTask<int> ReadAsync(byte[] buffer, int offset, int length, CancellationToken cancellationToken)
#else
        public override Task<int> ReadAsync(byte[] buffer, int offset, int length, CancellationToken cancellationToken)
#endif
        {
            throw new NotImplementedException();
        }

#if NETSTANDARD2_1
        public override ValueTask WriteAsync(byte[] buffer, int offset, int length, CancellationToken cancellationToken)
        {
            return this.byteStream.WriteAsync(new ReadOnlyMemory<byte>(buffer, offset, length), cancellationToken);
        }
#else
        public override Task WriteAsync(byte[] buffer, int offset, int length, CancellationToken cancellationToken)
        {
            return this.byteStream.WriteAsync(buffer, offset, length, cancellationToken);
        }
#endif

        public override string ToString()
        {
            return $"{nameof(JaegerThriftClientTransport)}(Client={this.client.RemoteEndPoint})";
        }

        protected override void Dispose(bool disposing)
        {
            if (!this.isDisposed && disposing)
            {
                this.byteStream?.Dispose();
                this.client?.Dispose();
            }

            this.isDisposed = true;
        }
    }
}

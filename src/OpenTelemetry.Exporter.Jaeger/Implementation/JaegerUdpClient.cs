// <copyright file="JaegerUdpClient.cs" company="OpenTelemetry Authors">
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
#if NETSTANDARD2_1
using System;
#else
using System;
using System.Diagnostics;
#endif
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace OpenTelemetry.Exporter.Jaeger.Implementation
{
    internal class JaegerUdpClient : IJaegerClient
    {
        private readonly UdpClient client;
        private bool disposed;

        public JaegerUdpClient()
        {
            this.client = new UdpClient();
        }

        public bool Connected => this.client.Client.Connected;

        public EndPoint RemoteEndPoint => this.client.Client.RemoteEndPoint;

        public void Close() => this.client.Close();

        public void Connect(string host, int port) => this.client.Connect(host, port);

        public ValueTask<int> SendAsync(byte[] buffer, CancellationToken cancellationToken = default)
        {
            return this.SendAsync(buffer, 0, buffer?.Length ?? 0, cancellationToken);
        }

        public ValueTask<int> SendAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default)
        {
#if NETSTANDARD2_1
            return this.client.Client.SendAsync(new ReadOnlyMemory<byte>(buffer, offset, count), SocketFlags.None, cancellationToken);
#else
            Debug.Assert(offset == 0, "Offset isn't supported in .NET Standard 2.0.");
            return new ValueTask<int>(this.client.SendAsync(buffer, count));
#endif
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases the unmanaged resources used by this class and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing"><see langword="true"/> to release both managed and unmanaged resources; <see langword="false"/> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }

            if (disposing)
            {
                this.client.Dispose();
            }

            this.disposed = true;
        }
    }
}

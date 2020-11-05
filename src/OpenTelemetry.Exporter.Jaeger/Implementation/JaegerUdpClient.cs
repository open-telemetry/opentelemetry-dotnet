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

using System;
using System.Net;
using System.Net.Sockets;

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

        public int Send(byte[] buffer)
        {
            return this.Send(buffer, 0, buffer?.Length ?? 0);
        }

        public int Send(byte[] buffer, int offset, int count)
        {
            return this.client.Client.Send(buffer, offset, count, SocketFlags.None);
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

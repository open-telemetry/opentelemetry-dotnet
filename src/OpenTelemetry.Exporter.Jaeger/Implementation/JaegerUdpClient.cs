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

using System.Net.Sockets;

namespace OpenTelemetry.Exporter.Jaeger.Implementation
{
    internal sealed class JaegerUdpClient : IJaegerClient
    {
        private readonly string host;
        private readonly int port;
        private readonly UdpClient client;
        private bool disposed;

        public JaegerUdpClient(string host, int port)
        {
            this.host = host;
            this.port = port;
            this.client = new UdpClient();
        }

        public bool Connected => this.client.Client.Connected;

        public void Close() => this.client.Close();

        public void Connect() => this.client.Connect(this.host, this.port);

        public int Send(byte[] buffer, int offset, int count)
        {
            return this.client.Client.Send(buffer, offset, count, SocketFlags.None);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (this.disposed)
            {
                return;
            }

            this.client.Dispose();

            this.disposed = true;
        }
    }
}

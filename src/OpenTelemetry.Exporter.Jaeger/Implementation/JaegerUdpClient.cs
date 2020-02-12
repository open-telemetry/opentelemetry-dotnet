﻿// <copyright file="JaegerUdpClient.cs" company="OpenTelemetry Authors">
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
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace OpenTelemetry.Exporter.Jaeger.Implementation
{
    public class JaegerUdpClient : IJaegerClient
    {
        private readonly UdpClient client;

        public JaegerUdpClient()
        {
            this.client = new UdpClient();
        }

        public bool Connected => this.client.Client.Connected;

        public EndPoint RemoteEndPoint => this.client.Client.RemoteEndPoint;

        public void Close() => this.client.Close();

        public void Connect(string host, int port) => this.client.Connect(host, port);

        public Task<int> SendAsync(byte[] buffer, int offset, int count)
        {
            return Task<int>.Factory.FromAsync(
                (callback, state) => ((Socket)state).BeginSend(buffer, offset, count, SocketFlags.None, callback, state),
                asyncResult => ((Socket)asyncResult.AsyncState).EndSend(asyncResult),
                state: this.client.Client);
        }

        public void Dispose() => this.client.Dispose();
    }
}

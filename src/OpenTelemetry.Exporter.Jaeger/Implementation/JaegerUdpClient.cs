// <copyright file="JaegerUdpClient.cs" company="OpenTelemetry Authors">
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
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace OpenTelemetry.Exporter.Jaeger.Implementation
{
    internal class JaegerUdpClient : IJaegerClient
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

        public ValueTask<int> SendAsync(byte[] buffer, CancellationToken cancellationToken = default)
        {
            return this.SendAsync(buffer, 0, buffer?.Length ?? 0);
        }

        public ValueTask<int> SendAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default)
        {
            var socket = this.client.Client;

            var asyncResult = socket.BeginSend(
                buffer,
                offset,
                count,
                SocketFlags.None,
                callback: null,
                state: null);

            if (asyncResult.CompletedSynchronously)
            {
                return new ValueTask<int>(socket.EndSend(asyncResult));
            }

            var tcs = new TaskCompletionSource<int>();

            ThreadPool.RegisterWaitForSingleObject(
                waitObject: asyncResult.AsyncWaitHandle,
                callBack: (s, t) => tcs.SetResult(socket.EndSend(asyncResult)),
                state: null,
                millisecondsTimeOutInterval: -1,
                executeOnlyOnce: true);

            return new ValueTask<int>(tcs.Task);
        }

        public void Dispose() => this.client.Dispose();
    }
}

// <copyright file="IJaegerClient.cs" company="OpenTelemetry Authors">
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
using System.Threading;
using System.Threading.Tasks;

namespace OpenTelemetry.Exporter.Jaeger.Implementation
{
    internal interface IJaegerClient : IDisposable
    {
        bool Connected { get; }

        EndPoint RemoteEndPoint { get; }

        void Connect(string host, int port);

        void Close();

        ValueTask<int> SendAsync(byte[] buffer, CancellationToken cancellationToken = default);

        ValueTask<int> SendAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default);
    }
}

// Licensed to the Apache Software Foundation(ASF) under one
// or more contributor license agreements.See the NOTICE file
// distributed with this work for additional information
// regarding copyright ownership.The ASF licenses this file
// to you under the Apache License, Version 2.0 (the
// "License"); you may not use this file except in compliance
// with the License. You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing,
// software distributed under the License is distributed on an
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
// KIND, either express or implied. See the License for the
// specific language governing permissions and limitations
// under the License.
using System;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace Thrift.Transports.Client
{
    // ReSharper disable once InconsistentNaming
    public class TNamedPipeClientTransport : TClientTransport
    {
        private NamedPipeClientStream _client;

        public TNamedPipeClientTransport(string pipe) : this(".", pipe)
        {
        }

        public TNamedPipeClientTransport(string server, string pipe)
        {
            var serverName = string.IsNullOrWhiteSpace(server) ? server : ".";

            _client = new NamedPipeClientStream(serverName, pipe, PipeDirection.InOut, PipeOptions.None);
        }

        public override bool IsOpen => _client != null && _client.IsConnected;

#if NETSTANDARD2_1
        public override async ValueTask OpenAsync(CancellationToken cancellationToken)
#else
        public override async Task OpenAsync(CancellationToken cancellationToken)
#endif
        {
            if (IsOpen)
            {
                throw new TTransportException(TTransportException.ExceptionType.AlreadyOpen);
            }

            await _client.ConnectAsync(cancellationToken).ConfigureAwait(false);
        }

        public override void Close()
        {
            if (_client != null)
            {
                _client.Dispose();
                _client = null;
            }
        }

#if NETSTANDARD2_1
        public override async ValueTask<int> ReadAsync(byte[] buffer, int offset, int length, CancellationToken cancellationToken)
#else
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int length, CancellationToken cancellationToken)
#endif
        {
            if (_client == null)
            {
                throw new TTransportException(TTransportException.ExceptionType.NotOpen);
            }

#if NETSTANDARD2_1
            return await _client.ReadAsync(new Memory<byte>(buffer, offset, length), cancellationToken).ConfigureAwait(false);
#else
            return await _client.ReadAsync(buffer, offset, length, cancellationToken).ConfigureAwait(false);
#endif
        }

#if NETSTANDARD2_1
        public override async ValueTask WriteAsync(byte[] buffer, int offset, int length, CancellationToken cancellationToken)
#else
        public override async Task WriteAsync(byte[] buffer, int offset, int length, CancellationToken cancellationToken)
#endif
        {
            if (_client == null)
            {
                throw new TTransportException(TTransportException.ExceptionType.NotOpen);
            }

#if NETSTANDARD2_1
            await _client.WriteAsync(new ReadOnlyMemory<byte>(buffer, offset, length), cancellationToken).ConfigureAwait(false);
#else
            await _client.WriteAsync(buffer, offset, length, cancellationToken).ConfigureAwait(false);
#endif
        }

#if NETSTANDARD2_1
        public override async ValueTask FlushAsync(CancellationToken cancellationToken)
#else
        public override async Task FlushAsync(CancellationToken cancellationToken)
#endif
        {
            if (cancellationToken.IsCancellationRequested)
            {
                await Task.FromCanceled(cancellationToken).ConfigureAwait(false);
            }
        }

        protected override void Dispose(bool disposing)
        {
            _client.Dispose();
        }
    }
}

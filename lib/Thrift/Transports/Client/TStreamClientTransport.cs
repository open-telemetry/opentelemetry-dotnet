﻿// Licensed to the Apache Software Foundation(ASF) under one
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
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Thrift.Transports.Client
{
    // ReSharper disable once InconsistentNaming
    public class TStreamClientTransport : TClientTransport
    {
        private bool _isDisposed;

        protected TStreamClientTransport()
        {
        }

        public TStreamClientTransport(Stream inputStream, Stream outputStream)
        {
            InputStream = inputStream;
            OutputStream = outputStream;
        }

        protected Stream OutputStream { get; set; }

        protected Stream InputStream { get; set; }

        public override bool IsOpen => true;

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

        public override void Close()
        {
            if (InputStream != null)
            {
                InputStream.Dispose();
                InputStream = null;
            }

            if (OutputStream != null)
            {
                OutputStream.Dispose();
                OutputStream = null;
            }
        }

#if NETSTANDARD2_1
        public override async ValueTask<int> ReadAsync(byte[] buffer, int offset, int length, CancellationToken cancellationToken)
#else
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int length, CancellationToken cancellationToken)
#endif
        {
            if (InputStream == null)
            {
                throw new TTransportException(TTransportException.ExceptionType.NotOpen,
                    "Cannot read from null inputstream");
            }

#if NETSTANDARD2_1
            return await InputStream.ReadAsync(new Memory<byte>(buffer, offset, length), cancellationToken).ConfigureAwait(false);
#else
            return await InputStream.ReadAsync(buffer, offset, length, cancellationToken).ConfigureAwait(false);
#endif
        }

#if NETSTANDARD2_1
        public override async ValueTask WriteAsync(byte[] buffer, int offset, int length, CancellationToken cancellationToken)
#else
        public override async Task WriteAsync(byte[] buffer, int offset, int length, CancellationToken cancellationToken)
#endif
        {
            if (OutputStream == null)
            {
                throw new TTransportException(TTransportException.ExceptionType.NotOpen,
                    "Cannot read from null inputstream");
            }

#if NETSTANDARD2_1
            await OutputStream.WriteAsync(new ReadOnlyMemory<byte>(buffer, offset, length), cancellationToken).ConfigureAwait(false);
#else
            await OutputStream.WriteAsync(buffer, offset, length, cancellationToken).ConfigureAwait(false);
#endif
        }

#if NETSTANDARD2_1
        public override async ValueTask FlushAsync(CancellationToken cancellationToken)
#else
        public override async Task FlushAsync(CancellationToken cancellationToken)
#endif
        {
            await OutputStream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        // IDisposable
        protected override void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    InputStream?.Dispose();
                    OutputStream?.Dispose();
                }
            }
            _isDisposed = true;
        }
    }
}

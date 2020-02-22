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
using System.Buffers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Thrift.Protocols.Entities;
using Thrift.Transports;

namespace Thrift.Protocols
{
    // ReSharper disable once InconsistentNaming
    public abstract class TProtocol : IDisposable
    {
        public const int DefaultRecursionDepth = 64;
        private bool _isDisposed;
        protected int RecursionDepth;

        protected TClientTransport Trans;

        protected TProtocol(TClientTransport trans)
        {
            Trans = trans;
            RecursionLimit = DefaultRecursionDepth;
            RecursionDepth = 0;
        }

        public TClientTransport Transport => Trans;

        protected int RecursionLimit { get; set; }

        public void Dispose()
        {
            Dispose(true);
        }

        public void IncrementRecursionDepth()
        {
            if (RecursionDepth < RecursionLimit)
            {
                ++RecursionDepth;
            }
            else
            {
                throw new TProtocolException(TProtocolException.DEPTH_LIMIT, "Depth limit exceeded");
            }
        }

        public void DecrementRecursionDepth()
        {
            --RecursionDepth;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    (Trans as IDisposable)?.Dispose();
                }
            }
            _isDisposed = true;
        }

        public virtual async ValueTask WriteMessageBeginAsync(TMessage message)
        {
            await WriteMessageBeginAsync(message, CancellationToken.None).ConfigureAwait(false);
        }

        public abstract ValueTask WriteMessageBeginAsync(TMessage message, CancellationToken cancellationToken);

        public virtual async ValueTask WriteMessageEndAsync()
        {
            await WriteMessageEndAsync(CancellationToken.None).ConfigureAwait(false);
        }

        public abstract ValueTask WriteMessageEndAsync(CancellationToken cancellationToken);

        public virtual async ValueTask WriteStructBeginAsync(TStruct @struct)
        {
            await WriteStructBeginAsync(@struct, CancellationToken.None).ConfigureAwait(false);
        }

        public abstract ValueTask WriteStructBeginAsync(TStruct @struct, CancellationToken cancellationToken);

        public virtual async ValueTask WriteStructEndAsync()
        {
            await WriteStructEndAsync(CancellationToken.None).ConfigureAwait(false);
        }

        public abstract ValueTask WriteStructEndAsync(CancellationToken cancellationToken);

        public virtual async ValueTask WriteFieldBeginAsync(TField field)
        {
            await WriteFieldBeginAsync(field, CancellationToken.None).ConfigureAwait(false);
        }

        public abstract ValueTask WriteFieldBeginAsync(TField field, CancellationToken cancellationToken);

        public virtual async ValueTask WriteFieldEndAsync()
        {
            await WriteFieldEndAsync(CancellationToken.None).ConfigureAwait(false);
        }

        public abstract ValueTask WriteFieldEndAsync(CancellationToken cancellationToken);

        public virtual async ValueTask WriteFieldStopAsync()
        {
            await WriteFieldStopAsync(CancellationToken.None).ConfigureAwait(false);
        }

        public abstract ValueTask WriteFieldStopAsync(CancellationToken cancellationToken);

        public virtual async ValueTask WriteMapBeginAsync(TMap map)
        {
            await WriteMapBeginAsync(map, CancellationToken.None).ConfigureAwait(false);
        }

        public abstract ValueTask WriteMapBeginAsync(TMap map, CancellationToken cancellationToken);

        public virtual async ValueTask WriteMapEndAsync()
        {
            await WriteMapEndAsync(CancellationToken.None).ConfigureAwait(false);
        }

        public abstract ValueTask WriteMapEndAsync(CancellationToken cancellationToken);

        public virtual async ValueTask WriteListBeginAsync(TList list)
        {
            await WriteListBeginAsync(list, CancellationToken.None).ConfigureAwait(false);
        }

        public abstract ValueTask WriteListBeginAsync(TList list, CancellationToken cancellationToken);

        public virtual async ValueTask WriteListEndAsync()
        {
            await WriteListEndAsync(CancellationToken.None).ConfigureAwait(false);
        }

        public abstract ValueTask WriteListEndAsync(CancellationToken cancellationToken);

        public virtual async ValueTask WriteSetBeginAsync(TSet set)
        {
            await WriteSetBeginAsync(set, CancellationToken.None).ConfigureAwait(false);
        }

        public abstract ValueTask WriteSetBeginAsync(TSet set, CancellationToken cancellationToken);

        public virtual async ValueTask WriteSetEndAsync()
        {
            await WriteSetEndAsync(CancellationToken.None).ConfigureAwait(false);
        }

        public abstract ValueTask WriteSetEndAsync(CancellationToken cancellationToken);

        public virtual async ValueTask WriteBoolAsync(bool b)
        {
            await WriteBoolAsync(b, CancellationToken.None).ConfigureAwait(false);
        }

        public abstract ValueTask WriteBoolAsync(bool b, CancellationToken cancellationToken);

        public virtual async ValueTask WriteByteAsync(sbyte b)
        {
            await WriteByteAsync(b, CancellationToken.None).ConfigureAwait(false);
        }

        public abstract ValueTask WriteByteAsync(sbyte b, CancellationToken cancellationToken);

        public virtual async ValueTask WriteI16Async(short i16)
        {
            await WriteI16Async(i16, CancellationToken.None).ConfigureAwait(false);
        }

        public abstract ValueTask WriteI16Async(short i16, CancellationToken cancellationToken);

        public virtual async ValueTask WriteI32Async(int i32)
        {
            await WriteI32Async(i32, CancellationToken.None).ConfigureAwait(false);
        }

        public abstract ValueTask WriteI32Async(int i32, CancellationToken cancellationToken);

        public virtual async ValueTask WriteI64Async(long i64)
        {
            await WriteI64Async(i64, CancellationToken.None).ConfigureAwait(false);
        }

        public abstract ValueTask WriteI64Async(long i64, CancellationToken cancellationToken);

        public virtual async ValueTask WriteDoubleAsync(double d)
        {
            await WriteDoubleAsync(d, CancellationToken.None).ConfigureAwait(false);
        }

        public abstract ValueTask WriteDoubleAsync(double d, CancellationToken cancellationToken);

        public virtual async ValueTask WriteStringAsync(string s)
        {
            await WriteStringAsync(s, CancellationToken.None).ConfigureAwait(false);
        }

#if NETSTANDARD2_1
        public virtual async ValueTask WriteStringAsync(string s, CancellationToken cancellationToken)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(Encoding.UTF8.GetByteCount(s));
            var numberOfBytes = Encoding.UTF8.GetBytes(s, buffer);
            await WriteBinaryAsync(buffer, 0, numberOfBytes, cancellationToken).ConfigureAwait(false);
            ArrayPool<byte>.Shared.Return(buffer);
        }
#else
        public virtual async ValueTask WriteStringAsync(string s, CancellationToken cancellationToken)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(Encoding.UTF8.GetByteCount(s));
            var numberOfBytes = Encoding.UTF8.GetBytes(s, 0, s.Length, buffer, 0);
            await WriteBinaryAsync(buffer, 0, numberOfBytes, cancellationToken).ConfigureAwait(false);
            ArrayPool<byte>.Shared.Return(buffer);
        }
#endif

        public virtual async ValueTask WriteBinaryAsync(byte[] bytes)
        {
            await WriteBinaryAsync(bytes, CancellationToken.None).ConfigureAwait(false);
        }

        public virtual async ValueTask WriteBinaryAsync(byte[] bytes, CancellationToken cancellationToken)
        {
            await WriteBinaryAsync(bytes, 0, bytes.Length, cancellationToken).ConfigureAwait(false);
        }

        public virtual async ValueTask WriteBinaryAsync(byte[] buffer, int offset, int count)
        {
            await WriteBinaryAsync(buffer, offset, count, CancellationToken.None).ConfigureAwait(false);
        }

        public abstract ValueTask WriteBinaryAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken);

        public virtual async ValueTask WriteRawAsync(ArraySegment<byte> bytes)
        {
            await WriteRawAsync(bytes, CancellationToken.None).ConfigureAwait(false);
        }

        public virtual async ValueTask WriteRawAsync(ArraySegment<byte> bytes, CancellationToken cancellationToken)
        {
            await Trans.WriteAsync(bytes.Array, bytes.Offset, bytes.Count, cancellationToken).ConfigureAwait(false);
        }

        public virtual async ValueTask WriteRawAsync(byte[] bytes)
        {
            await WriteRawAsync(bytes, CancellationToken.None).ConfigureAwait(false);
        }

        public virtual async ValueTask WriteRawAsync(byte[] bytes, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            await Trans.WriteAsync(bytes, 0, bytes.Length, cancellationToken).ConfigureAwait(false);
        }

        public virtual async ValueTask<TMessage> ReadMessageBeginAsync()
        {
            return await ReadMessageBeginAsync(CancellationToken.None).ConfigureAwait(false);
        }

        public abstract ValueTask<TMessage> ReadMessageBeginAsync(CancellationToken cancellationToken);

        public virtual async ValueTask ReadMessageEndAsync()
        {
            await ReadMessageEndAsync(CancellationToken.None).ConfigureAwait(false);
        }

        public abstract ValueTask ReadMessageEndAsync(CancellationToken cancellationToken);

        public virtual async ValueTask<TStruct> ReadStructBeginAsync()
        {
            return await ReadStructBeginAsync(CancellationToken.None).ConfigureAwait(false);
        }

        public abstract ValueTask<TStruct> ReadStructBeginAsync(CancellationToken cancellationToken);

        public virtual async ValueTask ReadStructEndAsync()
        {
            await ReadStructEndAsync(CancellationToken.None).ConfigureAwait(false);
        }

        public abstract ValueTask ReadStructEndAsync(CancellationToken cancellationToken);

        public virtual async ValueTask<TField> ReadFieldBeginAsync()
        {
            return await ReadFieldBeginAsync(CancellationToken.None).ConfigureAwait(false);
        }

        public abstract ValueTask<TField> ReadFieldBeginAsync(CancellationToken cancellationToken);

        public virtual async ValueTask ReadFieldEndAsync()
        {
            await ReadFieldEndAsync(CancellationToken.None).ConfigureAwait(false);
        }

        public abstract ValueTask ReadFieldEndAsync(CancellationToken cancellationToken);

        public virtual async ValueTask<TMap> ReadMapBeginAsync()
        {
            return await ReadMapBeginAsync(CancellationToken.None).ConfigureAwait(false);
        }

        public abstract ValueTask<TMap> ReadMapBeginAsync(CancellationToken cancellationToken);

        public virtual async ValueTask ReadMapEndAsync()
        {
            await ReadMapEndAsync(CancellationToken.None).ConfigureAwait(false);
        }

        public abstract ValueTask ReadMapEndAsync(CancellationToken cancellationToken);

        public virtual async ValueTask<TList> ReadListBeginAsync()
        {
            return await ReadListBeginAsync(CancellationToken.None).ConfigureAwait(false);
        }

        public abstract ValueTask<TList> ReadListBeginAsync(CancellationToken cancellationToken);

        public virtual async ValueTask ReadListEndAsync()
        {
            await ReadListEndAsync(CancellationToken.None).ConfigureAwait(false);
        }

        public abstract ValueTask ReadListEndAsync(CancellationToken cancellationToken);

        public virtual async ValueTask<TSet> ReadSetBeginAsync()
        {
            return await ReadSetBeginAsync(CancellationToken.None).ConfigureAwait(false);
        }

        public abstract ValueTask<TSet> ReadSetBeginAsync(CancellationToken cancellationToken);

        public virtual async ValueTask ReadSetEndAsync()
        {
            await ReadSetEndAsync(CancellationToken.None).ConfigureAwait(false);
        }

        public abstract ValueTask ReadSetEndAsync(CancellationToken cancellationToken);

        public virtual async ValueTask<bool> ReadBoolAsync()
        {
            return await ReadBoolAsync(CancellationToken.None).ConfigureAwait(false);
        }

        public abstract ValueTask<bool> ReadBoolAsync(CancellationToken cancellationToken);

        public virtual async ValueTask<sbyte> ReadByteAsync()
        {
            return await ReadByteAsync(CancellationToken.None).ConfigureAwait(false);
        }

        public abstract ValueTask<sbyte> ReadByteAsync(CancellationToken cancellationToken);

        public virtual async ValueTask<short> ReadI16Async()
        {
            return await ReadI16Async(CancellationToken.None).ConfigureAwait(false);
        }

        public abstract ValueTask<short> ReadI16Async(CancellationToken cancellationToken);

        public virtual async ValueTask<int> ReadI32Async()
        {
            return await ReadI32Async(CancellationToken.None).ConfigureAwait(false);
        }

        public abstract ValueTask<int> ReadI32Async(CancellationToken cancellationToken);

        public virtual async ValueTask<long> ReadI64Async()
        {
            return await ReadI64Async(CancellationToken.None).ConfigureAwait(false);
        }

        public abstract ValueTask<long> ReadI64Async(CancellationToken cancellationToken);

        public virtual async ValueTask<double> ReadDoubleAsync()
        {
            return await ReadDoubleAsync(CancellationToken.None).ConfigureAwait(false);
        }

        public abstract ValueTask<double> ReadDoubleAsync(CancellationToken cancellationToken);

        public virtual async ValueTask<string> ReadStringAsync()
        {
            return await ReadStringAsync(CancellationToken.None).ConfigureAwait(false);
        }

        public virtual async ValueTask<string> ReadStringAsync(CancellationToken cancellationToken)
        {
            var buf = await ReadBinaryAsync(cancellationToken).ConfigureAwait(false);
            return Encoding.UTF8.GetString(buf, 0, buf.Length);
        }

        public virtual async ValueTask<byte[]> ReadBinaryAsync()
        {
            return await ReadBinaryAsync(CancellationToken.None).ConfigureAwait(false);
        }

        public abstract ValueTask<byte[]> ReadBinaryAsync(CancellationToken cancellationToken);
    }
}

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
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Thrift.Protocols.Entities;
using Thrift.Transports;

namespace Thrift.Protocols
{
    //TODO: implementation of TProtocol

    // ReSharper disable once InconsistentNaming
    public class TCompactProtocol : TProtocol
    {
        private const byte ProtocolId = 0x82;
        private const byte Version = 1;
        private const byte VersionMask = 0x1f; // 0001 1111
        private const byte TypeMask = 0xE0; // 1110 0000
        private const byte TypeBits = 0x07; // 0000 0111
        private const int TypeShiftAmount = 5;
        private static readonly TStruct AnonymousStruct = new TStruct(string.Empty);
        private static readonly TField Tstop = new TField(string.Empty, TType.Stop, 0);
        private static readonly byte[] ProtocolIdBuffer = new byte[] { ProtocolId };
        private static readonly byte[] ZeroBuffer = new byte[] { 0 };
        private static readonly byte[] FieldStopBuffer = new byte[] { Types.Stop };
        private static readonly byte[] TrueBuffer = new byte[] { Types.BooleanTrue };
        private static readonly byte[] FalseBuffer = new byte[] { Types.BooleanFalse };

        // ReSharper disable once InconsistentNaming
        private static readonly byte[] TTypeToCompactType = new byte[16];

        /// <summary>
        ///     Used to keep track of the last field for the current and previous structs, so we can do the delta stuff.
        /// </summary>
        private readonly Stack<short> _lastField = new Stack<short>(15);

        /// <summary>
        ///     If we encounter a boolean field begin, save the TField here so it can have the value incorporated.
        /// </summary>
        private TField? _booleanField;

        /// <summary>
        ///     If we Read a field header, and it's a boolean field, save the boolean value here so that ReadBool can use it.
        /// </summary>
        private bool? _boolValue;

        private short _lastFieldId;

        public TCompactProtocol(TClientTransport trans)
            : base(trans)
        {
            TTypeToCompactType[(int)TType.Stop] = Types.Stop;
            TTypeToCompactType[(int)TType.Bool] = Types.BooleanTrue;
            TTypeToCompactType[(int)TType.Byte] = Types.Byte;
            TTypeToCompactType[(int)TType.I16] = Types.I16;
            TTypeToCompactType[(int)TType.I32] = Types.I32;
            TTypeToCompactType[(int)TType.I64] = Types.I64;
            TTypeToCompactType[(int)TType.Double] = Types.Double;
            TTypeToCompactType[(int)TType.String] = Types.Binary;
            TTypeToCompactType[(int)TType.List] = Types.List;
            TTypeToCompactType[(int)TType.Set] = Types.Set;
            TTypeToCompactType[(int)TType.Map] = Types.Map;
            TTypeToCompactType[(int)TType.Struct] = Types.Struct;
        }

        public void Reset()
        {
            _lastField.Clear();
            _lastFieldId = 0;
        }

        public override async ValueTask WriteMessageBeginAsync(TMessage message, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            await Trans.WriteAsync(ProtocolIdBuffer, cancellationToken).ConfigureAwait(false);

            byte[] messageTypeAndVersion = ArrayPool<byte>.Shared.Rent(1);
            messageTypeAndVersion[0] = (byte)((Version & VersionMask) | (((uint)message.Type << TypeShiftAmount) & TypeMask));
            await Trans.WriteAsync(messageTypeAndVersion, 0, 1, cancellationToken).ConfigureAwait(false);
            ArrayPool<byte>.Shared.Return(messageTypeAndVersion);

            var bufferTuple = CreateWriteVarInt32((uint)message.SeqID);
            await Trans.WriteAsync(bufferTuple.Item1, 0, bufferTuple.Item2, cancellationToken).ConfigureAwait(false);
            ArrayPool<byte>.Shared.Return(bufferTuple.Item1);

            await WriteStringAsync(message.Name, cancellationToken).ConfigureAwait(false);
        }

        public override async ValueTask WriteMessageEndAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                await Task.FromCanceled(cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Write a struct begin. This doesn't actually put anything on the wire. We
        ///     use it as an opportunity to put special placeholder markers on the field
        ///     stack so we can get the field id deltas correct.
        /// </summary>
        public override async ValueTask WriteStructBeginAsync(TStruct @struct, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                await Task.FromCanceled(cancellationToken).ConfigureAwait(false);
            }

            _lastField.Push(_lastFieldId);
            _lastFieldId = 0;
        }

        public override async ValueTask WriteStructEndAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                await Task.FromCanceled(cancellationToken).ConfigureAwait(false);
            }

            _lastFieldId = _lastField.Pop();
        }

        private async ValueTask WriteFieldBeginInternalAsync(TField field, byte typeOverride, CancellationToken cancellationToken)
        {
            // if there's a exType override, use that.
            var typeToWrite = typeOverride == 0xFF ? GetCompactType(field.Type) : typeOverride;

            var typeBuffer = ArrayPool<byte>.Shared.Rent(1);
            // check if we can use delta encoding for the field id
            if ((field.ID > _lastFieldId) && (field.ID - _lastFieldId <= 15))
            {
                typeBuffer[0] = (byte)(((field.ID - _lastFieldId) << 4) | typeToWrite);
                // Write them together
                await Trans.WriteAsync(typeBuffer, 0, 1, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                typeBuffer[0] = typeToWrite;
                // Write them separate
                await Trans.WriteAsync(typeBuffer, 0, 1, cancellationToken).ConfigureAwait(false);
                await WriteI16Async(field.ID, cancellationToken).ConfigureAwait(false);
            }
            ArrayPool<byte>.Shared.Return(typeBuffer);

            _lastFieldId = field.ID;
        }

        public override async ValueTask WriteFieldBeginAsync(TField field, CancellationToken cancellationToken)
        {
            if (field.Type == TType.Bool)
            {
                _booleanField = field;
            }
            else
            {
                await WriteFieldBeginInternalAsync(field, 0xFF, cancellationToken).ConfigureAwait(false);
            }
        }

        public override async ValueTask WriteFieldEndAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                await Task.FromCanceled(cancellationToken).ConfigureAwait(false);
            }
        }

        public override async ValueTask WriteFieldStopAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            await Trans.WriteAsync(FieldStopBuffer, cancellationToken).ConfigureAwait(false);
        }

        protected async ValueTask WriteCollectionBeginAsync(TType elemType, int size, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            /*
            Abstract method for writing the start of lists and sets. List and sets on
             the wire differ only by the exType indicator.
            */

            var elementTypeBuffer = ArrayPool<byte>.Shared.Rent(1);
            if (size <= 14)
            {
                elementTypeBuffer[0] = (byte)((size << 4) | GetCompactType(elemType));
                await Trans.WriteAsync(elementTypeBuffer, 0, 1, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                elementTypeBuffer[0] = (byte)(0xf0 | GetCompactType(elemType));
                await Trans.WriteAsync(elementTypeBuffer, 0, 1, cancellationToken).ConfigureAwait(false);

                var bufferTuple = CreateWriteVarInt32((uint)size);
                await Trans.WriteAsync(bufferTuple.Item1, 0, bufferTuple.Item2, cancellationToken).ConfigureAwait(false);
                ArrayPool<byte>.Shared.Return(bufferTuple.Item1);
            }
            ArrayPool<byte>.Shared.Return(elementTypeBuffer);
        }

        public override async ValueTask WriteListBeginAsync(TList list, CancellationToken cancellationToken)
        {
            await WriteCollectionBeginAsync(list.ElementType, list.Count, cancellationToken).ConfigureAwait(false);
        }

        public override async ValueTask WriteListEndAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                await Task.FromCanceled(cancellationToken).ConfigureAwait(false);
            }
        }

        public override async ValueTask WriteSetBeginAsync(TSet set, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            await WriteCollectionBeginAsync(set.ElementType, set.Count, cancellationToken).ConfigureAwait(false);
        }

        public override async ValueTask WriteSetEndAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                await Task.FromCanceled(cancellationToken).ConfigureAwait(false);
            }
        }

        public override async ValueTask WriteBoolAsync(bool b, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            /*
            Write a boolean value. Potentially, this could be a boolean field, in
            which case the field header info isn't written yet. If so, decide what the
            right exType header is for the value and then Write the field header.
            Otherwise, Write a single byte.
            */

            if (_booleanField != null)
            {
                // we haven't written the field header yet
                await
                    WriteFieldBeginInternalAsync(_booleanField.Value, b ? Types.BooleanTrue : Types.BooleanFalse,
                        cancellationToken).ConfigureAwait(false);
                _booleanField = null;
            }
            else
            {
                // we're not part of a field, so just Write the value.
                await Trans.WriteAsync(b ? TrueBuffer : FalseBuffer, cancellationToken).ConfigureAwait(false);
            }
        }

        public override async ValueTask WriteByteAsync(sbyte b, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            byte[] buffer = ArrayPool<byte>.Shared.Rent(1);
            buffer[0] = (byte)b;
            await Trans.WriteAsync(buffer, 0, 1, cancellationToken).ConfigureAwait(false);
            ArrayPool<byte>.Shared.Return(buffer);
        }

        public override async ValueTask WriteI16Async(short i16, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var bufferTuple = CreateWriteVarInt32(IntToZigzag(i16));
            await Trans.WriteAsync(bufferTuple.Item1, 0, bufferTuple.Item2, cancellationToken).ConfigureAwait(false);
            ArrayPool<byte>.Shared.Return(bufferTuple.Item1);
        }

        protected internal Tuple<byte[], int> CreateWriteVarInt32(uint n)
        {
            // Write an i32 as a varint.Results in 1 - 5 bytes on the wire.
            var i32Buf = ArrayPool<byte>.Shared.Rent(5);
            var idx = 0;

            while (true)
            {
                if ((n & ~0x7F) == 0)
                {
                    i32Buf[idx++] = (byte)n;
                    break;
                }

                i32Buf[idx++] = (byte)((n & 0x7F) | 0x80);
                n >>= 7;
            }

            return new Tuple<byte[], int>(i32Buf, idx);
        }

        public override async ValueTask WriteI32Async(int i32, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var bufferTuple = CreateWriteVarInt32(IntToZigzag(i32));
            await Trans.WriteAsync(bufferTuple.Item1, 0, bufferTuple.Item2, cancellationToken).ConfigureAwait(false);
            ArrayPool<byte>.Shared.Return(bufferTuple.Item1);
        }

        protected internal Tuple<byte[], int> CreateWriteVarInt64(ulong n)
        {
            // Write an i64 as a varint. Results in 1-10 bytes on the wire.
            var buf = ArrayPool<byte>.Shared.Rent(10);
            var idx = 0;

            while (true)
            {
                if ((n & ~(ulong)0x7FL) == 0)
                {
                    buf[idx++] = (byte)n;
                    break;
                }
                buf[idx++] = (byte)((n & 0x7F) | 0x80);
                n >>= 7;
            }

            return new Tuple<byte[], int>(buf, idx);
        }

        public override async ValueTask WriteI64Async(long i64, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var bufferTuple = CreateWriteVarInt64(LongToZigzag(i64));
            await Trans.WriteAsync(bufferTuple.Item1, 0, bufferTuple.Item2, cancellationToken).ConfigureAwait(false);
            ArrayPool<byte>.Shared.Return(bufferTuple.Item1);
        }

        public override async ValueTask WriteDoubleAsync(double d, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var data = ArrayPool<byte>.Shared.Rent(8);
            FixedLongToBytes(BitConverter.DoubleToInt64Bits(d), data, 0);
            await Trans.WriteAsync(data, 0, 8, cancellationToken).ConfigureAwait(false);
            ArrayPool<byte>.Shared.Return(data);
        }

        public override async ValueTask WriteStringAsync(string str, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var bytes = ArrayPool<byte>.Shared.Rent(Encoding.UTF8.GetByteCount(str));
#if NETSTANDARD2_1
            var numberOfBytes = Encoding.UTF8.GetBytes(str, bytes);
#else
            var numberOfBytes = Encoding.UTF8.GetBytes(str, 0, str.Length, bytes, 0);
#endif

            var bufferTuple = CreateWriteVarInt32((uint)numberOfBytes);
            await Trans.WriteAsync(bufferTuple.Item1, 0, bufferTuple.Item2, cancellationToken).ConfigureAwait(false);
            ArrayPool<byte>.Shared.Return(bufferTuple.Item1);

            await Trans.WriteAsync(bytes, 0, numberOfBytes, cancellationToken).ConfigureAwait(false);
            ArrayPool<byte>.Shared.Return(bytes);
        }

        public override async ValueTask WriteBinaryAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var bufferTuple = CreateWriteVarInt32((uint)count);
            await Trans.WriteAsync(bufferTuple.Item1, 0, bufferTuple.Item2, cancellationToken).ConfigureAwait(false);
            ArrayPool<byte>.Shared.Return(bufferTuple.Item1);

            await Trans.WriteAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
        }

        public override async ValueTask WriteMapBeginAsync(TMap map, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (map.Count == 0)
            {
                await Trans.WriteAsync(ZeroBuffer, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                var bufferTuple = CreateWriteVarInt32((uint)map.Count);
                await Trans.WriteAsync(bufferTuple.Item1, 0, bufferTuple.Item2, cancellationToken).ConfigureAwait(false);
                ArrayPool<byte>.Shared.Return(bufferTuple.Item1);

                var mapTypesBuffer = ArrayPool<byte>.Shared.Rent(1);
                mapTypesBuffer[0] = (byte)((GetCompactType(map.KeyType) << 4) | GetCompactType(map.ValueType));
                await Trans.WriteAsync(mapTypesBuffer, 0, 1, cancellationToken).ConfigureAwait(false);
                ArrayPool<byte>.Shared.Return(mapTypesBuffer);
            }
        }

        public override async ValueTask WriteMapEndAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                await Task.FromCanceled(cancellationToken).ConfigureAwait(false);
            }
        }

        public override async ValueTask<TMessage> ReadMessageBeginAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return await Task.FromCanceled<TMessage>(cancellationToken).ConfigureAwait(false);
            }

            var protocolId = (byte)await ReadByteAsync(cancellationToken).ConfigureAwait(false);
            if (protocolId != ProtocolId)
            {
                throw new TProtocolException($"Expected protocol id {ProtocolId:X} but got {protocolId:X}");
            }

            var versionAndType = (byte)await ReadByteAsync(cancellationToken).ConfigureAwait(false);
            var version = (byte)(versionAndType & VersionMask);

            if (version != Version)
            {
                throw new TProtocolException($"Expected version {Version} but got {version}");
            }

            var type = (byte)((versionAndType >> TypeShiftAmount) & TypeBits);
            var seqid = (int)await ReadVarInt32Async(cancellationToken).ConfigureAwait(false);
            var messageName = await ReadStringAsync(cancellationToken).ConfigureAwait(false);

            return new TMessage(messageName, (TMessageType)type, seqid);
        }

        public override async ValueTask ReadMessageEndAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                await Task.FromCanceled(cancellationToken).ConfigureAwait(false);
            }
        }

        public override async ValueTask<TStruct> ReadStructBeginAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return await Task.FromCanceled<TStruct>(cancellationToken).ConfigureAwait(false);
            }

            // some magic is here )

            _lastField.Push(_lastFieldId);
            _lastFieldId = 0;

            return AnonymousStruct;
        }

        public override async ValueTask ReadStructEndAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                await Task.FromCanceled(cancellationToken).ConfigureAwait(false);
            }

            /*
            Doesn't actually consume any wire data, just removes the last field for
            this struct from the field stack.
            */

            // consume the last field we Read off the wire.
            _lastFieldId = _lastField.Pop();
        }

        public override async ValueTask<TField> ReadFieldBeginAsync(CancellationToken cancellationToken)
        {
            // Read a field header off the wire.
            var type = (byte)await ReadByteAsync(cancellationToken).ConfigureAwait(false);
            // if it's a stop, then we can return immediately, as the struct is over.
            if (type == Types.Stop)
            {
                return Tstop;
            }

            short fieldId;
            // mask off the 4 MSB of the exType header. it could contain a field id delta.
            var modifier = (short)((type & 0xf0) >> 4);
            if (modifier == 0)
            {
                fieldId = await ReadI16Async(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                fieldId = (short)(_lastFieldId + modifier);
            }

            var field = new TField(string.Empty, GetTType((byte)(type & 0x0f)), fieldId);
            // if this happens to be a boolean field, the value is encoded in the exType
            if (IsBoolType(type))
            {
                _boolValue = (byte)(type & 0x0f) == Types.BooleanTrue;
            }

            // push the new field onto the field stack so we can keep the deltas going.
            _lastFieldId = field.ID;
            return field;
        }

        public override async ValueTask ReadFieldEndAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                await Task.FromCanceled(cancellationToken).ConfigureAwait(false);
            }
        }

        public override async ValueTask<TMap> ReadMapBeginAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                await Task.FromCanceled<TMap>(cancellationToken).ConfigureAwait(false);
            }

            /*
            Read a map header off the wire. If the size is zero, skip Reading the key
            and value exType. This means that 0-length maps will yield TMaps without the
            "correct" types.
            */

            var size = (int)await ReadVarInt32Async(cancellationToken).ConfigureAwait(false);
            var keyAndValueType = size == 0 ? (byte)0 : (byte)await ReadByteAsync(cancellationToken).ConfigureAwait(false);
            return new TMap(GetTType((byte)(keyAndValueType >> 4)), GetTType((byte)(keyAndValueType & 0xf)), size);
        }

        public override async ValueTask ReadMapEndAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                await Task.FromCanceled(cancellationToken).ConfigureAwait(false);
            }
        }

        public override async ValueTask<TSet> ReadSetBeginAsync(CancellationToken cancellationToken)
        {
            /*
            Read a set header off the wire. If the set size is 0-14, the size will
            be packed into the element exType header. If it's a longer set, the 4 MSB
            of the element exType header will be 0xF, and a varint will follow with the
            true size.
            */

            return new TSet(await ReadListBeginAsync(cancellationToken).ConfigureAwait(false));
        }

        public override async ValueTask<bool> ReadBoolAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return await Task.FromCanceled<bool>(cancellationToken).ConfigureAwait(false);
            }

            /*
            Read a boolean off the wire. If this is a boolean field, the value should
            already have been Read during ReadFieldBegin, so we'll just consume the
            pre-stored value. Otherwise, Read a byte.
            */

            if (_boolValue != null)
            {
                var result = _boolValue.Value;
                _boolValue = null;
                return result;
            }

            return (await ReadByteAsync(cancellationToken).ConfigureAwait(false)) == Types.BooleanTrue;
        }

        public override async ValueTask<sbyte> ReadByteAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return await Task.FromCanceled<sbyte>(cancellationToken).ConfigureAwait(false);
            }

            // Read a single byte off the wire. Nothing interesting here.
            var buf = new byte[1];
            await Trans.ReadAllAsync(buf, 0, 1, cancellationToken).ConfigureAwait(false);
            return (sbyte)buf[0];
        }

        public override async ValueTask<short> ReadI16Async(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return await Task.FromCanceled<short>(cancellationToken).ConfigureAwait(false);
            }

            return (short)ZigzagToInt(await ReadVarInt32Async(cancellationToken).ConfigureAwait(false));
        }

        public override async ValueTask<int> ReadI32Async(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return await Task.FromCanceled<int>(cancellationToken).ConfigureAwait(false);
            }

            return ZigzagToInt(await ReadVarInt32Async(cancellationToken).ConfigureAwait(false));
        }

        public override async ValueTask<long> ReadI64Async(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return await Task.FromCanceled<long>(cancellationToken).ConfigureAwait(false);
            }

            return ZigzagToLong(await ReadVarInt64Async(cancellationToken).ConfigureAwait(false));
        }

        public override async ValueTask<double> ReadDoubleAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return await Task.FromCanceled<double>(cancellationToken).ConfigureAwait(false);
            }

            var longBits = new byte[8];
            await Trans.ReadAllAsync(longBits, 0, 8, cancellationToken).ConfigureAwait(false);

            return BitConverter.Int64BitsToDouble(BytesToLong(longBits));
        }

        public override async ValueTask<string> ReadStringAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                await Task.FromCanceled<string>(cancellationToken).ConfigureAwait(false);
            }

            // Reads a byte[] (via ReadBinary), and then UTF-8 decodes it.
            var length = (int)await ReadVarInt32Async(cancellationToken).ConfigureAwait(false);

            if (length == 0)
            {
                return string.Empty;
            }

            var buf = new byte[length];
            await Trans.ReadAllAsync(buf, 0, length, cancellationToken).ConfigureAwait(false);

            return Encoding.UTF8.GetString(buf);
        }

        public override async ValueTask<byte[]> ReadBinaryAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return await Task.FromCanceled<byte[]>(cancellationToken).ConfigureAwait(false);
            }

            // Read a byte[] from the wire.
            var length = (int)await ReadVarInt32Async(cancellationToken).ConfigureAwait(false);
            if (length == 0)
            {
                return new byte[0];
            }

            var buf = new byte[length];
            await Trans.ReadAllAsync(buf, 0, length, cancellationToken).ConfigureAwait(false);
            return buf;
        }

        public override async ValueTask<TList> ReadListBeginAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                await Task.FromCanceled<TList>(cancellationToken).ConfigureAwait(false);
            }

            /*
            Read a list header off the wire. If the list size is 0-14, the size will
            be packed into the element exType header. If it's a longer list, the 4 MSB
            of the element exType header will be 0xF, and a varint will follow with the
            true size.
            */

            var sizeAndType = (byte)await ReadByteAsync(cancellationToken).ConfigureAwait(false);
            var size = (sizeAndType >> 4) & 0x0f;
            if (size == 15)
            {
                size = (int)await ReadVarInt32Async(cancellationToken).ConfigureAwait(false);
            }

            var type = GetTType(sizeAndType);
            return new TList(type, size);
        }

        public override async ValueTask ReadListEndAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                await Task.FromCanceled(cancellationToken).ConfigureAwait(false);
            }
        }

        public override async ValueTask ReadSetEndAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                await Task.FromCanceled(cancellationToken).ConfigureAwait(false);
            }
        }

        private static byte GetCompactType(TType ttype)
        {
            // Given a TType value, find the appropriate TCompactProtocol.Types constant.
            return TTypeToCompactType[(int)ttype];
        }

        private async ValueTask<uint> ReadVarInt32Async(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return await Task.FromCanceled<uint>(cancellationToken).ConfigureAwait(false);
            }

            /*
            Read an i32 from the wire as a varint. The MSB of each byte is set
            if there is another byte to follow. This can Read up to 5 bytes.
            */

            uint result = 0;
            var shift = 0;

            while (true)
            {
                var b = (byte)await ReadByteAsync(cancellationToken).ConfigureAwait(false);
                result |= (uint)(b & 0x7f) << shift;
                if ((b & 0x80) != 0x80)
                {
                    break;
                }
                shift += 7;
            }

            return result;
        }

        private async ValueTask<ulong> ReadVarInt64Async(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return await Task.FromCanceled<uint>(cancellationToken).ConfigureAwait(false);
            }

            /*
            Read an i64 from the wire as a proper varint. The MSB of each byte is set
            if there is another byte to follow. This can Read up to 10 bytes.
            */

            var shift = 0;
            ulong result = 0;
            while (true)
            {
                var b = (byte)await ReadByteAsync(cancellationToken).ConfigureAwait(false);
                result |= (ulong)(b & 0x7f) << shift;
                if ((b & 0x80) != 0x80)
                {
                    break;
                }
                shift += 7;
            }

            return result;
        }

        private static int ZigzagToInt(uint n)
        {
            return (int)(n >> 1) ^ -(int)(n & 1);
        }

        private static long ZigzagToLong(ulong n)
        {
            return (long)(n >> 1) ^ -(long)(n & 1);
        }

        private static long BytesToLong(byte[] bytes)
        {
            /*
            Note that it's important that the mask bytes are long literals,
            otherwise they'll default to ints, and when you shift an int left 56 bits,
            you just get a messed up int.
            */

            return
                ((bytes[7] & 0xffL) << 56) |
                ((bytes[6] & 0xffL) << 48) |
                ((bytes[5] & 0xffL) << 40) |
                ((bytes[4] & 0xffL) << 32) |
                ((bytes[3] & 0xffL) << 24) |
                ((bytes[2] & 0xffL) << 16) |
                ((bytes[1] & 0xffL) << 8) |
                (bytes[0] & 0xffL);
        }

        private static bool IsBoolType(byte b)
        {
            var lowerNibble = b & 0x0f;
            return (lowerNibble == Types.BooleanTrue) || (lowerNibble == Types.BooleanFalse);
        }

        private static TType GetTType(byte type)
        {
            // Given a TCompactProtocol.Types constant, convert it to its corresponding TType value.
            switch ((byte)(type & 0x0f))
            {
                case Types.Stop:
                    return TType.Stop;
                case Types.BooleanFalse:
                case Types.BooleanTrue:
                    return TType.Bool;
                case Types.Byte:
                    return TType.Byte;
                case Types.I16:
                    return TType.I16;
                case Types.I32:
                    return TType.I32;
                case Types.I64:
                    return TType.I64;
                case Types.Double:
                    return TType.Double;
                case Types.Binary:
                    return TType.String;
                case Types.List:
                    return TType.List;
                case Types.Set:
                    return TType.Set;
                case Types.Map:
                    return TType.Map;
                case Types.Struct:
                    return TType.Struct;
                default:
                    throw new TProtocolException($"Don't know what exType: {(byte)(type & 0x0f)}");
            }
        }

        private static ulong LongToZigzag(long n)
        {
            // Convert l into a zigzag long. This allows negative numbers to be represented compactly as a varint
            return (ulong)(n << 1) ^ (ulong)(n >> 63);
        }

        private static uint IntToZigzag(int n)
        {
            // Convert n into a zigzag int. This allows negative numbers to be represented compactly as a varint
            return (uint)(n << 1) ^ (uint)(n >> 31);
        }

        private static void FixedLongToBytes(long n, byte[] buf, int off)
        {
            // Convert a long into little-endian bytes in buf starting at off and going until off+7.
            buf[off + 0] = (byte)(n & 0xff);
            buf[off + 1] = (byte)((n >> 8) & 0xff);
            buf[off + 2] = (byte)((n >> 16) & 0xff);
            buf[off + 3] = (byte)((n >> 24) & 0xff);
            buf[off + 4] = (byte)((n >> 32) & 0xff);
            buf[off + 5] = (byte)((n >> 40) & 0xff);
            buf[off + 6] = (byte)((n >> 48) & 0xff);
            buf[off + 7] = (byte)((n >> 56) & 0xff);
        }

        public class Factory : ITProtocolFactory
        {
            public TProtocol GetProtocol(TClientTransport trans)
            {
                return new TCompactProtocol(trans);
            }
        }

        /// <summary>
        ///     All of the on-wire exType codes.
        /// </summary>
        private static class Types
        {
            public const byte Stop = 0x00;
            public const byte BooleanTrue = 0x01;
            public const byte BooleanFalse = 0x02;
            public const byte Byte = 0x03;
            public const byte I16 = 0x04;
            public const byte I32 = 0x05;
            public const byte I64 = 0x06;
            public const byte Double = 0x07;
            public const byte Binary = 0x08;
            public const byte List = 0x09;
            public const byte Set = 0x0A;
            public const byte Map = 0x0B;
            public const byte Struct = 0x0C;
        }
    }
}

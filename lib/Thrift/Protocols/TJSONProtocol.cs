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
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Thrift.Protocols.Entities;
using Thrift.Protocols.Utilities;
using Thrift.Transports;

namespace Thrift.Protocols
{
    /// <summary>
    ///     JSON protocol implementation for thrift.
    ///     This is a full-featured protocol supporting Write and Read.
    ///     Please see the C++ class header for a detailed description of the
    ///     protocol's wire format.
    ///     Adapted from the Java version.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public class TJsonProtocol : TProtocol
    {
        private const long Version = 1;

        // Temporary buffer used by several methods
        private readonly byte[] _tempBuffer = new byte[4];

        // Current context that we are in
        protected JSONBaseContext Context;

        // Stack of nested contexts that we may be in
        protected Stack<JSONBaseContext> ContextStack = new Stack<JSONBaseContext>();

        // Reader that manages a 1-byte buffer
        protected LookaheadReader Reader;

        // Default encoding
        protected Encoding Utf8Encoding = Encoding.UTF8;

        /// <summary>
        ///     TJsonProtocol Constructor
        /// </summary>
        public TJsonProtocol(TClientTransport trans)
            : base(trans)
        {
            Context = new JSONBaseContext(this);
            Reader = new LookaheadReader(this);
        }

        /// <summary>
        ///     Push a new JSON context onto the stack.
        /// </summary>
        protected void PushContext(JSONBaseContext c)
        {
            ContextStack.Push(Context);
            Context = c;
        }

        /// <summary>
        ///     Pop the last JSON context off the stack
        /// </summary>
        protected void PopContext()
        {
            Context = ContextStack.Pop();
        }

        /// <summary>
        ///     Read a byte that must match b[0]; otherwise an exception is thrown.
        ///     Marked protected to avoid synthetic accessor in JSONListContext.Read
        ///     and JSONPairContext.Read
        /// </summary>
#if NETSTANDARD2_1
        protected async ValueTask ReadJsonSyntaxCharAsync(byte[] bytes, CancellationToken cancellationToken)
#else
        protected async Task ReadJsonSyntaxCharAsync(byte[] bytes, CancellationToken cancellationToken)
#endif
        {
            var ch = await Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            if (ch != bytes[0])
            {
                throw new TProtocolException(TProtocolException.INVALID_DATA, $"Unexpected character: {(char)ch}");
            }
        }

        /// <summary>
        ///     Write the bytes in array buf as a JSON characters, escaping as needed
        /// </summary>
#if NETSTANDARD2_1
        private async ValueTask WriteJsonStringAsync(byte[] bytes, CancellationToken cancellationToken)
#else
        private async Task WriteJsonStringAsync(byte[] bytes, CancellationToken cancellationToken)
#endif
        {
            await Context.WriteAsync(cancellationToken).ConfigureAwait(false);
            await Trans.WriteAsync(TJSONProtocolConstants.Quote, cancellationToken).ConfigureAwait(false);

            var len = bytes.Length;
            for (var i = 0; i < len; i++)
            {
                if ((bytes[i] & 0x00FF) >= 0x30)
                {
                    if (bytes[i] == TJSONProtocolConstants.Backslash[0])
                    {
                        await Trans.WriteAsync(TJSONProtocolConstants.Backslash, cancellationToken).ConfigureAwait(false);
                        await Trans.WriteAsync(TJSONProtocolConstants.Backslash, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        await Trans.WriteAsync(bytes.ToArray(), i, 1, cancellationToken).ConfigureAwait(false);
                    }
                }
                else
                {
                    _tempBuffer[0] = TJSONProtocolConstants.JsonCharTable[bytes[i]];
                    if (_tempBuffer[0] == 1)
                    {
                        await Trans.WriteAsync(bytes, i, 1, cancellationToken).ConfigureAwait(false);
                    }
                    else if (_tempBuffer[0] > 1)
                    {
                        await Trans.WriteAsync(TJSONProtocolConstants.Backslash, cancellationToken).ConfigureAwait(false);
                        await Trans.WriteAsync(_tempBuffer, 0, 1, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        await Trans.WriteAsync(TJSONProtocolConstants.EscSequences, cancellationToken).ConfigureAwait(false);
                        _tempBuffer[0] = TJSONProtocolHelper.ToHexChar((byte)(bytes[i] >> 4));
                        _tempBuffer[1] = TJSONProtocolHelper.ToHexChar(bytes[i]);
                        await Trans.WriteAsync(_tempBuffer, 0, 2, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            await Trans.WriteAsync(TJSONProtocolConstants.Quote, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        ///     Write out number as a JSON value. If the context dictates so, it will be
        ///     wrapped in quotes to output as a JSON string.
        /// </summary>
#if NETSTANDARD2_1
        private async ValueTask WriteJsonIntegerAsync(long num, CancellationToken cancellationToken)
#else
        private async Task WriteJsonIntegerAsync(long num, CancellationToken cancellationToken)
#endif
        {
            await Context.WriteAsync(cancellationToken).ConfigureAwait(false);
            var str = num.ToString();

            var escapeNum = Context.EscapeNumbers();
            if (escapeNum)
            {
                await Trans.WriteAsync(TJSONProtocolConstants.Quote, cancellationToken).ConfigureAwait(false);
            }

            var bytes = Utf8Encoding.GetBytes(str);
            await Trans.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);

            if (escapeNum)
            {
                await Trans.WriteAsync(TJSONProtocolConstants.Quote, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Write out a double as a JSON value. If it is NaN or infinity or if the
        ///     context dictates escaping, Write out as JSON string.
        /// </summary>
#if NETSTANDARD2_1
        private async ValueTask WriteJsonDoubleAsync(double num, CancellationToken cancellationToken)
#else
        private async Task WriteJsonDoubleAsync(double num, CancellationToken cancellationToken)
#endif
        {
            await Context.WriteAsync(cancellationToken).ConfigureAwait(false);
            var str = num.ToString("G17", CultureInfo.InvariantCulture);
            var special = false;

            switch (str[0])
            {
                case 'N': // NaN
                case 'I': // Infinity
                    special = true;
                    break;
                case '-':
                    if (str[1] == 'I')
                    {
                        // -Infinity
                        special = true;
                    }
                    break;
            }

            var escapeNum = special || Context.EscapeNumbers();

            if (escapeNum)
            {
                await Trans.WriteAsync(TJSONProtocolConstants.Quote, cancellationToken).ConfigureAwait(false);
            }

            await Trans.WriteAsync(Utf8Encoding.GetBytes(str), cancellationToken).ConfigureAwait(false);

            if (escapeNum)
            {
                await Trans.WriteAsync(TJSONProtocolConstants.Quote, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Write out contents of byte array b as a JSON string with base-64 encoded
        ///     data
        /// </summary>
#if NETSTANDARD2_1
        private async ValueTask WriteJsonBase64Async(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
#else
        private async Task WriteJsonBase64Async(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
#endif
        {
            await Context.WriteAsync(cancellationToken).ConfigureAwait(false);
            await Trans.WriteAsync(TJSONProtocolConstants.Quote, cancellationToken).ConfigureAwait(false);

            var len = count;
            var off = offset;

            while (len >= 3)
            {
                // Encode 3 bytes at a time
                TBase64Helper.Encode(buffer, off, 3, _tempBuffer, 0);
                await Trans.WriteAsync(_tempBuffer, 0, 4, cancellationToken).ConfigureAwait(false);
                off += 3;
                len -= 3;
            }

            if (len > 0)
            {
                // Encode remainder
                TBase64Helper.Encode(buffer, off, len, _tempBuffer, 0);
                await Trans.WriteAsync(_tempBuffer, 0, len + 1, cancellationToken).ConfigureAwait(false);
            }

            await Trans.WriteAsync(TJSONProtocolConstants.Quote, cancellationToken).ConfigureAwait(false);
        }

#if NETSTANDARD2_1
        private async ValueTask WriteJsonObjectStartAsync(CancellationToken cancellationToken)
#else
        private async Task WriteJsonObjectStartAsync(CancellationToken cancellationToken)
#endif
        {
            await Context.WriteAsync(cancellationToken).ConfigureAwait(false);
            await Trans.WriteAsync(TJSONProtocolConstants.LeftBrace, cancellationToken).ConfigureAwait(false);
            PushContext(new JSONPairContext(this));
        }

#if NETSTANDARD2_1
        private async ValueTask WriteJsonObjectEndAsync(CancellationToken cancellationToken)
#else
        private async Task WriteJsonObjectEndAsync(CancellationToken cancellationToken)
#endif
        {
            PopContext();
            await Trans.WriteAsync(TJSONProtocolConstants.RightBrace, cancellationToken).ConfigureAwait(false);
        }

#if NETSTANDARD2_1
        private async ValueTask WriteJsonArrayStartAsync(CancellationToken cancellationToken)
#else
        private async Task WriteJsonArrayStartAsync(CancellationToken cancellationToken)
#endif
        {
            await Context.WriteAsync(cancellationToken).ConfigureAwait(false);
            await Trans.WriteAsync(TJSONProtocolConstants.LeftBracket, cancellationToken).ConfigureAwait(false);
            PushContext(new JSONListContext(this));
        }

#if NETSTANDARD2_1
        private async ValueTask WriteJsonArrayEndAsync(CancellationToken cancellationToken)
#else
        private async Task WriteJsonArrayEndAsync(CancellationToken cancellationToken)
#endif
        {
            PopContext();
            await Trans.WriteAsync(TJSONProtocolConstants.RightBracket, cancellationToken).ConfigureAwait(false);
        }

#if NETSTANDARD2_1
        public override async ValueTask WriteMessageBeginAsync(TMessage message, CancellationToken cancellationToken)
#else
        public override async Task WriteMessageBeginAsync(TMessage message, CancellationToken cancellationToken)
#endif
        {
            await WriteJsonArrayStartAsync(cancellationToken).ConfigureAwait(false);
            await WriteJsonIntegerAsync(Version, cancellationToken).ConfigureAwait(false);

            var b = Utf8Encoding.GetBytes(message.Name);
            await WriteJsonStringAsync(b, cancellationToken).ConfigureAwait(false);

            await WriteJsonIntegerAsync((long)message.Type, cancellationToken).ConfigureAwait(false);
            await WriteJsonIntegerAsync(message.SeqID, cancellationToken).ConfigureAwait(false);
        }

#if NETSTANDARD2_1
        public override async ValueTask WriteMessageEndAsync(CancellationToken cancellationToken)
#else
        public override async Task WriteMessageEndAsync(CancellationToken cancellationToken)
#endif
        {
            await WriteJsonArrayEndAsync(cancellationToken).ConfigureAwait(false);
        }

#if NETSTANDARD2_1
        public override async ValueTask WriteStructBeginAsync(TStruct @struct, CancellationToken cancellationToken)
#else
        public override async Task WriteStructBeginAsync(TStruct @struct, CancellationToken cancellationToken)
#endif
        {
            await WriteJsonObjectStartAsync(cancellationToken).ConfigureAwait(false);
        }

#if NETSTANDARD2_1
        public override async ValueTask WriteStructEndAsync(CancellationToken cancellationToken)
#else
        public override async Task WriteStructEndAsync(CancellationToken cancellationToken)
#endif
        {
            await WriteJsonObjectEndAsync(cancellationToken).ConfigureAwait(false);
        }

#if NETSTANDARD2_1
        public override async ValueTask WriteFieldBeginAsync(TField field, CancellationToken cancellationToken)
#else
        public override async Task WriteFieldBeginAsync(TField field, CancellationToken cancellationToken)
#endif
        {
            await WriteJsonIntegerAsync(field.ID, cancellationToken).ConfigureAwait(false);
            await WriteJsonObjectStartAsync(cancellationToken).ConfigureAwait(false);
            await WriteJsonStringAsync(TJSONProtocolHelper.GetTypeNameForTypeId(field.Type), cancellationToken).ConfigureAwait(false);
        }

#if NETSTANDARD2_1
        public override async ValueTask WriteFieldEndAsync(CancellationToken cancellationToken)
#else
        public override async Task WriteFieldEndAsync(CancellationToken cancellationToken)
#endif
        {
            await WriteJsonObjectEndAsync(cancellationToken).ConfigureAwait(false);
        }

#if NETSTANDARD2_1
        public override async ValueTask WriteFieldStopAsync(CancellationToken cancellationToken)
#else
        public override async Task WriteFieldStopAsync(CancellationToken cancellationToken)
#endif
        {
            if (cancellationToken.IsCancellationRequested)
            {
                await Task.FromCanceled(cancellationToken).ConfigureAwait(false);
            }
        }

#if NETSTANDARD2_1
        public override async ValueTask WriteMapBeginAsync(TMap map, CancellationToken cancellationToken)
#else
        public override async Task WriteMapBeginAsync(TMap map, CancellationToken cancellationToken)
#endif
        {
            await WriteJsonArrayStartAsync(cancellationToken).ConfigureAwait(false);
            await WriteJsonStringAsync(TJSONProtocolHelper.GetTypeNameForTypeId(map.KeyType), cancellationToken).ConfigureAwait(false);
            await WriteJsonStringAsync(TJSONProtocolHelper.GetTypeNameForTypeId(map.ValueType), cancellationToken).ConfigureAwait(false);
            await WriteJsonIntegerAsync(map.Count, cancellationToken).ConfigureAwait(false);
            await WriteJsonObjectStartAsync(cancellationToken).ConfigureAwait(false);
        }

#if NETSTANDARD2_1
        public override async ValueTask WriteMapEndAsync(CancellationToken cancellationToken)
#else
        public override async Task WriteMapEndAsync(CancellationToken cancellationToken)
#endif
        {
            await WriteJsonObjectEndAsync(cancellationToken).ConfigureAwait(false);
            await WriteJsonArrayEndAsync(cancellationToken).ConfigureAwait(false);
        }

#if NETSTANDARD2_1
        public override async ValueTask WriteListBeginAsync(TList list, CancellationToken cancellationToken)
#else
        public override async Task WriteListBeginAsync(TList list, CancellationToken cancellationToken)
#endif
        {
            await WriteJsonArrayStartAsync(cancellationToken).ConfigureAwait(false);
            await WriteJsonStringAsync(TJSONProtocolHelper.GetTypeNameForTypeId(list.ElementType), cancellationToken).ConfigureAwait(false);
            await WriteJsonIntegerAsync(list.Count, cancellationToken).ConfigureAwait(false);
        }

#if NETSTANDARD2_1
        public override async ValueTask WriteListEndAsync(CancellationToken cancellationToken)
#else
        public override async Task WriteListEndAsync(CancellationToken cancellationToken)
#endif
        {
            await WriteJsonArrayEndAsync(cancellationToken).ConfigureAwait(false);
        }

#if NETSTANDARD2_1
        public override async ValueTask WriteSetBeginAsync(TSet set, CancellationToken cancellationToken)
#else
        public override async Task WriteSetBeginAsync(TSet set, CancellationToken cancellationToken)
#endif
        {
            await WriteJsonArrayStartAsync(cancellationToken).ConfigureAwait(false);
            await WriteJsonStringAsync(TJSONProtocolHelper.GetTypeNameForTypeId(set.ElementType), cancellationToken).ConfigureAwait(false);
            await WriteJsonIntegerAsync(set.Count, cancellationToken).ConfigureAwait(false);
        }

#if NETSTANDARD2_1
        public override async ValueTask WriteSetEndAsync(CancellationToken cancellationToken)
#else
        public override async Task WriteSetEndAsync(CancellationToken cancellationToken)
#endif
        {
            await WriteJsonArrayEndAsync(cancellationToken).ConfigureAwait(false);
        }

#if NETSTANDARD2_1
        public override async ValueTask WriteBoolAsync(bool b, CancellationToken cancellationToken)
#else
        public override async Task WriteBoolAsync(bool b, CancellationToken cancellationToken)
#endif
        {
            await WriteJsonIntegerAsync(b ? 1 : 0, cancellationToken).ConfigureAwait(false);
        }

#if NETSTANDARD2_1
        public override async ValueTask WriteByteAsync(sbyte b, CancellationToken cancellationToken)
#else
        public override async Task WriteByteAsync(sbyte b, CancellationToken cancellationToken)
#endif
        {
            await WriteJsonIntegerAsync(b, cancellationToken).ConfigureAwait(false);
        }

#if NETSTANDARD2_1
        public override async ValueTask WriteI16Async(short i16, CancellationToken cancellationToken)
#else
        public override async Task WriteI16Async(short i16, CancellationToken cancellationToken)
#endif
        {
            await WriteJsonIntegerAsync(i16, cancellationToken).ConfigureAwait(false);
        }

#if NETSTANDARD2_1
        public override async ValueTask WriteI32Async(int i32, CancellationToken cancellationToken)
#else
        public override async Task WriteI32Async(int i32, CancellationToken cancellationToken)
#endif
        {
            await WriteJsonIntegerAsync(i32, cancellationToken).ConfigureAwait(false);
        }

#if NETSTANDARD2_1
        public override async ValueTask WriteI64Async(long i64, CancellationToken cancellationToken)
#else
        public override async Task WriteI64Async(long i64, CancellationToken cancellationToken)
#endif
        {
            await WriteJsonIntegerAsync(i64, cancellationToken).ConfigureAwait(false);
        }

#if NETSTANDARD2_1
        public override async ValueTask WriteDoubleAsync(double d, CancellationToken cancellationToken)
#else
        public override async Task WriteDoubleAsync(double d, CancellationToken cancellationToken)
#endif
        {
            await WriteJsonDoubleAsync(d, cancellationToken).ConfigureAwait(false);
        }

#if NETSTANDARD2_1
        public override async ValueTask WriteStringAsync(string s, CancellationToken cancellationToken)
#else
        public override async Task WriteStringAsync(string s, CancellationToken cancellationToken)
#endif
        {
            var b = Utf8Encoding.GetBytes(s);
            await WriteJsonStringAsync(b, cancellationToken).ConfigureAwait(false);
        }

#if NETSTANDARD2_1
        public override async ValueTask WriteBinaryAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
#else
        public override async Task WriteBinaryAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
#endif
        {
            await WriteJsonBase64Async(buffer, offset, count, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        ///     Read in a JSON string, unescaping as appropriate.. Skip Reading from the
        ///     context if skipContext is true.
        /// </summary>
#if NETSTANDARD2_1
        private async ValueTask<byte[]> ReadJsonStringAsync(bool skipContext, CancellationToken cancellationToken)
#else
        private async Task<byte[]> ReadJsonStringAsync(bool skipContext, CancellationToken cancellationToken)
#endif
        {
            // todo: Switch to a pooling buffer to save GC cycles.
            using (var buffer = new MemoryStream())
            {
                var codeunits = new List<char>();

                if (!skipContext)
                {
                    await Context.ReadAsync(cancellationToken).ConfigureAwait(false);
                }

                await ReadJsonSyntaxCharAsync(TJSONProtocolConstants.Quote, cancellationToken).ConfigureAwait(false);

                while (true)
                {
                    var ch = await Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                    if (ch == TJSONProtocolConstants.Quote[0])
                    {
                        break;
                    }

                    // escaped?
                    if (ch != TJSONProtocolConstants.EscSequences[0])
                    {
#if NETSTANDARD2_1
                        await buffer.WriteAsync(new ReadOnlyMemory<byte>(new[] { ch }), cancellationToken).ConfigureAwait(false);
#else
                        await buffer.WriteAsync(new[] { ch }, 0, 1, cancellationToken).ConfigureAwait(false);
#endif
                        continue;
                    }

                    // distinguish between \uXXXX and \?
                    ch = await Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                    if (ch != TJSONProtocolConstants.EscSequences[1]) // control chars like \n
                    {
                        var off = Array.IndexOf(TJSONProtocolConstants.EscapeChars, (char)ch);
                        if (off == -1)
                        {
                            throw new TProtocolException(TProtocolException.INVALID_DATA, "Expected control char");
                        }
                        ch = TJSONProtocolConstants.EscapeCharValues[off];
#if NETSTANDARD2_1
                        await buffer.WriteAsync(new ReadOnlyMemory<byte>(new[] { ch }), cancellationToken).ConfigureAwait(false);
#else
                        await buffer.WriteAsync(new[] { ch }, 0, 1, cancellationToken).ConfigureAwait(false);
#endif
                        continue;
                    }

                    // it's \uXXXX
                    await Trans.ReadAllAsync(_tempBuffer, 0, 4, cancellationToken).ConfigureAwait(false);

                    var wch = (short)((TJSONProtocolHelper.ToHexVal(_tempBuffer[0]) << 12) +
                                       (TJSONProtocolHelper.ToHexVal(_tempBuffer[1]) << 8) +
                                       (TJSONProtocolHelper.ToHexVal(_tempBuffer[2]) << 4) +
                                       TJSONProtocolHelper.ToHexVal(_tempBuffer[3]));

                    if (char.IsHighSurrogate((char)wch))
                    {
                        if (codeunits.Count > 0)
                        {
                            throw new TProtocolException(TProtocolException.INVALID_DATA, "Expected low surrogate char");
                        }
                        codeunits.Add((char)wch);
                    }
                    else if (char.IsLowSurrogate((char)wch))
                    {
                        if (codeunits.Count == 0)
                        {
                            throw new TProtocolException(TProtocolException.INVALID_DATA, "Expected high surrogate char");
                        }

                        codeunits.Add((char)wch);
                        var tmp = Utf8Encoding.GetBytes(codeunits.ToArray());
#if NETSTANDARD2_1
                        await buffer.WriteAsync(new ReadOnlyMemory<byte>(tmp, 0, tmp.Length), cancellationToken).ConfigureAwait(false);
#else
                        await buffer.WriteAsync(tmp, 0, tmp.Length, cancellationToken).ConfigureAwait(false);
#endif
                        codeunits.Clear();
                    }
                    else
                    {
                        var tmp = Utf8Encoding.GetBytes(new[] { (char)wch });
#if NETSTANDARD2_1
                        await buffer.WriteAsync(new ReadOnlyMemory<byte>(tmp, 0, tmp.Length), cancellationToken).ConfigureAwait(false);
#else
                        await buffer.WriteAsync(tmp, 0, tmp.Length, cancellationToken).ConfigureAwait(false);
#endif
                    }
                }

                if (codeunits.Count > 0)
                {
                    throw new TProtocolException(TProtocolException.INVALID_DATA, "Expected low surrogate char");
                }

                return buffer.ToArray();
            }
        }

        /// <summary>
        ///     Read in a sequence of characters that are all valid in JSON numbers. Does
        ///     not do a complete regex check to validate that this is actually a number.
        /// </summary>
#if NETSTANDARD2_1
        private async ValueTask<string> ReadJsonNumericCharsAsync(CancellationToken cancellationToken)
#else
        private async Task<string> ReadJsonNumericCharsAsync(CancellationToken cancellationToken)
#endif
        {
            var strbld = new StringBuilder();
            while (true)
            {
                //TODO: workaround for primitive types with TJsonProtocol, think - how to rewrite into more easy form without exceptions
                try
                {
                    var ch = await Reader.PeekAsync(cancellationToken).ConfigureAwait(false);
                    if (!TJSONProtocolHelper.IsJsonNumeric(ch))
                    {
                        break;
                    }
                    var c = (char)await Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                    strbld.Append(c);
                }
                catch (TTransportException)
                {
                    break;
                }
            }
            return strbld.ToString();
        }

        /// <summary>
        ///     Read in a JSON number. If the context dictates, Read in enclosing quotes.
        /// </summary>
#if NETSTANDARD2_1
        private async ValueTask<long> ReadJsonIntegerAsync(CancellationToken cancellationToken)
#else
        private async Task<long> ReadJsonIntegerAsync(CancellationToken cancellationToken)
#endif
        {
            await Context.ReadAsync(cancellationToken).ConfigureAwait(false);
            if (Context.EscapeNumbers())
            {
                await ReadJsonSyntaxCharAsync(TJSONProtocolConstants.Quote, cancellationToken).ConfigureAwait(false);
            }

            var str = await ReadJsonNumericCharsAsync(cancellationToken).ConfigureAwait(false);
            if (Context.EscapeNumbers())
            {
                await ReadJsonSyntaxCharAsync(TJSONProtocolConstants.Quote, cancellationToken).ConfigureAwait(false);
            }

            try
            {
                return long.Parse(str);
            }
            catch (FormatException)
            {
                throw new TProtocolException(TProtocolException.INVALID_DATA, "Bad data encounted in numeric data");
            }
        }

        /// <summary>
        ///     Read in a JSON double value. Throw if the value is not wrapped in quotes
        ///     when expected or if wrapped in quotes when not expected.
        /// </summary>
#if NETSTANDARD2_1
        private async ValueTask<double> ReadJsonDoubleAsync(CancellationToken cancellationToken)
#else
        private async Task<double> ReadJsonDoubleAsync(CancellationToken cancellationToken)
#endif
        {
            await Context.ReadAsync(cancellationToken).ConfigureAwait(false);
            if (await Reader.PeekAsync(cancellationToken) == TJSONProtocolConstants.Quote[0])
            {
                var arr = await ReadJsonStringAsync(true, cancellationToken).ConfigureAwait(false);
                var dub = double.Parse(Utf8Encoding.GetString(arr, 0, arr.Length), CultureInfo.InvariantCulture);

                if (!Context.EscapeNumbers() && !double.IsNaN(dub) && !double.IsInfinity(dub))
                {
                    // Throw exception -- we should not be in a string in this case
                    throw new TProtocolException(TProtocolException.INVALID_DATA, "Numeric data unexpectedly quoted");
                }

                return dub;
            }

            if (Context.EscapeNumbers())
            {
                // This will throw - we should have had a quote if escapeNum == true
                await ReadJsonSyntaxCharAsync(TJSONProtocolConstants.Quote, cancellationToken).ConfigureAwait(false);
            }

            try
            {
                return double.Parse(await ReadJsonNumericCharsAsync(cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture);
            }
            catch (FormatException)
            {
                throw new TProtocolException(TProtocolException.INVALID_DATA, "Bad data encounted in numeric data");
            }
        }

        /// <summary>
        ///     Read in a JSON string containing base-64 encoded data and decode it.
        /// </summary>
#if NETSTANDARD2_1
        private async ValueTask<byte[]> ReadJsonBase64Async(CancellationToken cancellationToken)
#else
        private async Task<byte[]> ReadJsonBase64Async(CancellationToken cancellationToken)
#endif
        {
            var b = await ReadJsonStringAsync(false, cancellationToken).ConfigureAwait(false);
            var len = b.Length;
            var off = 0;
            var size = 0;

            // reduce len to ignore fill bytes
            while ((len > 0) && (b[len - 1] == '='))
            {
                --len;
            }

            // read & decode full byte triplets = 4 source bytes
            while (len > 4)
            {
                // Decode 4 bytes at a time
                TBase64Helper.Decode(b, off, 4, b, size); // NB: decoded in place
                off += 4;
                len -= 4;
                size += 3;
            }

            // Don't decode if we hit the end or got a single leftover byte (invalid
            // base64 but legal for skip of regular string exType)
            if (len > 1)
            {
                // Decode remainder
                TBase64Helper.Decode(b, off, len, b, size); // NB: decoded in place
                size += len - 1;
            }

            // Sadly we must copy the byte[] (any way around this?)
            var result = new byte[size];
            Array.Copy(b, 0, result, 0, size);
            return result;
        }

#if NETSTANDARD2_1
        private async ValueTask ReadJsonObjectStartAsync(CancellationToken cancellationToken)
#else
        private async Task ReadJsonObjectStartAsync(CancellationToken cancellationToken)
#endif
        {
            await Context.ReadAsync(cancellationToken).ConfigureAwait(false);
            await ReadJsonSyntaxCharAsync(TJSONProtocolConstants.LeftBrace, cancellationToken).ConfigureAwait(false);
            PushContext(new JSONPairContext(this));
        }

#if NETSTANDARD2_1
        private async ValueTask ReadJsonObjectEndAsync(CancellationToken cancellationToken)
#else
        private async Task ReadJsonObjectEndAsync(CancellationToken cancellationToken)
#endif
        {
            await ReadJsonSyntaxCharAsync(TJSONProtocolConstants.RightBrace, cancellationToken).ConfigureAwait(false);
            PopContext();
        }

#if NETSTANDARD2_1
        private async ValueTask ReadJsonArrayStartAsync(CancellationToken cancellationToken)
#else
        private async Task ReadJsonArrayStartAsync(CancellationToken cancellationToken)
#endif
        {
            await Context.ReadAsync(cancellationToken).ConfigureAwait(false);
            await ReadJsonSyntaxCharAsync(TJSONProtocolConstants.LeftBracket, cancellationToken).ConfigureAwait(false);
            PushContext(new JSONListContext(this));
        }

#if NETSTANDARD2_1
        private async ValueTask ReadJsonArrayEndAsync(CancellationToken cancellationToken)
#else
        private async Task ReadJsonArrayEndAsync(CancellationToken cancellationToken)
#endif
        {
            await ReadJsonSyntaxCharAsync(TJSONProtocolConstants.RightBracket, cancellationToken).ConfigureAwait(false);
            PopContext();
        }

#if NETSTANDARD2_1
        public override async ValueTask<TMessage> ReadMessageBeginAsync(CancellationToken cancellationToken)
#else
        public override async Task<TMessage> ReadMessageBeginAsync(CancellationToken cancellationToken)
#endif
        {
            var message = new TMessage();
            await ReadJsonArrayStartAsync(cancellationToken).ConfigureAwait(false);
            if (await ReadJsonIntegerAsync(cancellationToken) != Version)
            {
                throw new TProtocolException(TProtocolException.BAD_VERSION, "Message contained bad version.");
            }

            var buf = await ReadJsonStringAsync(false, cancellationToken).ConfigureAwait(false);
            message.Name = Utf8Encoding.GetString(buf, 0, buf.Length);
            message.Type = (TMessageType)await ReadJsonIntegerAsync(cancellationToken).ConfigureAwait(false);
            message.SeqID = (int)await ReadJsonIntegerAsync(cancellationToken).ConfigureAwait(false);
            return message;
        }

#if NETSTANDARD2_1
        public override async ValueTask ReadMessageEndAsync(CancellationToken cancellationToken)
#else
        public override async Task ReadMessageEndAsync(CancellationToken cancellationToken)
#endif
        {
            await ReadJsonArrayEndAsync(cancellationToken).ConfigureAwait(false);
        }

#if NETSTANDARD2_1
        public override async ValueTask<TStruct> ReadStructBeginAsync(CancellationToken cancellationToken)
#else
        public override async Task<TStruct> ReadStructBeginAsync(CancellationToken cancellationToken)
#endif
        {
            await ReadJsonObjectStartAsync(cancellationToken).ConfigureAwait(false);
            return new TStruct();
        }

#if NETSTANDARD2_1
        public override async ValueTask ReadStructEndAsync(CancellationToken cancellationToken)
#else
        public override async Task ReadStructEndAsync(CancellationToken cancellationToken)
#endif
        {
            await ReadJsonObjectEndAsync(cancellationToken).ConfigureAwait(false);
        }

#if NETSTANDARD2_1
        public override async ValueTask<TField> ReadFieldBeginAsync(CancellationToken cancellationToken)
#else
        public override async Task<TField> ReadFieldBeginAsync(CancellationToken cancellationToken)
#endif
        {
            var field = new TField();
            var ch = await Reader.PeekAsync(cancellationToken).ConfigureAwait(false);
            if (ch == TJSONProtocolConstants.RightBrace[0])
            {
                field.Type = TType.Stop;
            }
            else
            {
                field.ID = (short)await ReadJsonIntegerAsync(cancellationToken).ConfigureAwait(false);
                await ReadJsonObjectStartAsync(cancellationToken).ConfigureAwait(false);
                field.Type = TJSONProtocolHelper.GetTypeIdForTypeName(await ReadJsonStringAsync(false, cancellationToken).ConfigureAwait(false));
            }
            return field;
        }

#if NETSTANDARD2_1
        public override async ValueTask ReadFieldEndAsync(CancellationToken cancellationToken)
#else
        public override async Task ReadFieldEndAsync(CancellationToken cancellationToken)
#endif
        {
            await ReadJsonObjectEndAsync(cancellationToken).ConfigureAwait(false);
        }

#if NETSTANDARD2_1
        public override async ValueTask<TMap> ReadMapBeginAsync(CancellationToken cancellationToken)
#else
        public override async Task<TMap> ReadMapBeginAsync(CancellationToken cancellationToken)
#endif
        {
            var map = new TMap();
            await ReadJsonArrayStartAsync(cancellationToken).ConfigureAwait(false);
            map.KeyType = TJSONProtocolHelper.GetTypeIdForTypeName(await ReadJsonStringAsync(false, cancellationToken).ConfigureAwait(false));
            map.ValueType = TJSONProtocolHelper.GetTypeIdForTypeName(await ReadJsonStringAsync(false, cancellationToken).ConfigureAwait(false));
            map.Count = (int)await ReadJsonIntegerAsync(cancellationToken).ConfigureAwait(false);
            await ReadJsonObjectStartAsync(cancellationToken).ConfigureAwait(false);
            return map;
        }

#if NETSTANDARD2_1
        public override async ValueTask ReadMapEndAsync(CancellationToken cancellationToken)
#else
        public override async Task ReadMapEndAsync(CancellationToken cancellationToken)
#endif
        {
            await ReadJsonObjectEndAsync(cancellationToken).ConfigureAwait(false);
            await ReadJsonArrayEndAsync(cancellationToken).ConfigureAwait(false);
        }

#if NETSTANDARD2_1
        public override async ValueTask<TList> ReadListBeginAsync(CancellationToken cancellationToken)
#else
        public override async Task<TList> ReadListBeginAsync(CancellationToken cancellationToken)
#endif
        {
            var list = new TList();
            await ReadJsonArrayStartAsync(cancellationToken).ConfigureAwait(false);
            list.ElementType = TJSONProtocolHelper.GetTypeIdForTypeName(await ReadJsonStringAsync(false, cancellationToken).ConfigureAwait(false));
            list.Count = (int)await ReadJsonIntegerAsync(cancellationToken).ConfigureAwait(false);
            return list;
        }

#if NETSTANDARD2_1
        public override async ValueTask ReadListEndAsync(CancellationToken cancellationToken)
#else
        public override async Task ReadListEndAsync(CancellationToken cancellationToken)
#endif
        {
            await ReadJsonArrayEndAsync(cancellationToken).ConfigureAwait(false);
        }

#if NETSTANDARD2_1
        public override async ValueTask<TSet> ReadSetBeginAsync(CancellationToken cancellationToken)
#else
        public override async Task<TSet> ReadSetBeginAsync(CancellationToken cancellationToken)
#endif
        {
            var set = new TSet();
            await ReadJsonArrayStartAsync(cancellationToken).ConfigureAwait(false);
            set.ElementType = TJSONProtocolHelper.GetTypeIdForTypeName(await ReadJsonStringAsync(false, cancellationToken).ConfigureAwait(false));
            set.Count = (int)await ReadJsonIntegerAsync(cancellationToken).ConfigureAwait(false);
            return set;
        }

#if NETSTANDARD2_1
        public override async ValueTask ReadSetEndAsync(CancellationToken cancellationToken)
#else
        public override async Task ReadSetEndAsync(CancellationToken cancellationToken)
#endif
        {
            await ReadJsonArrayEndAsync(cancellationToken).ConfigureAwait(false);
        }

#if NETSTANDARD2_1
        public override async ValueTask<bool> ReadBoolAsync(CancellationToken cancellationToken)
#else
        public override async Task<bool> ReadBoolAsync(CancellationToken cancellationToken)
#endif
        {
            return (await ReadJsonIntegerAsync(cancellationToken).ConfigureAwait(false)) != 0;
        }

#if NETSTANDARD2_1
        public override async ValueTask<sbyte> ReadByteAsync(CancellationToken cancellationToken)
#else
        public override async Task<sbyte> ReadByteAsync(CancellationToken cancellationToken)
#endif
        {
            return (sbyte)await ReadJsonIntegerAsync(cancellationToken).ConfigureAwait(false);
        }

#if NETSTANDARD2_1
        public override async ValueTask<short> ReadI16Async(CancellationToken cancellationToken)
#else
        public override async Task<short> ReadI16Async(CancellationToken cancellationToken)
#endif
        {
            return (short)await ReadJsonIntegerAsync(cancellationToken).ConfigureAwait(false);
        }

#if NETSTANDARD2_1
        public override async ValueTask<int> ReadI32Async(CancellationToken cancellationToken)
#else
        public override async Task<int> ReadI32Async(CancellationToken cancellationToken)
#endif
        {
            return (int)await ReadJsonIntegerAsync(cancellationToken).ConfigureAwait(false);
        }

#if NETSTANDARD2_1
        public override async ValueTask<long> ReadI64Async(CancellationToken cancellationToken)
#else
        public override async Task<long> ReadI64Async(CancellationToken cancellationToken)
#endif
        {
            return await ReadJsonIntegerAsync(cancellationToken).ConfigureAwait(false);
        }

#if NETSTANDARD2_1
        public override async ValueTask<double> ReadDoubleAsync(CancellationToken cancellationToken)
#else
        public override async Task<double> ReadDoubleAsync(CancellationToken cancellationToken)
#endif
        {
            return await ReadJsonDoubleAsync(cancellationToken).ConfigureAwait(false);
        }

#if NETSTANDARD2_1
        public override async ValueTask<string> ReadStringAsync(CancellationToken cancellationToken)
#else
        public override async Task<string> ReadStringAsync(CancellationToken cancellationToken)
#endif
        {
            var buf = await ReadJsonStringAsync(false, cancellationToken).ConfigureAwait(false);
            return Utf8Encoding.GetString(buf, 0, buf.Length);
        }

#if NETSTANDARD2_1
        public override async ValueTask<byte[]> ReadBinaryAsync(CancellationToken cancellationToken)
#else
        public override async Task<byte[]> ReadBinaryAsync(CancellationToken cancellationToken)
#endif
        {
            return await ReadJsonBase64Async(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        ///     Factory for JSON protocol objects
        /// </summary>
        public class Factory : ITProtocolFactory
        {
            public TProtocol GetProtocol(TClientTransport trans)
            {
                return new TJsonProtocol(trans);
            }
        }

        /// <summary>
        ///     Base class for tracking JSON contexts that may require
        ///     inserting/Reading additional JSON syntax characters
        ///     This base context does nothing.
        /// </summary>
        protected class JSONBaseContext
        {
            protected TJsonProtocol Proto;

            public JSONBaseContext(TJsonProtocol proto)
            {
                Proto = proto;
            }

#if NETSTANDARD2_1
            public virtual async ValueTask WriteAsync(CancellationToken cancellationToken)
#else
            public virtual async Task WriteAsync(CancellationToken cancellationToken)
#endif
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    await Task.FromCanceled(cancellationToken).ConfigureAwait(false);
                }
            }

#if NETSTANDARD2_1
            public virtual async ValueTask ReadAsync(CancellationToken cancellationToken)
#else
            public virtual async Task ReadAsync(CancellationToken cancellationToken)
#endif
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    await Task.FromCanceled(cancellationToken).ConfigureAwait(false);
                }
            }

            public virtual bool EscapeNumbers()
            {
                return false;
            }
        }

        /// <summary>
        ///     Context for JSON lists. Will insert/Read commas before each item except
        ///     for the first one
        /// </summary>
        protected class JSONListContext : JSONBaseContext
        {
            private bool _first = true;

            public JSONListContext(TJsonProtocol protocol)
                : base(protocol)
            {
            }

#if NETSTANDARD2_1
            public override async ValueTask WriteAsync(CancellationToken cancellationToken)
#else
            public override async Task WriteAsync(CancellationToken cancellationToken)
#endif
            {
                if (_first)
                {
                    _first = false;
                }
                else
                {
                    await Proto.Trans.WriteAsync(TJSONProtocolConstants.Comma, cancellationToken).ConfigureAwait(false);
                }
            }

#if NETSTANDARD2_1
            public override async ValueTask ReadAsync(CancellationToken cancellationToken)
#else
            public override async Task ReadAsync(CancellationToken cancellationToken)
#endif
            {
                if (_first)
                {
                    _first = false;
                }
                else
                {
                    await Proto.ReadJsonSyntaxCharAsync(TJSONProtocolConstants.Comma, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        ///     Context for JSON records. Will insert/Read colons before the value portion
        ///     of each record pair, and commas before each key except the first. In
        ///     addition, will indicate that numbers in the key position need to be
        ///     escaped in quotes (since JSON keys must be strings).
        /// </summary>
        // ReSharper disable once InconsistentNaming
        protected class JSONPairContext : JSONBaseContext
        {
            private bool _colon = true;

            private bool _first = true;

            public JSONPairContext(TJsonProtocol proto)
                : base(proto)
            {
            }

#if NETSTANDARD2_1
            public override async ValueTask WriteAsync(CancellationToken cancellationToken)
#else
            public override async Task WriteAsync(CancellationToken cancellationToken)
#endif
            {
                if (_first)
                {
                    _first = false;
                    _colon = true;
                }
                else
                {
                    await Proto.Trans.WriteAsync(_colon ? TJSONProtocolConstants.Colon : TJSONProtocolConstants.Comma, cancellationToken).ConfigureAwait(false);
                    _colon = !_colon;
                }
            }

#if NETSTANDARD2_1
            public override async ValueTask ReadAsync(CancellationToken cancellationToken)
#else
            public override async Task ReadAsync(CancellationToken cancellationToken)
#endif
            {
                if (_first)
                {
                    _first = false;
                    _colon = true;
                }
                else
                {
                    await Proto.ReadJsonSyntaxCharAsync(_colon ? TJSONProtocolConstants.Colon : TJSONProtocolConstants.Comma, cancellationToken).ConfigureAwait(false);
                    _colon = !_colon;
                }
            }

            public override bool EscapeNumbers()
            {
                return _colon;
            }
        }

        /// <summary>
        ///     Holds up to one byte from the transport
        /// </summary>
        protected class LookaheadReader
        {
            private readonly byte[] _data = new byte[1];

            private bool _hasData;
            protected TJsonProtocol Proto;

            public LookaheadReader(TJsonProtocol proto)
            {
                Proto = proto;
            }

            /// <summary>
            ///     Return and consume the next byte to be Read, either taking it from the
            ///     data buffer if present or getting it from the transport otherwise.
            /// </summary>
#if NETSTANDARD2_1
            public async ValueTask<byte> ReadAsync(CancellationToken cancellationToken)
#else
            public async Task<byte> ReadAsync(CancellationToken cancellationToken)
#endif
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return await Task.FromCanceled<byte>(cancellationToken).ConfigureAwait(false);
                }

                if (_hasData)
                {
                    _hasData = false;
                }
                else
                {
                    // find more easy way to avoid exception on reading primitive types
                    await Proto.Trans.ReadAllAsync(_data, 0, 1, cancellationToken).ConfigureAwait(false);
                }
                return _data[0];
            }

            /// <summary>
            ///     Return the next byte to be Read without consuming, filling the data
            ///     buffer if it has not been filled alReady.
            /// </summary>
#if NETSTANDARD2_1
            public async ValueTask<byte> PeekAsync(CancellationToken cancellationToken)
#else
            public async Task<byte> PeekAsync(CancellationToken cancellationToken)
#endif
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return await Task.FromCanceled<byte>(cancellationToken).ConfigureAwait(false);
                }

                if (!_hasData)
                {
                    // find more easy way to avoid exception on reading primitive types
                    await Proto.Trans.ReadAllAsync(_data, 0, 1, cancellationToken).ConfigureAwait(false);
                }
                _hasData = true;
                return _data[0];
            }
        }
    }
}

﻿// <copyright file="JaegerThriftClient.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Thrift;
using Thrift.Protocols;
using Thrift.Protocols.Entities;

namespace OpenTelemetry.Exporter.Jaeger.Implementation
{
    public class JaegerThriftClient : TBaseClient, IDisposable
    {
        public JaegerThriftClient(TProtocol protocol)
            : this(protocol, protocol)
        {
        }

        public JaegerThriftClient(TProtocol inputProtocol, TProtocol outputProtocol)
            : base(inputProtocol, outputProtocol)
        {
        }

#if NETSTANDARD2_1
        public async ValueTask EmitBatchAsync(Batch batch, CancellationToken cancellationToken)
#else
        public async Task EmitBatchAsync(Batch batch, CancellationToken cancellationToken)
#endif
        {
            await this.OutputProtocol.WriteMessageBeginAsync(new TMessage("emitBatch", TMessageType.Oneway, this.SeqId), cancellationToken);

            var args = new EmitBatchArgs
            {
                Batch = batch,
            };

            await args.WriteAsync(this.OutputProtocol, cancellationToken);
            await this.OutputProtocol.WriteMessageEndAsync(cancellationToken);
            await this.OutputProtocol.Transport.FlushAsync(cancellationToken);
        }

#if NETSTANDARD2_1
        internal async ValueTask EmitBatchAsync(ArraySegment<byte> processMessage, IEnumerable<ArraySegment<byte>> spanMessages, CancellationToken cancellationToken)
#else
        internal async Task EmitBatchAsync(ArraySegment<byte> processMessage, IEnumerable<ArraySegment<byte>> spanMessages, CancellationToken cancellationToken)
#endif
        {
            await this.OutputProtocol.WriteMessageBeginAsync(new TMessage("emitBatch", TMessageType.Oneway, this.SeqId), cancellationToken);
            await EmitBatchArgs.WriteAsync(processMessage, spanMessages, this.OutputProtocol, cancellationToken);
            await this.OutputProtocol.WriteMessageEndAsync(cancellationToken);
            await this.OutputProtocol.Transport.FlushAsync(cancellationToken);
        }
    }
}

// <copyright file="Batch.cs" company="OpenTelemetry Authors">
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
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Thrift.Protocols;
using Thrift.Protocols.Entities;

namespace OpenTelemetry.Exporter.Jaeger.Implementation
{
    public class Batch : TAbstractBase
    {
        private readonly ArraySegment<byte> processMessage;

        private readonly IEnumerable<ArraySegment<byte>> spanMessages;

        public Batch(ArraySegment<byte> processMessage, IEnumerable<ArraySegment<byte>> spanMessages)
        {
            this.processMessage = processMessage;
            this.spanMessages = spanMessages ?? Enumerable.Empty<ArraySegment<byte>>();
        }

        public async Task WriteAsync(TProtocol oprot, CancellationToken cancellationToken)
        {
            oprot.IncrementRecursionDepth();
            try
            {
                var struc = new TStruct("Batch");

                await oprot.WriteStructBeginAsync(struc, cancellationToken);

                var field = new TField
                {
                    Name = "process",
                    Type = TType.Struct,
                    ID = 1,
                };

                await oprot.WriteFieldBeginAsync(field, cancellationToken);
                await oprot.WriteRawAsync(this.processMessage, cancellationToken);
                await oprot.WriteFieldEndAsync(cancellationToken);

                field.Name = "spans";
                field.Type = TType.List;
                field.ID = 2;

                await oprot.WriteFieldBeginAsync(field, cancellationToken);
                {
                    await oprot.WriteListBeginAsync(new TList(TType.Struct, this.spanMessages.Count()), cancellationToken);

                    foreach (var s in this.spanMessages)
                    {
                        await oprot.WriteRawAsync(s, cancellationToken);
                    }

                    await oprot.WriteListEndAsync(cancellationToken);
                }

                await oprot.WriteFieldEndAsync(cancellationToken);
                await oprot.WriteFieldStopAsync(cancellationToken);
                await oprot.WriteStructEndAsync(cancellationToken);
            }
            finally
            {
                oprot.DecrementRecursionDepth();
            }
        }

        public override string ToString()
        {
            var sb = new StringBuilder("Batch(");
            sb.Append(", Process: ");
            sb.Append(this.processMessage.Count);
            sb.Append(" bytes, Spans: ");
            sb.Append(this.spanMessages.Count());
            sb.Append(")");
            return sb.ToString();
        }
    }
}

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
using Thrift.Protocol;
using Thrift.Protocol.Entities;

namespace OpenTelemetry.Exporter.Jaeger.Implementation
{
    internal class Batch : TUnionBase
    {
        public Batch(Process process, IEnumerable<JaegerSpan> spans = null)
        {
            this.Process = process ?? throw new ArgumentNullException(nameof(process));
            this.Spans = spans;
        }

        public Process Process { get; }

        public IEnumerable<JaegerSpan> Spans { get; set; }

        internal List<BufferWriterMemory> SpanMessages { get; set; }

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
                await this.Process.WriteAsync(oprot, cancellationToken);
                await oprot.WriteFieldEndAsync(cancellationToken);

                field.Name = "spans";
                field.Type = TType.List;
                field.ID = 2;

                var spans = this.Spans ?? Enumerable.Empty<JaegerSpan>();

                await oprot.WriteFieldBeginAsync(field, cancellationToken);
                {
                    await oprot.WriteListBeginAsync(new TList(TType.Struct, spans.Count()), cancellationToken);
                    foreach (var s in spans)
                    {
                        await s.WriteAsync(oprot, cancellationToken);
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
            sb.Append(this.Process?.ToString() ?? "<null>");
            sb.Append(", Spans: ");
            sb.Append(this.Spans);
            sb.Append(")");
            return sb.ToString();
        }

        internal static async Task WriteAsync(byte[] processMessage, List<BufferWriterMemory> spanMessages, TProtocol oprot, CancellationToken cancellationToken)
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
                await oprot.Transport.WriteAsync(processMessage, cancellationToken);
                await oprot.WriteFieldEndAsync(cancellationToken);

                field.Name = "spans";
                field.Type = TType.List;
                field.ID = 2;

                await oprot.WriteFieldBeginAsync(field, cancellationToken);
                {
                    await oprot.WriteListBeginAsync(new TList(TType.Struct, spanMessages?.Count ?? 0), cancellationToken);

                    if (spanMessages != null)
                    {
                        foreach (var s in spanMessages)
                        {
                            await oprot.Transport.WriteAsync(s.BufferWriter.Buffer, s.Offset, s.Count, cancellationToken);
                        }
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
    }
}

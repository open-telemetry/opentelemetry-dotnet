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
        public Batch(Process process, List<JaegerSpan> spans = null)
        {
            this.Process = process ?? throw new ArgumentNullException(nameof(process));
            this.Spans = spans ?? new List<JaegerSpan>();
        }

        public Process Process { get; }

        public List<JaegerSpan> Spans { get; }

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

                await oprot.WriteFieldBeginAsync(field, cancellationToken);
                {
                    await oprot.WriteListBeginAsync(new TList(TType.Struct, this.Spans.Count()), cancellationToken);
                    foreach (JaegerSpan s in this.Spans)
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
    }
}

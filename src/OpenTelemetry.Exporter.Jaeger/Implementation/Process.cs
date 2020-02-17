// <copyright file="Process.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Thrift.Protocols;
using Thrift.Protocols.Entities;

namespace OpenTelemetry.Exporter.Jaeger.Implementation
{
    public class Process : TAbstractBase
    {
        public Process()
        {
        }

        public Process(string serviceName, IDictionary<string, object> processTags)
            : this()
        {
            this.ServiceName = serviceName;

            if (processTags != null)
            {
                this.Tags = processTags.Select(pt => pt.ToJaegerTag()).ToDictionary(jt => jt.Key, jt => jt);
            }
        }

        public string ServiceName { get; set; }

        public IDictionary<string, JaegerTag> Tags { get; set; }

        public async Task WriteAsync(TProtocol oprot, CancellationToken cancellationToken)
        {
            oprot.IncrementRecursionDepth();

            try
            {
                var struc = new TStruct("Process");
                await oprot.WriteStructBeginAsync(struc, cancellationToken);

                var field = new TField
                {
                    Name = "serviceName",
                    Type = TType.String,
                    ID = 1,
                };

                await oprot.WriteFieldBeginAsync(field, cancellationToken);
                await oprot.WriteStringAsync(this.ServiceName, cancellationToken);
                await oprot.WriteFieldEndAsync(cancellationToken);

                if (this.Tags != null)
                {
                    field.Name = "tags";
                    field.Type = TType.List;
                    field.ID = 2;

                    await oprot.WriteFieldBeginAsync(field, cancellationToken);
                    {
                        await oprot.WriteListBeginAsync(new TList(TType.Struct, this.Tags.Count), cancellationToken);

                        foreach (var jt in this.Tags)
                        {
                            await jt.Value.WriteAsync(oprot, cancellationToken);
                        }

                        await oprot.WriteListEndAsync(cancellationToken);
                    }

                    await oprot.WriteFieldEndAsync(cancellationToken);
                }

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
            var sb = new StringBuilder("Process(");
            sb.Append(", ServiceName: ");
            sb.Append(this.ServiceName);

            if (this.Tags != null)
            {
                sb.Append(", Tags: ");
                sb.Append(this.Tags);
            }

            sb.Append(")");
            return sb.ToString();
        }
    }
}

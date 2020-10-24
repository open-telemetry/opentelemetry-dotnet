// <copyright file="Process.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
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
using OpenTelemetry.Exporter.Jaeger.Implementation;
using Thrift.Protocol;
using Thrift.Protocol.Entities;

namespace OpenTelemetry.Exporter.Jaeger
{
    public class Process
    {
        public Process(string serviceName)
        {
            this.ServiceName = serviceName;
        }

        public Process(string serviceName, IEnumerable<KeyValuePair<string, object>> processTags)
            : this(serviceName, processTags?.Select(pt => pt.ToJaegerTag()).ToDictionary(pt => pt.Key, pt => pt))
        {
        }

        internal Process(string serviceName, Dictionary<string, JaegerTag> processTags)
            : this(serviceName)
        {
            if (processTags != null)
            {
                this.Tags = processTags;
            }
        }

        public string ServiceName { get; internal set; }

        internal Dictionary<string, JaegerTag> Tags { get; set; }

        internal byte[] Message { get; set; }

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

            sb.Append(')');
            return sb.ToString();
        }

        internal void Write(TProtocol oprot)
        {
            oprot.IncrementRecursionDepth();

            try
            {
                var struc = new TStruct("Process");
                oprot.WriteStructBegin(struc);

                var field = new TField
                {
                    Name = "serviceName",
                    Type = TType.String,
                    ID = 1,
                };

                oprot.WriteFieldBegin(field);
                oprot.WriteString(this.ServiceName);
                oprot.WriteFieldEnd();

                if (this.Tags != null)
                {
                    field.Name = "tags";
                    field.Type = TType.List;
                    field.ID = 2;

                    oprot.WriteFieldBegin(field);
                    {
                        oprot.WriteListBegin(new TList(TType.Struct, this.Tags.Count));

                        foreach (var jt in this.Tags)
                        {
                            jt.Value.Write(oprot);
                        }

                        oprot.WriteListEnd();
                    }

                    oprot.WriteFieldEnd();
                }

                oprot.WriteFieldStop();
                oprot.WriteStructEnd();
            }
            finally
            {
                oprot.DecrementRecursionDepth();
            }
        }
    }
}

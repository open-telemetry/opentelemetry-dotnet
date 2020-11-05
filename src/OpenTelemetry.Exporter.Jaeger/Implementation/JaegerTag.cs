// <copyright file="JaegerTag.cs" company="OpenTelemetry Authors">
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

using System.Text;
using Thrift.Protocol;
using Thrift.Protocol.Entities;

namespace OpenTelemetry.Exporter.Jaeger.Implementation
{
    internal readonly struct JaegerTag : TUnionBase
    {
        public JaegerTag(
            string key,
            JaegerTagType vType,
            string vStr = null,
            double? vDouble = null,
            bool? vBool = null,
            long? vLong = null,
            byte[] vBinary = null)
        {
            this.Key = key;
            this.VType = vType;

            this.VStr = vStr;
            this.VDouble = vDouble;
            this.VBool = vBool;
            this.VLong = vLong;
            this.VBinary = vBinary;
        }

        public string Key { get; }

        public JaegerTagType VType { get; }

        public string VStr { get; }

        public double? VDouble { get; }

        public bool? VBool { get; }

        public long? VLong { get; }

        public byte[] VBinary { get; }

        public void Write(TProtocol oprot)
        {
            oprot.IncrementRecursionDepth();
            try
            {
                var struc = new TStruct("Tag");
                oprot.WriteStructBegin(struc);

                var field = new TField
                {
                    Name = "key",
                    Type = TType.String,
                    ID = 1,
                };

                oprot.WriteFieldBegin(field);
                oprot.WriteString(this.Key);
                oprot.WriteFieldEnd();

                field.Name = "vType";
                field.Type = TType.I32;
                field.ID = 2;

                oprot.WriteFieldBegin(field);
                oprot.WriteI32((int)this.VType);
                oprot.WriteFieldEnd();

                if (this.VStr != null)
                {
                    field.Name = "vStr";
                    field.Type = TType.String;
                    field.ID = 3;
                    oprot.WriteFieldBegin(field);
                    oprot.WriteString(this.VStr);
                    oprot.WriteFieldEnd();
                }

                if (this.VDouble.HasValue)
                {
                    field.Name = "vDouble";
                    field.Type = TType.Double;
                    field.ID = 4;
                    oprot.WriteFieldBegin(field);
                    oprot.WriteDouble(this.VDouble.Value);
                    oprot.WriteFieldEnd();
                }

                if (this.VBool.HasValue)
                {
                    field.Name = "vBool";
                    field.Type = TType.Bool;
                    field.ID = 5;
                    oprot.WriteFieldBegin(field);
                    oprot.WriteBool(this.VBool.Value);
                    oprot.WriteFieldEnd();
                }

                if (this.VLong.HasValue)
                {
                    field.Name = "vLong";
                    field.Type = TType.I64;
                    field.ID = 6;
                    oprot.WriteFieldBegin(field);
                    oprot.WriteI64(this.VLong.Value);
                    oprot.WriteFieldEnd();
                }

                if (this.VBinary != null)
                {
                    field.Name = "vBinary";
                    field.Type = TType.String;
                    field.ID = 7;
                    oprot.WriteFieldBegin(field);
                    oprot.WriteBinary(this.VBinary, 0, this.VBinary.Length);
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

        public override string ToString()
        {
            var sb = new StringBuilder("Tag(");
            sb.Append(", Key: ");
            sb.Append(this.Key);
            sb.Append(", VType: ");
            sb.Append(this.VType);
            if (this.VStr != null)
            {
                sb.Append(", VStr: ");
                sb.Append(this.VStr);
            }

            if (this.VDouble.HasValue)
            {
                sb.Append(", VDouble: ");
                sb.Append(this.VDouble);
            }

            if (this.VBool.HasValue)
            {
                sb.Append(", VBool: ");
                sb.Append(this.VBool);
            }

            if (this.VLong.HasValue)
            {
                sb.Append(", VLong: ");
                sb.Append(this.VLong);
            }

            if (this.VBinary != null)
            {
                sb.Append(", VBinary: ");
                sb.Append(this.VBinary);
            }

            sb.Append(')');
            return sb.ToString();
        }
    }
}

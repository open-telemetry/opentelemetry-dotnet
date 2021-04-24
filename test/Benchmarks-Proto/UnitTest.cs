// <copyright file="UnitTest.cs" company="OpenTelemetry Authors">
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

using Xunit;

namespace ProtoBench
{
    public class UnitTest
    {
        private (string name, object value)[] resources;
        private (string name, string value)[] labels;

        public UnitTest()
        {
            this.resources = new (string name, object value)[]
            {
                ("StartTimeUnixnano", 12345678L),
                ("Pid", 1234L),
                ("HostName", "fakehost"),
                ("ServiceName", "generator"),
            };

            this.labels = new (string name, string value)[]
            {
                ("label1", "val1"),
                ("label2", "val2"),
            };
        }

        [Fact]
        public void ProtoOld()
        {
            var bytes = global::ProtoBench.Otlp040.EncodeGauge(false, this.resources, this.labels, 1, 100, 1);

            var extracts = global::ProtoBench.Otlp040.Decode(bytes);

            Assert.Equal(305, extracts.Count);
        }

        [Fact]
        public void ProtoNew()
        {
            var bytes = global::ProtoBench.Otlp080.EncodeGauge(false, this.resources, this.labels, 1, 100, 1);

            var extracts = global::ProtoBench.Otlp080.Decode(bytes);

            Assert.Equal(305, extracts.Count);
        }

        [Fact]
        public void ProtoOldSummary()
        {
            var bytes = global::ProtoBench.Otlp040.EncodeSummary(this.resources, this.labels, 1, 100, 1, 10);

            var extracts = global::ProtoBench.Otlp040.Decode(bytes);

            Assert.Equal(405, extracts.Count);
        }

        [Fact]
        public void ProtoNewSummary()
        {
            var bytes = global::ProtoBench.Otlp080.EncodeSummary(this.resources, this.labels, 1, 100, 1, 10);

            var extracts = global::ProtoBench.Otlp080.Decode(bytes);

            Assert.Equal(405, extracts.Count);
        }

        [Fact]
        public void ProtoOldHistogram()
        {
            var bytes = global::ProtoBench.Otlp040.EncodeHistogram(this.resources, this.labels, 1, 100, 1, 10, 20);

            var extracts = global::ProtoBench.Otlp040.Decode(bytes);

            Assert.Equal(1505, extracts.Count);
        }

        [Fact]
        public void ProtoNewHistogram()
        {
            var bytes = global::ProtoBench.Otlp080.EncodeHistogram(this.resources, this.labels, 1, 100, 1, 10, 20);

            var extracts = global::ProtoBench.Otlp080.Decode(bytes);

            Assert.Equal(1505, extracts.Count);
        }
    }
}

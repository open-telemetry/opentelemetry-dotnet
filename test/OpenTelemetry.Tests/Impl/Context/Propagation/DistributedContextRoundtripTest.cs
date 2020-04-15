// <copyright file="DistributedContextRoundtripTest.cs" company="OpenTelemetry Authors">
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

using Xunit;
using System.Collections.Generic;

namespace OpenTelemetry.Context.Propagation.Test
{
    public class DistributedContextRoundtripTest
    {
        private const string K1 = "k1";
        private const string K2 = "k2";
        private const string K3 = "k3";

        private const string V_EMPTY = "";
        private const string V1 = "v1";
        private const string V2 = "v2";
        private const string V3 = "v3";

        private readonly DistributedContextBinarySerializer serializer;

        public DistributedContextRoundtripTest()
        {
            DistributedContext.Carrier = AsyncLocalDistributedContextCarrier.Instance;
            this.serializer = new DistributedContextBinarySerializer();
        }

        [Fact]
        public void TestRoundtripSerialization_NormalTagContext()
        {
            this.TestRoundtripSerialization(DistributedContext.Empty);
            this.TestRoundtripSerialization(DistributedContextBuilder.CreateContext(K1, V1));

            var expected = DistributedContextBuilder.CreateContext(
                new List<CorrelationContextEntry>(3)
                {
                    new CorrelationContextEntry(K1, V1),
                    new CorrelationContextEntry(K2, V2),
                    new CorrelationContextEntry(K3, V3),
                }
            );

            this.TestRoundtripSerialization(expected);
            this.TestRoundtripSerialization(DistributedContextBuilder.CreateContext(K1, V_EMPTY));
        }

        [Fact]
        public void TestRoundtrip_TagContextWithMaximumSize()
        {
            var list = new List<CorrelationContextEntry>();

            for (var i = 0; i < SerializationUtils.TagContextSerializedSizeLimit / 8; i++)
            {
                // Each tag will be with format {key : "0123", value : "0123"}, so the length of it is 8.
                // Add 1024 tags, the total size should just be 8192.

                var str = i.ToString("0000");
                list.Add(new CorrelationContextEntry(str, str));
            }

            this.TestRoundtripSerialization(DistributedContextBuilder.CreateContext(list));
        }

        private void TestRoundtripSerialization(DistributedContext expected)
        {
            var bytes = this.serializer.ToByteArray(expected);
            var actual = this.serializer.FromByteArray(bytes);
            Assert.Equal(expected, actual);
        }
    }
}

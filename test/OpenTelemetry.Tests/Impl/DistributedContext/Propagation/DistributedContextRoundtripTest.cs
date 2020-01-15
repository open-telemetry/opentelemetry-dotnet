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
using System;
using Xunit;
using System.Collections.Generic;

namespace OpenTelemetry.Context.Propagation.Test
{
    public class DistributedContextRoundtripTest
    {

        private static readonly string K1 = "k1";
        private static readonly string K2 = "k2";
        private static readonly string K3 = "k3";

        private static readonly string V_EMPTY = "";
        private static readonly string V1 = "v1";
        private static readonly string V2 = "v2";
        private static readonly string V3 = "v3";

        private readonly DistributedContextBinarySerializer serializer;

        public DistributedContextRoundtripTest()
        {
            DistributedContext.Carrier = AsyncLocalDistributedContextCarrier.Instance;
            serializer = new DistributedContextBinarySerializer();
        }

        [Fact]
        public void TestRoundtripSerialization_NormalTagContext()
        {
            TestRoundtripSerialization(DistributedContext.Empty);
            TestRoundtripSerialization(DistributedContextBuilder.CreateContext(K1, V1));

            DistributedContext expected = DistributedContextBuilder.CreateContext(new List<DistributedContextEntry>(3) {
                                                                          new DistributedContextEntry(K1, V1),
                                                                          new DistributedContextEntry(K2, V2),
                                                                          new DistributedContextEntry(K3, V3),
                                                                 });
            TestRoundtripSerialization(expected);

            TestRoundtripSerialization(DistributedContextBuilder.CreateContext(K1, V_EMPTY));
        }

        [Fact]
        public void TestRoundtrip_TagContextWithMaximumSize()
        {
            List<DistributedContextEntry> list = new List<DistributedContextEntry>();
            for (var i = 0; i < SerializationUtils.TagContextSerializedSizeLimit / 8; i++)
            {
                // Each tag will be with format {key : "0123", value : "0123"}, so the length of it is 8.
                // Add 1024 tags, the total size should just be 8192.
                String str;
                if (i < 10)
                {
                    str = "000" + i;
                }
                else if (i < 100)
                {
                    str = "00" + i;
                }
                else if (i < 1000)
                {
                    str = "0" + i;
                }
                else
                {
                    str = "" + i;
                }

                list.Add(new DistributedContextEntry(str, str));
            }

            TestRoundtripSerialization(DistributedContextBuilder.CreateContext(list));
        }

        private void TestRoundtripSerialization(DistributedContext expected)
        {
            var bytes = serializer.ToByteArray(expected);
            var actual = serializer.FromByteArray(bytes);
            Assert.Equal(expected, actual);
        }
    }
}

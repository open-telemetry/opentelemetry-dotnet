// <copyright file="TraceOptionsTest.cs" company="OpenCensus Authors">
// Copyright 2018, OpenCensus Authors
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

namespace OpenCensus.Trace.Test
{
    using Xunit;

    public class TraceOptionsTest
    {
        private static readonly byte[] firstBytes = { 0xff };
        private static readonly byte[] secondBytes = { 1 };
        private static readonly byte[] thirdBytes = { 6 };

        [Fact]
        public void getOptions()
        {
            Assert.Equal(0, TraceOptions.Default.Options);
            Assert.Equal(0, TraceOptions.Builder().SetIsSampled(false).Build().Options);
            Assert.Equal(1, TraceOptions.Builder().SetIsSampled(true).Build().Options);
            Assert.Equal(0, TraceOptions.Builder().SetIsSampled(true).SetIsSampled(false).Build().Options);
            Assert.Equal(-1, TraceOptions.FromBytes(firstBytes).Options);
            Assert.Equal(1, TraceOptions.FromBytes(secondBytes).Options);
            Assert.Equal(6, TraceOptions.FromBytes(thirdBytes).Options);
        }

        [Fact]
        public void IsSampled()
        {
            Assert.False(TraceOptions.Default.IsSampled);
            Assert.True(TraceOptions.Builder().SetIsSampled(true).Build().IsSampled);
        }

        [Fact]
        public void ToFromBytes()
        {
            Assert.Equal(firstBytes, TraceOptions.FromBytes(firstBytes).Bytes);
            Assert.Equal(secondBytes, TraceOptions.FromBytes(secondBytes).Bytes);
            Assert.Equal(thirdBytes, TraceOptions.FromBytes(thirdBytes).Bytes);
        }

        [Fact]
        public void Builder_FromOptions()
        {
            Assert.Equal(6 | 1,
                    TraceOptions.Builder(TraceOptions.FromBytes(thirdBytes))
                        .SetIsSampled(true)
                        .Build()
                        .Options);
        }

        [Fact]
        public void traceOptions_EqualsAndHashCode()
        {
            // EqualsTester tester = new EqualsTester();
            // tester.addEqualityGroup(TraceOptions.DEFAULT);
            // tester.addEqualityGroup(
            //    TraceOptions.FromBytes(secondBytes), TraceOptions.Builder().SetIsSampled(true).build());
            // tester.addEqualityGroup(TraceOptions.FromBytes(firstBytes));
            // tester.testEquals();
        }

        [Fact]
        public void traceOptions_ToString()
        {
            Assert.Contains("sampled=False", TraceOptions.Default.ToString());
            Assert.Contains("sampled=True", TraceOptions.Builder().SetIsSampled(true).Build().ToString());
        }
    }
}

// <copyright file="TracestateUtilsTests.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Context.Propagation.Tests
{
    public class TracestateUtilsTests
    {
        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData(" ")]
        [InlineData("\t")]
        public void EmptyTracestate(string tracestate)
        {
            var tracestateEntries = new List<KeyValuePair<string, string>>();
            Assert.False(TraceStateUtilsNew.AppendTraceState(tracestate, tracestateEntries));
            Assert.Empty(tracestateEntries);
        }

        [Theory]
        [InlineData("k=")]
        [InlineData("=v")]
        [InlineData("kv")]
        [InlineData("k =v")]
        [InlineData("k\t=v")]
        [InlineData("k=v,k=v")]
        [InlineData("k1=v1,,,k2=v2")]
        [InlineData("k=morethan256......................................................................................................................................................................................................................................................")]
        [InlineData("v=morethan256......................................................................................................................................................................................................................................................")]
        public void InvalidTracestate(string tracestate)
        {
            var tracestateEntries = new List<KeyValuePair<string, string>>();
            Assert.False(TraceStateUtilsNew.AppendTraceState(tracestate, tracestateEntries));
            Assert.Empty(tracestateEntries);
        }

        [Fact]
        public void MaxEntries()
        {
            var tracestateEntries = new List<KeyValuePair<string, string>>();
            var tracestate =
                "k0=v,k1=v,k2=v,k3=v,k4=v,k5=v,k6=v,k7=v1,k8=v,k9=v,k10=v,k11=v,k12=v,k13=v,k14=v,k15=v,k16=v,k17=v,k18=v,k19=v,k20=v,k21=v,k22=v,k23=v,k24=v,k25=v,k26=v,k27=v1,k28=v,k29=v,k30=v,k31=v";
            Assert.True(TraceStateUtilsNew.AppendTraceState(tracestate, tracestateEntries));
            Assert.Equal(32, tracestateEntries.Count);
            Assert.Equal(
                "k0=v,k1=v,k2=v,k3=v,k4=v,k5=v,k6=v,k7=v1,k8=v,k9=v,k10=v,k11=v,k12=v,k13=v,k14=v,k15=v,k16=v,k17=v,k18=v,k19=v,k20=v,k21=v,k22=v,k23=v,k24=v,k25=v,k26=v,k27=v1,k28=v,k29=v,k30=v,k31=v",
                TraceStateUtilsNew.GetString(tracestateEntries));
        }

        [Fact]
        public void TooManyEntries()
        {
            var tracestateEntries = new List<KeyValuePair<string, string>>();
            var tracestate =
                "k0=v,k1=v,k2=v,k3=v,k4=v,k5=v,k6=v,k7=v1,k8=v,k9=v,k10=v,k11=v,k12=v,k13=v,k14=v,k15=v,k16=v,k17=v,k18=v,k19=v,k20=v,k21=v,k22=v,k23=v,k24=v,k25=v,k26=v,k27=v1,k28=v,k29=v,k30=v,k31=v,k32=v";
            Assert.False(TraceStateUtilsNew.AppendTraceState(tracestate, tracestateEntries));
            Assert.Empty(tracestateEntries);
        }

        [Theory]
        [InlineData("k=v", "k", "v")]
        [InlineData(" k=v ", "k", "v")]
        [InlineData("\tk=v", "k", "v")]
        [InlineData(" k= v ", "k", "v")]
        [InlineData(",k=v,", "k", "v")]
        [InlineData(", k= v, ", "k", "v")]
        [InlineData("k=\tv", "k", "v")]
        [InlineData("k=v\t", "k", "v")]
        [InlineData("1k=v", "1k", "v")]
        public void ValidPair(string pair, string expectedKey, string expectedValue)
        {
            var tracestateEntries = new List<KeyValuePair<string, string>>();
            Assert.True(TraceStateUtilsNew.AppendTraceState(pair, tracestateEntries));
            Assert.Single(tracestateEntries);
            Assert.Equal(new KeyValuePair<string, string>(expectedKey, expectedValue), tracestateEntries.Single());
            Assert.Equal($"{expectedKey}={expectedValue}", TraceStateUtilsNew.GetString(tracestateEntries));
        }

        [Theory]
        [InlineData("k1=v1,k2=v2")]
        [InlineData(" k1=v1 , k2=v2")]
        [InlineData(" ,k1=v1,k2=v2")]
        [InlineData("k1=v1,k2=v2, ")]
        public void ValidPairs(string tracestate)
        {
            var tracestateEntries = new List<KeyValuePair<string, string>>();
            Assert.True(TraceStateUtilsNew.AppendTraceState(tracestate, tracestateEntries));
            Assert.Equal(2, tracestateEntries.Count);
            Assert.Contains(new KeyValuePair<string, string>("k1", "v1"), tracestateEntries);
            Assert.Contains(new KeyValuePair<string, string>("k2", "v2"), tracestateEntries);

            Assert.Equal("k1=v1,k2=v2", TraceStateUtilsNew.GetString(tracestateEntries));
        }
    }
}

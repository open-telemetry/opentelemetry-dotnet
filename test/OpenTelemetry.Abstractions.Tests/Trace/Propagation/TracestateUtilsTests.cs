// <copyright file="StringExtensionsTests.cs" company="OpenTelemetry Authors">
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
using System.Linq;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Tests.Impl.Trace.Propagation
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
            var builder = Tracestate.Builder;
            Assert.True(TracestateUtils.TryExtractTracestate(tracestate, builder));
            Assert.Empty(builder.Build().Entries);
        }

        [Theory]
        [InlineData("k=", 0)]
        [InlineData("=v", 0)]
        [InlineData("kv", 0)]
        [InlineData("k=v,k=v", 1)]
        [InlineData("k1=v1,,,k2=v2", 1)]
        [InlineData("k=morethan256......................................................................................................................................................................................................................................................", 0)]
        [InlineData("v=morethan256......................................................................................................................................................................................................................................................", 0)]
        public void InvalidTracestate(string tracestate, int validEntriesCount)
        {
            var builder = Tracestate.Builder;
            Assert.False(TracestateUtils.TryExtractTracestate(tracestate, builder));
            Assert.Equal(validEntriesCount, builder.Build().Entries.Count());
        }

        [Fact]
        public void TooManyEntries()
        {
            var builder = Tracestate.Builder;
            var tracestate =
                "k0=v,k1=v,k2=v,k3=v,k4=v,k5=v,k6=v,k7=v1,k8=v,k9=v,k10=v,k11=v,k12=v,k13=v,k14=v,k15=v,k16=v,k17=v,k18=v,k19=v,k20=v,k21=v,k22=v,k23=v,k24=v,k25=v,k26=v,k27=v1,k28=v,k29=v,k30=v,k31=v,k32=v,k33=v";
            Assert.False(TracestateUtils.TryExtractTracestate(tracestate, builder));
            Assert.Throws<ArgumentException>(() => builder.Build());
        }

        [Theory]
        [InlineData("k=v")]
        [InlineData(" k=v ")]
        [InlineData(" k = v ")]
        [InlineData("\tk\t=\tv\t")]
        [InlineData(",k=v,")]
        [InlineData(", k = v, ")]
        public void ValidPair(string pair)
        {
            var builder = Tracestate.Builder;
            Assert.True(TracestateUtils.TryExtractTracestate(pair, builder));
            Assert.Equal("k=v", builder.Build().ToString());
        }

        [Theory]
        [InlineData("k1=v1,k2=v2")]
        [InlineData(" k1=v1 , k2=v2")]
        [InlineData(" ,k1=v1,k2=v2")]
        [InlineData("k1=v1,k2=v2, ")]
        public void ValidPairs(string tracestate)
        {
            var builder = Tracestate.Builder;
            Assert.True(TracestateUtils.TryExtractTracestate(tracestate, builder));
            Assert.Equal("k1=v1,k2=v2", builder.Build().ToString());
        }
    }
}

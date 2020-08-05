// <copyright file="SamplingResultTests.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Tests.Implementation.Trace.Samplers
{
    public class SamplingResultTests
    {
        [Fact]
        public void TestEquality()
        {
            var sample1 = new SamplingResult(true);
            var sample2 = new SamplingResult(true);
            object sample3 = new SamplingResult(true);
            var sample4 = new SamplingResult(false);
            object notSample = 1;
            var sample5 = new SamplingResult(SamplingDecision.NotRecord, new Dictionary<string, object> { { "a", 1 }, { "b", "2" } });
            var sample6 = new SamplingResult(SamplingDecision.NotRecord, new Dictionary<string, object> { { "a", 1 }, { "b", "2" } });

            Assert.True(sample1 == sample2 && sample1.Equals(sample2));
            Assert.True(sample1.Equals(sample3));
            Assert.True(sample5 == sample6 && sample5.Equals(sample6));

            Assert.False(sample1.Equals(notSample));
            Assert.True(sample1 != sample4);
        }

        [Fact]
        public void TestGetHashCode()
        {
            var sample1 = new SamplingResult(true);
            Assert.NotEqual(0, sample1.GetHashCode());
        }
    }
}

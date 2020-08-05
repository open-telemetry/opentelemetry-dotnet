// <copyright file="DistributedContextTest.cs" company="OpenTelemetry Authors">
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
using Xunit;

namespace OpenTelemetry.Context.Test
{
    public class DistributedContextTest
    {
        [Fact]
        public void TestEquality()
        {
            var d1 = DistributedContext.Empty;
            var d2 = DistributedContext.Empty;
            object d3 = DistributedContext.Empty;

            Assert.Equal(d1, d2);
            Assert.True(d1 == d2);
            Assert.True(d1.Equals(d2));
            Assert.True(d1.Equals(d3));
        }

        [Fact]
        public void TestInvalidEquality()
        {
            var d1 = DistributedContext.Empty;
            var d2 = new DistributedContext(new CorrelationContext(new List<CorrelationContextEntry>
            {
                new CorrelationContextEntry("key", "value"),
            }));

            Assert.True(d1 != d2);
        }
    }
}

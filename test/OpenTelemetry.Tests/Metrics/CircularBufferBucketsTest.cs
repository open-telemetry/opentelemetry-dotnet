// <copyright file="CircularBufferBucketsTest.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Metrics.Tests
{
    public class CircularBufferBucketsTest
    {
        [Fact]
        public void BasicOperation()
        {
            var buckets = new CircularBufferBuckets(10);

            Assert.Equal(10, buckets.Capacity);
            Assert.Equal(0, buckets.Size);

            Assert.True(buckets.TryIncrement(0));
            Assert.Equal(1, buckets.Size);
        }
    }
}

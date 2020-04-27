// <copyright file="EvictingQueueTests.cs" company="OpenTelemetry Authors">
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

using System.Linq;
using OpenTelemetry.Trace.Internal;
using Xunit;

namespace OpenTelemetry.Tests
{
    public class EvictingQueueTests
    {
        [Fact]
        public void NoEviction_Max()
        {
            var eq = new EvictingQueue<int>(4);
            Assert.Equal(0, eq.Count);
            Assert.Equal(0, eq.DroppedItems);

            eq.Add(0);
            Assert.Equal(1, eq.Count);
            Assert.Equal(0, eq.DroppedItems);
            eq.Add(1);
            Assert.Equal(2, eq.Count);
            Assert.Equal(0, eq.DroppedItems);
            eq.Add(2);
            Assert.Equal(3, eq.Count);
            Assert.Equal(0, eq.DroppedItems);
            eq.Add(3);
            Assert.Equal(4, eq.Count);
            Assert.Equal(0, eq.DroppedItems);

            var items = eq.ToArray();
            Assert.Equal(4, items.Length);
            Assert.Equal(0, items[0]);
            Assert.Equal(1, items[1]);
            Assert.Equal(2, items[2]);
            Assert.Equal(3, items[3]);
        }

        [Fact]
        public void NoEviction()
        {
            var eq = new EvictingQueue<int>(4) { 0, 1, 2 };

            var items = eq.ToArray();
            Assert.Equal(3, items.Length);
            Assert.Equal(0, items[0]);
            Assert.Equal(1, items[1]);
            Assert.Equal(2, items[2]);
        }

        [Fact]
        public void Eviction()
        {
            var eq = new EvictingQueue<int>(4) { 0, 1, 2, 3, 4 };
            Assert.Equal(4, eq.Count);
            Assert.Equal(1, eq.DroppedItems);

            var items = eq.ToArray();
            Assert.Equal(4, items.Length);
            Assert.Equal(1, items[0]);
            Assert.Equal(2, items[1]);
            Assert.Equal(3, items[2]);
            Assert.Equal(4, items[3]);
        }

        [Fact]
        public void MaxItems0()
        {
            var eq = new EvictingQueue<int>(0) { 0 };
            Assert.Equal(0, eq.Count);
            Assert.Equal(1, eq.DroppedItems);

            Assert.Empty(eq);
        }

        [Fact]
        public void Replacing()
        {
            var eq = new EvictingQueue<int>(1) { 0 };
            eq.Replace(0, 1);

            var items = eq.ToArray();

            Assert.Equal(1, items[0]);
        }

        [Fact]
        public void NoReplacing()
        {
            var eq = new EvictingQueue<int>(1) { 0 };
            eq.Replace(1, 1);

            var items = eq.ToArray();

            Assert.Equal(0, items[0]);
        }
    }
}

// <copyright file="DistributedContextTest.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using Xunit;

namespace OpenTelemetry.Context.Test
{
    public class DistributedContextTest
    {
        private static readonly string K1 = "k1";
        private static readonly string K2 = "k2";

        private static readonly string V1 = "v1";
        private static readonly string V2 = "v2";

        public DistributedContextTest()
        {
            DistributedContext.Carrier = AsyncLocalDistributedContextCarrier.Instance;
        }

        [Fact]
        public void EmptyContext()
        {
            DistributedContext dc = DistributedContextBuilder.CreateContext(new List<DistributedContextEntry>());
            Assert.Empty(dc.Entries);
            Assert.Equal(DistributedContext.Empty, dc);
        }

        [Fact]
        public void NonEmptyContext()
        {
            List<DistributedContextEntry> list = new List<DistributedContextEntry>(2) { new DistributedContextEntry(K1, V1), new DistributedContextEntry(K2, V2) };
            DistributedContext dc = DistributedContextBuilder.CreateContext(list);
            Assert.Equal(list, dc.Entries);
        }

        [Fact]
        public void AddExtraKey()
        {
            List<DistributedContextEntry> list = new List<DistributedContextEntry>(1) { new DistributedContextEntry(K1, V1)};
            DistributedContext dc = DistributedContextBuilder.CreateContext(list);
            Assert.Equal(list, dc.Entries);

            list.Add(new DistributedContextEntry(K2, V2));
            DistributedContext dc1 = DistributedContextBuilder.CreateContext(list);
            Assert.Equal(list, dc1.Entries);
        }

        [Fact]
        public void AddExistingKey()
        {
            List<DistributedContextEntry> list = new List<DistributedContextEntry>(2) { new DistributedContextEntry(K1, V1), new DistributedContextEntry(K1, V2) };
            DistributedContext dc = DistributedContextBuilder.CreateContext(list);
            Assert.Equal(new List<DistributedContextEntry>(1) { new DistributedContextEntry(K1, V2) }, dc.Entries);
        }

        [Fact]
        public void UseDefaultEntry()
        {
            Assert.Equal(DistributedContext.Empty, DistributedContextBuilder.CreateContext(new List<DistributedContextEntry>(1) { default }));
            Assert.Equal(DistributedContext.Empty, DistributedContextBuilder.CreateContext(null));
        }

        [Fact]
        public void RemoveExistingKey()
        {
            List<DistributedContextEntry> list = new List<DistributedContextEntry>(2) { new DistributedContextEntry(K1, V1), new DistributedContextEntry(K2, V2) };
            DistributedContext dc = DistributedContextBuilder.CreateContext(list);
            Assert.Equal(list, dc.Entries);

            list.RemoveAt(0);

            dc = DistributedContextBuilder.CreateContext(list);
            Assert.Equal(list, dc.Entries);

            list.Clear();
            dc = DistributedContextBuilder.CreateContext(list);
            Assert.Equal(DistributedContext.Empty, dc);
        }

        [Fact]
        public void TestIterator()
        {
            List<DistributedContextEntry> list = new List<DistributedContextEntry>(2) { new DistributedContextEntry(K1, V1), new DistributedContextEntry(K2, V2) };
            DistributedContext dc = DistributedContextBuilder.CreateContext(list);

            var i = dc.Entries.GetEnumerator();
            Assert.True(i.MoveNext());
            var tag1 = i.Current;
            Assert.True(i.MoveNext());
            var tag2 = i.Current;
            Assert.False(i.MoveNext());
            Assert.Equal(new List<DistributedContextEntry>() { new DistributedContextEntry(K1, V1), new DistributedContextEntry(K2, V2)}, new List<DistributedContextEntry>() { tag1, tag2 });
        }

        [Fact]
        public void TestEquals()
        {
            DistributedContext dc1 = DistributedContextBuilder.CreateContext(new List<DistributedContextEntry>(2) { new DistributedContextEntry(K1, V1), new DistributedContextEntry(K2, V2) });
            DistributedContext dc2 = DistributedContextBuilder.CreateContext(new List<DistributedContextEntry>(2) { new DistributedContextEntry(K1, V1), new DistributedContextEntry(K2, V2) });
            DistributedContext dc3 = DistributedContextBuilder.CreateContext(new List<DistributedContextEntry>(2) { new DistributedContextEntry(K2, V2), new DistributedContextEntry(K1, V1) });
            DistributedContext dc4 = DistributedContextBuilder.CreateContext(new List<DistributedContextEntry>(2) { new DistributedContextEntry(K1, V1), new DistributedContextEntry(K2, V1) });
            DistributedContext dc5 = DistributedContextBuilder.CreateContext(new List<DistributedContextEntry>(2) { new DistributedContextEntry(K1, V2), new DistributedContextEntry(K2, V1) });

            Assert.True(dc1.Equals(dc2));
            Assert.True(dc1.Equals(dc3));

            Assert.False(dc1.Equals(dc4));
            Assert.False(dc2.Equals(dc4));
            Assert.False(dc3.Equals(dc4));
            Assert.False(dc5.Equals(dc4));
            Assert.False(dc4.Equals(dc5));
        }
    }
}

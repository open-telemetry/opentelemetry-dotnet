// <copyright file="CorrelationContextTest.cs" company="OpenTelemetry Authors">
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
    public class CorrelationContextTest
    {
        private static readonly string K1 = "k1";
        private static readonly string K2 = "k2";

        private static readonly string V1 = "v1";
        private static readonly string V2 = "v2";

        public CorrelationContextTest()
        {
            DistributedContext.Carrier = AsyncLocalDistributedContextCarrier.Instance;
        }

        [Fact]
        public void EmptyContext()
        {
            CorrelationContext dc = CorrelationContextBuilder.CreateContext(new List<CorrelationContextEntry>());
            Assert.Empty(dc.Entries);
            Assert.Equal(CorrelationContext.Empty, dc);
        }

        [Fact]
        public void NonEmptyContext()
        {
            List<CorrelationContextEntry> list = new List<CorrelationContextEntry>(2) { new CorrelationContextEntry(K1, V1), new CorrelationContextEntry(K2, V2) };
            CorrelationContext dc = CorrelationContextBuilder.CreateContext(list);
            Assert.Equal(list, dc.Entries);
        }

        [Fact]
        public void AddExtraKey()
        {
            List<CorrelationContextEntry> list = new List<CorrelationContextEntry>(1) { new CorrelationContextEntry(K1, V1)};
            CorrelationContext dc = CorrelationContextBuilder.CreateContext(list);
            Assert.Equal(list, dc.Entries);

            list.Add(new CorrelationContextEntry(K2, V2));
            CorrelationContext dc1 = CorrelationContextBuilder.CreateContext(list);
            Assert.Equal(list, dc1.Entries);
        }

        [Fact]
        public void AddExistingKey()
        {
            List<CorrelationContextEntry> list = new List<CorrelationContextEntry>(2) { new CorrelationContextEntry(K1, V1), new CorrelationContextEntry(K1, V2) };
            CorrelationContext dc = CorrelationContextBuilder.CreateContext(list);
            Assert.Equal(new List<CorrelationContextEntry>(1) { new CorrelationContextEntry(K1, V2) }, dc.Entries);
        }

        [Fact]
        public void UseDefaultEntry()
        {
            Assert.Equal(CorrelationContext.Empty, CorrelationContextBuilder.CreateContext(new List<CorrelationContextEntry>(1) { default }));
            Assert.Equal(CorrelationContext.Empty, CorrelationContextBuilder.CreateContext(null));
        }

        [Fact]
        public void RemoveExistingKey()
        {
            List<CorrelationContextEntry> list = new List<CorrelationContextEntry>(2) { new CorrelationContextEntry(K1, V1), new CorrelationContextEntry(K2, V2) };
            CorrelationContext dc = CorrelationContextBuilder.CreateContext(list);
            Assert.Equal(list, dc.Entries);

            list.RemoveAt(0);

            dc = CorrelationContextBuilder.CreateContext(list);
            Assert.Equal(list, dc.Entries);

            list.Clear();
            dc = CorrelationContextBuilder.CreateContext(list);
            Assert.Equal(CorrelationContext.Empty, dc);
        }

        [Fact]
        public void TestIterator()
        {
            List<CorrelationContextEntry> list = new List<CorrelationContextEntry>(2) { new CorrelationContextEntry(K1, V1), new CorrelationContextEntry(K2, V2) };
            CorrelationContext dc = CorrelationContextBuilder.CreateContext(list);

            var i = dc.Entries.GetEnumerator();
            Assert.True(i.MoveNext());
            var tag1 = i.Current;
            Assert.True(i.MoveNext());
            var tag2 = i.Current;
            Assert.False(i.MoveNext());
            Assert.Equal(new List<CorrelationContextEntry>() { new CorrelationContextEntry(K1, V1), new CorrelationContextEntry(K2, V2)}, new List<CorrelationContextEntry>() { tag1, tag2 });
        }

        [Fact]
        public void TestEquals()
        {
            CorrelationContext dc1 = CorrelationContextBuilder.CreateContext(new List<CorrelationContextEntry>(2) { new CorrelationContextEntry(K1, V1), new CorrelationContextEntry(K2, V2) });
            CorrelationContext dc2 = CorrelationContextBuilder.CreateContext(new List<CorrelationContextEntry>(2) { new CorrelationContextEntry(K1, V1), new CorrelationContextEntry(K2, V2) });
            CorrelationContext dc3 = CorrelationContextBuilder.CreateContext(new List<CorrelationContextEntry>(2) { new CorrelationContextEntry(K2, V2), new CorrelationContextEntry(K1, V1) });
            CorrelationContext dc4 = CorrelationContextBuilder.CreateContext(new List<CorrelationContextEntry>(2) { new CorrelationContextEntry(K1, V1), new CorrelationContextEntry(K2, V1) });
            CorrelationContext dc5 = CorrelationContextBuilder.CreateContext(new List<CorrelationContextEntry>(2) { new CorrelationContextEntry(K1, V2), new CorrelationContextEntry(K2, V1) });

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

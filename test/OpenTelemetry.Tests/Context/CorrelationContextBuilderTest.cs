// <copyright file="CorrelationContextBuilderTest.cs" company="OpenTelemetry Authors">
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
    public class CorrelationContextBuilderTest
    {
        private const string Key1 = "key 1";
        private const string Key2 = "key 2";

        private const string Value1 = "value 1";
        private const string Value2 = "value 2";

        private static readonly List<CorrelationContextEntry> List1 = new List<CorrelationContextEntry>(1)
            { new CorrelationContextEntry(Key1, Value1) };

        private static readonly List<CorrelationContextEntry> List2 = new List<CorrelationContextEntry>(2)
        {
            new CorrelationContextEntry(Key1, Value1),
            new CorrelationContextEntry(Key2, Value2),
        };

        public CorrelationContextBuilderTest()
        {
            DistributedContext.Carrier = AsyncLocalDistributedContextCarrier.Instance;
        }

        [Fact]
        public void ContextCreation()
        {
            CorrelationContext dc = CorrelationContextBuilder.CreateContext(null);
            Assert.Equal(CorrelationContext.Empty, dc);

            dc = CorrelationContextBuilder.CreateContext(CorrelationContext.Empty.Entries);
            Assert.Equal(CorrelationContext.Empty, dc);

            dc = CorrelationContextBuilder.CreateContext(Key1, Value1);
            Assert.Equal(CorrelationContextBuilder.CreateContext(List1), dc);

            Assert.Equal(dc, new CorrelationContextBuilder(dc).Build());
        }

        [Fact]
        public void AddEntries()
        {
            Assert.Equal(CorrelationContext.Empty, new CorrelationContextBuilder(inheritCurrentContext: false).Build());

            Assert.Equal(
                CorrelationContextBuilder.CreateContext(List1), new CorrelationContextBuilder(inheritCurrentContext: false)
                    .Add(Key1, Value1)
                    .Build());

            Assert.Equal(
                CorrelationContextBuilder.CreateContext(List1), new CorrelationContextBuilder(inheritCurrentContext: false)
                    .Add(new CorrelationContextEntry(Key1, Value1))
                    .Build());

            Assert.Equal(
                CorrelationContextBuilder.CreateContext(List2), new CorrelationContextBuilder(inheritCurrentContext: false)
                    .Add(Key1, Value1)
                    .Add(Key2, Value2)
                    .Build());

            Assert.Equal(
                CorrelationContextBuilder.CreateContext(List2), new CorrelationContextBuilder(inheritCurrentContext: false)
                    .Add(new CorrelationContextEntry(Key1, Value1))
                    .Add(new CorrelationContextEntry(Key2, Value2))
                    .Build());

            Assert.Equal(
                CorrelationContextBuilder.CreateContext(List1), new CorrelationContextBuilder(inheritCurrentContext: false)
                    .Add(List1)
                    .Build());

            Assert.Equal(
                CorrelationContextBuilder.CreateContext(List2), new CorrelationContextBuilder(inheritCurrentContext: false)
                    .Add(List2)
                    .Build());
        }

        [Fact]
        public void RemoveEntries()
        {
            Assert.Equal(
                CorrelationContextBuilder.CreateContext(List1), new CorrelationContextBuilder(inheritCurrentContext: false)
                    .Add(List2)
                    .Remove(Key2)
                    .Build());

            Assert.Equal(
                CorrelationContext.Empty, new CorrelationContextBuilder(inheritCurrentContext: false)
                    .Add(List2)
                    .Remove(Key2)
                    .Remove(Key1)
                    .Build());
        }

        [Fact]
        public void EnsureEmptyListAfterBuild()
        {
            var dcb = new CorrelationContextBuilder(inheritCurrentContext: false);
            Assert.Equal(CorrelationContext.Empty, dcb.Build());

            dcb.Add(List2);
            Assert.Equal(CorrelationContextBuilder.CreateContext(List2), dcb.Build());
            Assert.Equal(CorrelationContext.Empty, dcb.Build());

            var dc = dcb.Add(List1).Build();
            Assert.Equal(dc, dcb.Add(List1).Build());

            dcb = new CorrelationContextBuilder(dc);
            Assert.Equal(dc, dcb.Build());
            Assert.Equal(CorrelationContext.Empty, dcb.Build());
        }
    }
}

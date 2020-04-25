// <copyright file="CorrelationContextBuilderTest.cs" company="OpenTelemetry Authors">
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

using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace OpenTelemetry.Context.Test
{
    public class CorrelationContextBuilderTest
    {
        private const string KEY_1 = "key 1";
        private const string KEY_2 = "key 2";

        private const string VALUE_1 = "value 1";
        private const string VALUE_2 = "value 2";

        private static readonly List<CorrelationContextEntry> List1 = new List<CorrelationContextEntry>(1)
            {new CorrelationContextEntry(KEY_1, VALUE_1)};

        private static readonly List<CorrelationContextEntry> List2 = new List<CorrelationContextEntry>(2)
        {
            new CorrelationContextEntry(KEY_1, VALUE_1),
            new CorrelationContextEntry(KEY_2, VALUE_2),
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

            dc = CorrelationContextBuilder.CreateContext(KEY_1, VALUE_1);
            Assert.Equal(CorrelationContextBuilder.CreateContext(List1), dc);

            Assert.Equal(dc, new CorrelationContextBuilder(dc).Build());
        }

        [Fact]
        public void AddEntries()
        {
            Assert.Equal(CorrelationContext.Empty, new CorrelationContextBuilder(inheritCurrentContext: false).Build());

            Assert.Equal(
                CorrelationContextBuilder.CreateContext(List1), new CorrelationContextBuilder(inheritCurrentContext: false)
                    .Add(KEY_1, VALUE_1)
                    .Build()
            );

            Assert.Equal(
                CorrelationContextBuilder.CreateContext(List1), new CorrelationContextBuilder(inheritCurrentContext: false)
                    .Add(new CorrelationContextEntry(KEY_1, VALUE_1))
                    .Build()
            );

            Assert.Equal(
                CorrelationContextBuilder.CreateContext(List2), new CorrelationContextBuilder(inheritCurrentContext: false)
                    .Add(KEY_1, VALUE_1)
                    .Add(KEY_2, VALUE_2)
                    .Build()
            );

            Assert.Equal(
                CorrelationContextBuilder.CreateContext(List2), new CorrelationContextBuilder(inheritCurrentContext: false)
                    .Add(new CorrelationContextEntry(KEY_1, VALUE_1))
                    .Add(new CorrelationContextEntry(KEY_2, VALUE_2))
                    .Build()
            );

            Assert.Equal(
                CorrelationContextBuilder.CreateContext(List1), new CorrelationContextBuilder(inheritCurrentContext: false)
                    .Add(List1)
                    .Build()
            );

            Assert.Equal(
                CorrelationContextBuilder.CreateContext(List2), new CorrelationContextBuilder(inheritCurrentContext: false)
                    .Add(List2)
                    .Build()
            );
        }

        [Fact]
        public void RemoveEntries()
        {
            Assert.Equal(
                CorrelationContextBuilder.CreateContext(List1), new CorrelationContextBuilder(inheritCurrentContext: false)
                    .Add(List2)
                    .Remove(KEY_2)
                    .Build()
            );

            Assert.Equal(
                CorrelationContext.Empty, new CorrelationContextBuilder(inheritCurrentContext: false)
                    .Add(List2)
                    .Remove(KEY_2)
                    .Remove(KEY_1)
                    .Build()
            );
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

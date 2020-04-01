// <copyright file="DistributedContextBuilderTest.cs" company="OpenTelemetry Authors">
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
    public class DistributedContextBuilderTest
    {
        private const string KEY_1 = "key 1";
        private const string KEY_2 = "key 2";

        private const string VALUE_1 = "value 1";
        private const string VALUE_2 = "value 2";

        private static readonly List<DistributedContextEntry> List1 = new List<DistributedContextEntry>(1)
            {new DistributedContextEntry(KEY_1, VALUE_1)};

        private static readonly List<DistributedContextEntry> List2 = new List<DistributedContextEntry>(2)
        {
            new DistributedContextEntry(KEY_1, VALUE_1),
            new DistributedContextEntry(KEY_2, VALUE_2),
        };

        public DistributedContextBuilderTest()
        {
            DistributedContext.Carrier = AsyncLocalDistributedContextCarrier.Instance;
        }

        [Fact]
        public void ContextCreation()
        {
            DistributedContext dc = DistributedContextBuilder.CreateContext(null);
            Assert.Equal(DistributedContext.Empty, dc);

            dc = DistributedContextBuilder.CreateContext(DistributedContext.Empty.Entries);
            Assert.Equal(DistributedContext.Empty, dc);

            dc = DistributedContextBuilder.CreateContext(KEY_1, VALUE_1);
            Assert.Equal(DistributedContextBuilder.CreateContext(List1), dc);

            Assert.Equal(dc, new DistributedContextBuilder(dc).Build());
        }

        [Fact]
        public void AddEntries()
        {
            Assert.Equal(DistributedContext.Empty, new DistributedContextBuilder(inheritCurrentContext: false).Build());

            Assert.Equal(
                DistributedContextBuilder.CreateContext(List1), new DistributedContextBuilder(inheritCurrentContext: false)
                    .Add(KEY_1, VALUE_1)
                    .Build()
            );

            Assert.Equal(
                DistributedContextBuilder.CreateContext(List1), new DistributedContextBuilder(inheritCurrentContext: false)
                    .Add(new DistributedContextEntry(KEY_1, VALUE_1))
                    .Build()
            );

            Assert.Equal(
                DistributedContextBuilder.CreateContext(List2), new DistributedContextBuilder(inheritCurrentContext: false)
                    .Add(KEY_1, VALUE_1)
                    .Add(KEY_2, VALUE_2)
                    .Build()
            );

            Assert.Equal(
                DistributedContextBuilder.CreateContext(List2), new DistributedContextBuilder(inheritCurrentContext: false)
                    .Add(new DistributedContextEntry(KEY_1, VALUE_1))
                    .Add(new DistributedContextEntry(KEY_2, VALUE_2))
                    .Build()
            );

            Assert.Equal(
                DistributedContextBuilder.CreateContext(List1), new DistributedContextBuilder(inheritCurrentContext: false)
                    .Add(List1)
                    .Build()
            );

            Assert.Equal(
                DistributedContextBuilder.CreateContext(List2), new DistributedContextBuilder(inheritCurrentContext: false)
                    .Add(List2)
                    .Build()
            );
        }

        [Fact]
        public void RemoveEntries()
        {
            Assert.Equal(
                DistributedContextBuilder.CreateContext(List1), new DistributedContextBuilder(inheritCurrentContext: false)
                    .Add(List2)
                    .Remove(KEY_2)
                    .Build()
            );

            Assert.Equal(
                DistributedContext.Empty, new DistributedContextBuilder(inheritCurrentContext: false)
                    .Add(List2)
                    .Remove(KEY_2)
                    .Remove(KEY_1)
                    .Build()
            );
        }

        [Fact]
        public void EnsureEmptyListAfterBuild()
        {
            var dcb = new DistributedContextBuilder(inheritCurrentContext: false);
            Assert.Equal(DistributedContext.Empty, dcb.Build());

            dcb.Add(List2);
            Assert.Equal(DistributedContextBuilder.CreateContext(List2), dcb.Build());
            Assert.Equal(DistributedContext.Empty, dcb.Build());

            var dc = dcb.Add(List1).Build();
            Assert.Equal(dc, dcb.Add(List1).Build());

            dcb = new DistributedContextBuilder(dc);
            Assert.Equal(dc, dcb.Build());
            Assert.Equal(DistributedContext.Empty, dcb.Build());
        }
    }
}

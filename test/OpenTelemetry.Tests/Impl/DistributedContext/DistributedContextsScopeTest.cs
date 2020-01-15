// <copyright file="DistributedContextsScopeTest.cs" company="OpenTelemetry Authors">
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
    public class DistributedContextsScopeTest
    {
        private static readonly string KEY_1 = "key 1";
        private static readonly string KEY_2 = "key 2";

        private static readonly string VALUE_1 = "value 1";
        private static readonly string VALUE_2 = "value 2";

        [Fact]
        public void NoopContextCarrier()
        {
            DistributedContext.Carrier = NoopDistributedContextCarrier.Instance;
            List<DistributedContextEntry> list = new List<DistributedContextEntry>(2)
            {
                new DistributedContextEntry(KEY_1, VALUE_1), new DistributedContextEntry(KEY_2, VALUE_2),
            };
            Assert.Equal(DistributedContext.Empty, DistributedContext.Current);

            using (DistributedContext.SetCurrent(DistributedContextBuilder.CreateContext(KEY_1, VALUE_1)))
            {
                Assert.Equal(DistributedContext.Empty, DistributedContext.Current);
                using (DistributedContext.SetCurrent(DistributedContextBuilder.CreateContext(list)))
                {
                    Assert.Equal(DistributedContext.Empty, DistributedContext.Current);
                }
            }

            Assert.Equal(DistributedContext.Empty, DistributedContext.Current);
        }

        [Fact]
        public async void AsyncContextCarrier()
        {
            DistributedContext.Carrier = AsyncLocalDistributedContextCarrier.Instance;
            List<DistributedContextEntry> list = new List<DistributedContextEntry>(2) { new DistributedContextEntry(KEY_1, VALUE_1), new DistributedContextEntry(KEY_2, VALUE_2), };

            DistributedContext dc1 = DistributedContextBuilder.CreateContext(KEY_1, VALUE_1);
            DistributedContext dc2 = DistributedContextBuilder.CreateContext(list);

            DistributedContext.SetCurrent(DistributedContext.Empty);
            Assert.Equal(DistributedContext.Empty, DistributedContext.Current);

            using (DistributedContext.SetCurrent(dc1))
            {
                Assert.Equal(dc1, DistributedContext.Current);
                using (DistributedContext.SetCurrent(dc2))
                {
                    Assert.Equal(dc2, DistributedContext.Current);
                }

                Assert.Equal(dc1, DistributedContext.Current);

                using (DistributedContext.SetCurrent(dc2))
                {
                    await Task.Run(() => Assert.Equal(dc2, DistributedContext.Current));
                }
                await Task.Run(() => Assert.Equal(dc1, DistributedContext.Current));
            }
            Assert.Equal(DistributedContext.Empty, DistributedContext.Current);
            await Task.Run(() => Assert.Equal(DistributedContext.Empty, DistributedContext.Current));
        }

        [Fact]
        public async void TestContextInheritance()
        {
            DistributedContext.Carrier = AsyncLocalDistributedContextCarrier.Instance;
            List<DistributedContextEntry> list1 = new List<DistributedContextEntry>(1) { new DistributedContextEntry(KEY_1, VALUE_1)};
            List<DistributedContextEntry> list2 = new List<DistributedContextEntry>(2) { new DistributedContextEntry(KEY_1, VALUE_1), new DistributedContextEntry(KEY_2, VALUE_2), };

            DistributedContext.SetCurrent(DistributedContext.Empty);
            await Task.Run(() => Assert.Equal(DistributedContext.Empty, DistributedContext.Current));

            using (DistributedContext.SetCurrent(DistributedContextBuilder.CreateContext(list1)))
            {
                await Task.Run(() => Assert.Equal(DistributedContextBuilder.CreateContext(list1), DistributedContext.Current));

                using (DistributedContext.SetCurrent(new DistributedContextBuilder(inheritCurrentContext: true).Build()))
                {
                    await Task.Run(() => Assert.Equal(DistributedContextBuilder.CreateContext(list1), DistributedContext.Current));

                    using (DistributedContext.SetCurrent(new DistributedContextBuilder(inheritCurrentContext: true).Add(KEY_2, VALUE_2).Build()))
                    {
                        await Task.Run(() => Assert.Equal(DistributedContextBuilder.CreateContext(list2), DistributedContext.Current));
                        using (DistributedContext.SetCurrent(new DistributedContextBuilder(inheritCurrentContext: true).Remove(KEY_2).Build()))
                        {
                            await Task.Run(() => Assert.Equal(DistributedContextBuilder.CreateContext(list1), DistributedContext.Current));
                        }
                    }

                    await Task.Run(() => Assert.Equal(DistributedContextBuilder.CreateContext(list1), DistributedContext.Current));

                    using (DistributedContext.SetCurrent(new DistributedContextBuilder(inheritCurrentContext: false).Build()))
                    {
                        await Task.Run(() => Assert.Equal(DistributedContext.Empty, DistributedContext.Current));
                    }

                    await Task.Run(() => Assert.Equal(DistributedContextBuilder.CreateContext(list1), DistributedContext.Current));
                }
            }
        }
    }
}


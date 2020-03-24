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
            CorrelationContext.Carrier = NoopDistributedContextCarrier.Instance;
            List<CorrelationContextEntry> list = new List<CorrelationContextEntry>(2)
            {
                new CorrelationContextEntry(KEY_1, VALUE_1), new CorrelationContextEntry(KEY_2, VALUE_2),
            };
            Assert.Equal(CorrelationContext.Empty, CorrelationContext.Current);

            using (CorrelationContext.SetCurrent(CorrelationContextBuilder.CreateContext(KEY_1, VALUE_1)))
            {
                Assert.Equal(CorrelationContext.Empty, CorrelationContext.Current);
                using (CorrelationContext.SetCurrent(CorrelationContextBuilder.CreateContext(list)))
                {
                    Assert.Equal(CorrelationContext.Empty, CorrelationContext.Current);
                }
            }

            Assert.Equal(CorrelationContext.Empty, CorrelationContext.Current);
        }

        [Fact]
        public async void AsyncContextCarrier()
        {
            CorrelationContext.Carrier = AsyncLocalDistributedContextCarrier.Instance;
            List<CorrelationContextEntry> list = new List<CorrelationContextEntry>(2) { new CorrelationContextEntry(KEY_1, VALUE_1), new CorrelationContextEntry(KEY_2, VALUE_2), };

            CorrelationContext dc1 = CorrelationContextBuilder.CreateContext(KEY_1, VALUE_1);
            CorrelationContext dc2 = CorrelationContextBuilder.CreateContext(list);

            CorrelationContext.SetCurrent(CorrelationContext.Empty);
            Assert.Equal(CorrelationContext.Empty, CorrelationContext.Current);

            using (CorrelationContext.SetCurrent(dc1))
            {
                Assert.Equal(dc1, CorrelationContext.Current);
                using (CorrelationContext.SetCurrent(dc2))
                {
                    Assert.Equal(dc2, CorrelationContext.Current);
                }

                Assert.Equal(dc1, CorrelationContext.Current);

                using (CorrelationContext.SetCurrent(dc2))
                {
                    await Task.Run(() => Assert.Equal(dc2, CorrelationContext.Current));
                }
                await Task.Run(() => Assert.Equal(dc1, CorrelationContext.Current));
            }
            Assert.Equal(CorrelationContext.Empty, CorrelationContext.Current);
            await Task.Run(() => Assert.Equal(CorrelationContext.Empty, CorrelationContext.Current));
        }

        [Fact]
        public async void TestContextInheritance()
        {
            CorrelationContext.Carrier = AsyncLocalDistributedContextCarrier.Instance;
            List<CorrelationContextEntry> list1 = new List<CorrelationContextEntry>(1) { new CorrelationContextEntry(KEY_1, VALUE_1)};
            List<CorrelationContextEntry> list2 = new List<CorrelationContextEntry>(2) { new CorrelationContextEntry(KEY_1, VALUE_1), new CorrelationContextEntry(KEY_2, VALUE_2), };

            CorrelationContext.SetCurrent(CorrelationContext.Empty);
            await Task.Run(() => Assert.Equal(CorrelationContext.Empty, CorrelationContext.Current));

            using (CorrelationContext.SetCurrent(CorrelationContextBuilder.CreateContext(list1)))
            {
                await Task.Run(() => Assert.Equal(CorrelationContextBuilder.CreateContext(list1), CorrelationContext.Current));

                using (CorrelationContext.SetCurrent(new CorrelationContextBuilder(inheritCurrentContext: true).Build()))
                {
                    await Task.Run(() => Assert.Equal(CorrelationContextBuilder.CreateContext(list1), CorrelationContext.Current));

                    using (CorrelationContext.SetCurrent(new CorrelationContextBuilder(inheritCurrentContext: true).Add(KEY_2, VALUE_2).Build()))
                    {
                        await Task.Run(() => Assert.Equal(CorrelationContextBuilder.CreateContext(list2), CorrelationContext.Current));
                        using (CorrelationContext.SetCurrent(new CorrelationContextBuilder(inheritCurrentContext: true).Remove(KEY_2).Build()))
                        {
                            await Task.Run(() => Assert.Equal(CorrelationContextBuilder.CreateContext(list1), CorrelationContext.Current));
                        }
                    }

                    await Task.Run(() => Assert.Equal(CorrelationContextBuilder.CreateContext(list1), CorrelationContext.Current));

                    using (CorrelationContext.SetCurrent(new CorrelationContextBuilder(inheritCurrentContext: false).Build()))
                    {
                        await Task.Run(() => Assert.Equal(CorrelationContext.Empty, CorrelationContext.Current));
                    }

                    await Task.Run(() => Assert.Equal(CorrelationContextBuilder.CreateContext(list1), CorrelationContext.Current));
                }
            }
        }
    }
}


// <copyright file="DistributedContextsScopeTest.cs" company="OpenTelemetry Authors">
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
using System.Threading.Tasks;
using Xunit;

namespace OpenTelemetry.Context.Test
{
    public class DistributedContextsScopeTest
    {
        private const string Key1 = "key 1";
        private const string Key2 = "key 2";

        private const string Value1 = "value 1";
        private const string Value2 = "value 2";

        [Fact]
        public void NoopContextCarrier()
        {
            DistributedContext.Carrier = NoopDistributedContextCarrier.Instance;
            List<CorrelationContextEntry> list = new List<CorrelationContextEntry>(2)
            {
                new CorrelationContextEntry(Key1, Value1), new CorrelationContextEntry(Key2, Value2),
            };
            Assert.Equal(DistributedContext.Empty, DistributedContext.Current);

            using (DistributedContext.SetCurrent(DistributedContextBuilder.CreateContext(Key1, Value1)))
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
            List<CorrelationContextEntry> list = new List<CorrelationContextEntry>(2) { new CorrelationContextEntry(Key1, Value1), new CorrelationContextEntry(Key2, Value2), };

            var dc1 = DistributedContextBuilder.CreateContext(Key1, Value1);
            var dc2 = DistributedContextBuilder.CreateContext(list);

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
            var list1 = new List<CorrelationContextEntry>(1) { new CorrelationContextEntry(Key1, Value1) };
            var list2 = new List<CorrelationContextEntry>(2) { new CorrelationContextEntry(Key1, Value1), new CorrelationContextEntry(Key2, Value2) };

            DistributedContext.SetCurrent(DistributedContext.Empty);
            await Task.Run(() => Assert.Equal(DistributedContext.Empty, DistributedContext.Current));

            using (DistributedContext.SetCurrent(DistributedContextBuilder.CreateContext(list1)))
            {
                await Task.Run(() => Assert.Equal(DistributedContextBuilder.CreateContext(list1), DistributedContext.Current));

                using (DistributedContext.SetCurrent(new DistributedContextBuilder(inheritCurrentContext: true).Build()))
                {
                    await Task.Run(() => Assert.Equal(DistributedContextBuilder.CreateContext(list1), DistributedContext.Current));

                    using (DistributedContext.SetCurrent(new DistributedContextBuilder(inheritCurrentContext: true).Correlations(b => b.Add(Key2, Value2)).Build()))
                    {
                        await Task.Run(() => Assert.Equal(DistributedContextBuilder.CreateContext(list2), DistributedContext.Current));
                        using (DistributedContext.SetCurrent(new DistributedContextBuilder(inheritCurrentContext: true).Correlations(b => b.Remove(Key2)).Build()))
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

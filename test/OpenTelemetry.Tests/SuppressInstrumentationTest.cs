// <copyright file="SuppressInstrumentationTest.cs" company="OpenTelemetry Authors">
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

using System.Threading.Tasks;
using Xunit;

namespace OpenTelemetry.Tests
{
    public class SuppressInstrumentationTest
    {
        [Fact]
        public static void UsingSuppressInstrumentation()
        {
            Assert.False(OpenTelemetrySdk.SuppressInstrumentation);

            using (var scope = SuppressInstrumentationScope.Begin())
            {
                Assert.True(OpenTelemetrySdk.SuppressInstrumentation);

                using (var innerScope = SuppressInstrumentationScope.Begin())
                {
                    innerScope.Dispose();

                    Assert.True(OpenTelemetrySdk.SuppressInstrumentation);

                    scope.Dispose();
                }

                Assert.False(OpenTelemetrySdk.SuppressInstrumentation);
            }

            Assert.False(OpenTelemetrySdk.SuppressInstrumentation);
        }

        [Theory]
        [InlineData(null)]
        [InlineData(false)]
        [InlineData(true)]
        public void SuppressInstrumentationBeginTest(bool? shouldBegin)
        {
            Assert.False(OpenTelemetrySdk.SuppressInstrumentation);

            using var scope = shouldBegin.HasValue ? SuppressInstrumentationScope.Begin(shouldBegin.Value) : SuppressInstrumentationScope.Begin();
            if (shouldBegin.HasValue)
            {
                Assert.Equal(shouldBegin.Value, OpenTelemetrySdk.SuppressInstrumentation);
            }
            else
            {
                Assert.True(OpenTelemetrySdk.SuppressInstrumentation); // Default behavior is to pass true and suppress the instrumentation
            }
        }

        [Fact]
        public async void SuppressInstrumentationScopeEnterIsLocalToAsyncFlow()
        {
            Assert.False(OpenTelemetrySdk.SuppressInstrumentation);

            // SuppressInstrumentationScope.Enter called inside the task is only applicable to this async flow
            await Task.Factory.StartNew(() =>
            {
                Assert.False(OpenTelemetrySdk.SuppressInstrumentation);
                SuppressInstrumentationScope.Enter();
                Assert.True(OpenTelemetrySdk.SuppressInstrumentation);
            });

            Assert.False(OpenTelemetrySdk.SuppressInstrumentation); // Changes made by SuppressInstrumentationScope.Enter in the task above are not reflected here as it's not part of the same async flow
        }

        [Fact]
        public void DecrementIfTriggeredOnlyWorksInReferenceCountingMode()
        {
            // Instrumentation is not suppressed, DecrementIfTriggered is a no op
            Assert.False(OpenTelemetrySdk.SuppressInstrumentation);
            SuppressInstrumentationScope.DecrementIfTriggered();
            Assert.False(OpenTelemetrySdk.SuppressInstrumentation);

            // Instrumentation is suppressed in reference counting mode, DecrementIfTriggered should work
            SuppressInstrumentationScope.Enter();
            Assert.True(OpenTelemetrySdk.SuppressInstrumentation);
            SuppressInstrumentationScope.DecrementIfTriggered();
            Assert.False(OpenTelemetrySdk.SuppressInstrumentation); // Instrumentation is not suppressed anymore
        }

        [Fact]
        public void IncrementIfTriggeredOnlyWorksInReferenceCountingMode()
        {
            // Instrumentation is not suppressed, IncrementIfTriggered is a no op
            Assert.False(OpenTelemetrySdk.SuppressInstrumentation);
            SuppressInstrumentationScope.IncrementIfTriggered();
            Assert.False(OpenTelemetrySdk.SuppressInstrumentation);

            // Instrumentation is suppressed in reference counting mode, IncrementIfTriggered should work
            SuppressInstrumentationScope.Enter();
            SuppressInstrumentationScope.IncrementIfTriggered();
            Assert.True(OpenTelemetrySdk.SuppressInstrumentation);
            SuppressInstrumentationScope.DecrementIfTriggered();
            Assert.True(OpenTelemetrySdk.SuppressInstrumentation); // Instrumentation is still suppressed as IncrementIfTriggered incremented the slot count after Enter, need to decrement the slot count again to enable instrumentation
            SuppressInstrumentationScope.DecrementIfTriggered();
            Assert.False(OpenTelemetrySdk.SuppressInstrumentation); // Instrumentation is not suppressed anymore
        }
    }
}

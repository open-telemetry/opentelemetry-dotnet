// <copyright file="MassTransitInstrumentationTests.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using MassTransit.Testing;
using Moq;
using OpenTelemetry.Instrumentation.MassTransit.Implementation;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.MassTransit.Tests
{
    public class MassTransitInstrumentationTests
    {
        [Fact]
        public async Task MassTransitInstrumentationConsumerAndHandlerTest()
        {
            var activityProcessor = new Mock<ActivityProcessor>();
            using (Sdk.CreateTracerProvider(b =>
            {
                b.AddProcessorPipeline(c => c.AddProcessor(ap => activityProcessor.Object));
                b.AddMassTransitInstrumentation();
            }))
            {
                var harness = new InMemoryTestHarness();
                var consumerHarness = harness.Consumer<TestConsumer>();
                var handlerHarness = harness.Handler<TestMessage>();
                await harness.Start();
                try
                {
                    await harness.InputQueueSendEndpoint.Send<TestMessage>(new
                    {
                        Text = "Hello, world!",
                    });

                    Assert.True(harness.Consumed.Select<TestMessage>().Any());
                    Assert.True(consumerHarness.Consumed.Select<TestMessage>().Any());
                    Assert.True(handlerHarness.Consumed.Select().Any());
                }
                finally
                {
                    await harness.Stop();
                }
            }

            Assert.Equal(8, activityProcessor.Invocations.Count);

            var sends = this.GetActivitiesFromInvocationsByOperationName(activityProcessor.Invocations, OperationName.Transport.Send);
            var receives = this.GetActivitiesFromInvocationsByOperationName(activityProcessor.Invocations, OperationName.Transport.Receive);
            var consumes = this.GetActivitiesFromInvocationsByOperationName(activityProcessor.Invocations, OperationName.Consumer.Consume);
            var handles = this.GetActivitiesFromInvocationsByOperationName(activityProcessor.Invocations, OperationName.Consumer.Handle);

            foreach (var activity in sends)
            {
                Assert.Equal("SEND /input_queue", activity.DisplayName);
            }

            foreach (var activity in receives)
            {
                Assert.Equal("RECV /input_queue", activity.DisplayName);
            }

            foreach (var activity in consumes)
            {
                Assert.Equal("CONSUME OpenTelemetry.Instrumentation.MassTransit.Tests.TestConsumer", activity.DisplayName);
            }

            foreach (var activity in handles)
            {
                Assert.Equal("HANDLE TestMessage/OpenTelemetry.Instrumentation.MassTransit.Tests", activity.DisplayName);
            }
        }

        [Fact]
        public async Task MassTransitInstrumentationTestOptions()
        {
            var activityProcessor = new Mock<ActivityProcessor>();
            using (Sdk.CreateTracerProvider(b =>
            {
                b.AddProcessorPipeline(c => c.AddProcessor(ap => activityProcessor.Object));
                b.AddMassTransitInstrumentation(
                    opts =>
                        opts.TracedOperations = new HashSet<string>(new[] { OperationName.Consumer.Consume }));
            }))
            {
                var harness = new InMemoryTestHarness();
                var consumerHarness = harness.Consumer<TestConsumer>();
                var handlerHarness = harness.Handler<TestMessage>();
                await harness.Start();
                try
                {
                    await harness.InputQueueSendEndpoint.Send<TestMessage>(new
                    {
                        Text = "Hello, world!",
                    });

                    Assert.True(harness.Consumed.Select<TestMessage>().Any());
                    Assert.True(consumerHarness.Consumed.Select<TestMessage>().Any());
                    Assert.True(handlerHarness.Consumed.Select().Any());
                }
                finally
                {
                    await harness.Stop();
                }
            }

            Assert.Equal(2, activityProcessor.Invocations.Count);

            var consumes = this.GetActivitiesFromInvocationsByOperationName(activityProcessor.Invocations, "MassTransit.Consumer.Consume");
            Assert.Equal(2, consumes.Count());
        }

        private IEnumerable<Activity> GetActivitiesFromInvocationsByOperationName(IEnumerable<IInvocation> invocations, string operationName)
        {
            return
                invocations
                    .Where(i =>
                        i.Arguments.OfType<Activity>()
                            .Any(a => a.OperationName == operationName))
                    .Select(i => i.Arguments.OfType<Activity>().Single());
        }
    }
}

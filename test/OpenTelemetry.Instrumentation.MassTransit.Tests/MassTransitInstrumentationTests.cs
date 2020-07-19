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
using OpenTelemetry.Trace.Configuration;
using OpenTelemetry.Trace.Export;
using Xunit;

namespace OpenTelemetry.Instrumentation.MassTransit.Tests
{
    public class MassTransitInstrumentationTests
    {
        [Fact]
        public async Task TestMassTransitInstrumentationOnConsume()
        {
            var activityProcessor = new Mock<ActivityProcessor>();
            using (OpenTelemetrySdk.EnableOpenTelemetry(b =>
            {
                b.AddProcessorPipeline(c => c.AddProcessor(ap => activityProcessor.Object));
                b.AddMassTransitInstrumentation();
            }))
            {
                var harness = new InMemoryTestHarness();
                var consumerHarness = harness.Consumer<TestConsumer>();
                await harness.Start();
                try
                {
                    await harness.InputQueueSendEndpoint.Send<TestMessage>(new
                    {
                        Text = "Hello, world!",
                    });

                    Assert.True(harness.Consumed.Select<TestMessage>().Any());
                    Assert.True(consumerHarness.Consumed.Select<TestMessage>().Any());
                }
                finally
                {
                    await harness.Stop();
                }
            }

            Assert.Equal(6, activityProcessor.Invocations.Count);

            var sends = this.GetActivitiesFromInvocationsByOperationName(activityProcessor.Invocations, "MassTransit.Transport.Send");
            var receives = this.GetActivitiesFromInvocationsByOperationName(activityProcessor.Invocations, "MassTransit.Transport.Receive");
            var consumes = this.GetActivitiesFromInvocationsByOperationName(activityProcessor.Invocations, "MassTransit.Consumer.Consume");

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

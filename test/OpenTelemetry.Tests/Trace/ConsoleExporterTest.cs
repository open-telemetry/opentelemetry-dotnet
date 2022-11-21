// <copyright file="ConsoleExporterTest.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Tests.Trace
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;
    using OpenTelemetry.Trace;
    using Xunit;

    public class ConsoleExporterTest
    {
        [Fact]
        public void Test_3863()
        {
            // https://github.com/open-telemetry/opentelemetry-dotnet/issues/3863
            var mytesteventlistener = new MyTestEventListener("OpenTelemetry-Sdk", System.Diagnostics.Tracing.EventLevel.Error);

            var uniqueTestId = Guid.NewGuid();

            using var source = new ActivitySource("Testing");

            var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddSource("Testing")
                .AddConsoleExporter()
                .Build();

            ActivityContext context;
            using (var first = source.StartActivity("first"))
            {
                context = first!.Context;
            }

            var links = new[] { new ActivityLink(context) };
            using (var secondActivity = source.StartActivity(ActivityKind.Internal, links: links, name: "Second"))
            {
            }

            Task.Delay(TimeSpan.FromSeconds(1)).Wait(); // TODO: SpinWait

            Assert.False(mytesteventlistener.CapturedEvents.Any()); // SpanProcessorException
        }
    }
}

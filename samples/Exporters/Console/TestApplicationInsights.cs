// <copyright file="TestApplicationInsights.cs" company="OpenTelemetry Authors">
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
using System;
using System.Threading;
using OpenTelemetry.Context;
using OpenTelemetry.Exporter.ApplicationInsights;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Configuration;

namespace Samples
{
    internal class TestApplicationInsights
    {
        private static readonly string FrontendKey = "my.org/keys/frontend";

        internal static object Run()
        {
            DistributedContext.Carrier = AsyncLocalDistributedContextCarrier.Instance; // Enable asynclocal carrier for the context
            DistributedContext dc = DistributedContextBuilder.CreateContext(FrontendKey, "mobile-ios9.3.5");

            using var tracerFactory = TracerFactory.Create(builder => builder
                .SetResource(Resources.CreateServiceResource("my-service"))
                .UseApplicationInsights(config => config.InstrumentationKey = "instrumentation-key"));
            var tracer = tracerFactory.GetTracer("application-insights-test");

            using (DistributedContext.SetCurrent(dc))
            using (tracer.StartActiveSpan("incoming request", out var span))
            {
                span.AddEvent("Start processing video.");
                Thread.Sleep(TimeSpan.FromMilliseconds(10));
                span.AddEvent("Finished processing video.");
            }

            Thread.Sleep(TimeSpan.FromMilliseconds(5100));

            Console.WriteLine("Done... wait for events to arrive to backend!");
            Console.ReadLine();

            return null;
        }
    }
}

// <copyright file="TestStackdriver.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Threading;
using OpenTelemetry.Context;
using OpenTelemetry.Exporter.Stackdriver;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Configuration;

namespace Samples
{
    internal class TestStackdriver
    {
        private static readonly string FrontendKey = "my_org/keys/frontend";

        internal static object Run(string projectId)
        {
            var spanExporter = new StackdriverTraceExporter(projectId);

            using (var tracerFactory = TracerFactory.Create(builder => builder.AddProcessorPipeline(c => c.SetExporter(spanExporter))))
            {
                var tracer = tracerFactory.GetTracer("stackdriver-test");

                DistributedContext.Carrier = AsyncLocalDistributedContextCarrier.Instance; // Enable asynclocal carrier for the context
                DistributedContext dc = new DistributedContext(FrontendKey, "mobile-ios9.3.5");

                using (DistributedContext.SetCurrent(dc))
                {
                    using (tracer.StartActiveSpan("incoming request", out var span))
                    {
                        span.AddEvent("Processing video.");
                        Thread.Sleep(TimeSpan.FromMilliseconds(10));
                    }
                }

                Thread.Sleep(TimeSpan.FromMilliseconds(5100));

                Console.WriteLine("Done... wait for events to arrive to backend!");
                Console.ReadLine();

                return null;
            }
        }
    }
}

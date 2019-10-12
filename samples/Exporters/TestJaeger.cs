// <copyright file="TestJaeger.cs" company="OpenTelemetry Authors">
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

namespace Samples
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using OpenTelemetry.Exporter.Jaeger;
    using OpenTelemetry.Trace;
    using OpenTelemetry.Trace.Configuration;

    internal class TestJaeger
    {
        internal static object Run(string host, int port)
        {
            // Configure exporter to export traces to Jaeger
            var jaegerOptions = new JaegerExporterOptions()
            {
                ServiceName = "jaeger-test",
                AgentHost = host,
                AgentPort = port,
            };

            // Create a tracer. 
            using (var tracerFactory = TracerFactory.Create(builder => builder.SetExporter(new JaegerTraceExporter(jaegerOptions))))
            {
                var tracer = tracerFactory.GetTracer("jaeger-test");

                // Create a scoped span. It will end automatically when using statement ends
                using (tracer.WithSpan(tracer.StartSpan("Main")))
                {
                    tracer.CurrentSpan.SetAttribute("custom-attribute", 55);
                    Console.WriteLine("About to do a busy work");
                    for (int i = 0; i < 10; i++)
                    {
                        DoWork(i, tracer);
                    }
                }

                return null;
            }
        }

        private static void DoWork(int i, ITracer tracer)
        {
            // Start another span. If another span was already started, it'll use that span as the parent span.
            // In this example, the main method already started a span, so that'll be the parent span, and this will be
            // a child span.
            using (tracer.WithSpan(tracer.StartSpan("DoWork")))
            {
                // Simulate some work.
                var span = tracer.CurrentSpan;

                try
                {
                    Console.WriteLine("Doing busy work");
                    Thread.Sleep(1000);
                }
                catch (ArgumentOutOfRangeException e)
                {
                    // Set status upon error
                    span.Status = Status.Internal.WithDescription(e.ToString());
                }

                // Annotate our span to capture metadata about our operation
                var attributes = new Dictionary<string, object>();
                attributes.Add("use", "demo");
                attributes.Add("iteration", i);
                span.AddEvent("Invoking DoWork", attributes);
            }
        }
    }
}

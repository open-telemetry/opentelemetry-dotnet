// <copyright file="TestJaeger.cs" company="OpenTelemetry Authors">
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
using System;
using System.Collections.Generic;
using System.Threading;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Configuration;

namespace Samples
{
    internal class TestJaeger
    {
        internal static object Run(string host, int port, bool useActivitySource)
        {
            if (useActivitySource)
            {
                return RunWithActivity(host, port);
            }

            return RunWithSdk(host, port);
        }

        internal static object RunWithActivity(string host, int port)
        {
            // Enable OpenTelemetry for the sources "Samples.SampleServer" and "Samples.SampleClient"
            // and use the Jaeger exporter.
            OpenTelemetrySdk.EnableOpenTelemetry(
                builder => builder
                    .AddActivitySource("Samples.SampleServer")
                    .AddActivitySource("Samples.SampleClient")
                    .UseJaegerActivityExporter(o =>
                    {
                        o.ServiceName = "jaeger-test";
                        o.AgentHost = host;
                        o.AgentPort = port;
                    }));

            // The above lines are required only in Applications
            // which decide to use OT.

            using (var sample = new InstrumentationWithActivitySource())
            {
                sample.Start();

                Console.WriteLine("Sample is running on the background, press ENTER to stop");
                Console.ReadLine();
            }

            return null;
        }

        internal static object RunWithSdk(string host, int port)
        {
            // Create a tracer.
            using var tracerFactory = TracerFactory.Create(
                builder => builder.UseJaeger(o =>
                {
                    o.ServiceName = "jaeger-test";
                    o.AgentHost = host;
                    o.AgentPort = port;
                }));
            var tracer = tracerFactory.GetTracer("jaeger-test");

            // Create a scoped span. It will end automatically when using statement ends
            using (tracer.StartActiveSpan("Main", out var span))
            {
                span.SetAttribute("custom-attribute", 55);
                Console.WriteLine("About to do a busy work");
                for (int i = 0; i < 10; i++)
                {
                    DoWork(i, tracer);
                }

                Console.WriteLine("Completed doing busy work");
                Console.WriteLine("Press Enter key to exit.");
            }

            return null;
        }

        private static void DoWork(int i, Tracer tracer)
        {
            // Start another span. If another span was already started, it'll use that span as the parent span.
            // In this example, the main method already started a span, so that'll be the parent span, and this will be
            // a child span.
            using (tracer.StartActiveSpan("DoWork", out var span))
            {
                // Simulate some work.
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
                span.AddEvent(new Event("Invoking DoWork", attributes));
            }
        }
    }
}

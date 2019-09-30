﻿// <copyright file="TestJaeger.cs" company="OpenTelemetry Authors">
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
    using OpenTelemetry.Exporter.Jaeger.Implementation;
    using OpenTelemetry.Trace;
    using OpenTelemetry.Trace.Config;
    using OpenTelemetry.Trace.Export;

    internal class TestJaeger
    {
        internal static object Run(string host, int port)
        {
            // Configure exporter to export traces to Jaeger
            var jaegerOptions = new JaegerExporterOptions()
            {
                ServiceName = "tracing-to-jaeger-service",
                AgentHost = host,
                AgentPort = port,
            };

            var exporter = new JaegerTraceExporter(
                jaegerOptions);

            // Create a tracer. You may also need to register it as a global instance to make auto-collectors work..
            var tracer = new Tracer(new BatchingSpanProcessor(exporter), TraceConfig.Default);

            // Create a scoped span. It will end automatically when using statement ends
            using (tracer.WithSpan(tracer.SpanBuilder("Main").StartSpan()))
            {
                tracer.CurrentSpan.SetAttribute("custom-attribute", 55);
                Console.WriteLine("About to do a busy work");
                for (int i = 0; i < 10; i++)
                {
                    DoWork(i, tracer);
                }
            }

            // Gracefully shutdown the exporter so it'll flush queued traces to Jaeger.
            exporter.ShutdownAsync(CancellationToken.None).GetAwaiter().GetResult();
            return null;
        }

        private static void DoWork(int i, Tracer tracer)
        {
            // Start another span. If another span was already started, it'll use that span as the parent span.
            // In this example, the main method already started a span, so that'll be the parent span, and this will be
            // a child span.
            using (tracer.WithSpan(tracer.SpanBuilder("DoWork").StartSpan()))
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

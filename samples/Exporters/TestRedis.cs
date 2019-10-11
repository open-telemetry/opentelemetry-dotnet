// <copyright file="TestRedis.cs" company="OpenTelemetry Authors">
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
    using OpenTelemetry.Collector.StackExchangeRedis;
    using OpenTelemetry.Exporter.Zipkin;
    using OpenTelemetry.Trace;
    using OpenTelemetry.Trace.Export;
    using StackExchange.Redis;

    internal class TestRedis
    {
        internal static object Run(string zipkinUri)
        {
            // Configure exporter to export traces to Zipkin
            var exporter = new ZipkinTraceExporter(
                new ZipkinTraceExporterOptions()
                {
                    Endpoint = new Uri(zipkinUri),
                    ServiceName = "tracing-to-zipkin-service",
                });

            // Create a tracer. You may also need to register it as a global instance to make auto-collectors work..
            var tracerFactory = new TracerFactory(new BatchingSpanProcessor(exporter));
            var tracer = tracerFactory.GetTracer(string.Empty);

            var collector = new StackExchangeRedisCallsCollector(tracer);

            // connect to the server
            var connection = ConnectionMultiplexer.Connect("localhost:6379");
            connection.RegisterProfiler(collector.GetProfilerSessionsFactory());

            // select a database (by default, DB = 0)
            var db = connection.GetDatabase();

            // Create a scoped span. It will end automatically when using statement ends
            using (tracer.WithSpan(tracer.StartSpan("Main")))
            {
                Console.WriteLine("About to do a busy work");
                for (var i = 0; i < 10; i++)
                {
                    DoWork(db, tracer);
                }
            }

            // Gracefully shutdown the exporter so it'll flush queued traces to Zipkin.
            exporter.ShutdownAsync(CancellationToken.None).GetAwaiter().GetResult();
            return null;
        }

        private static void DoWork(IDatabase db, ITracer tracer)
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
                    db.StringSet("key", "value " + DateTime.Now.ToLongDateString());

                    Console.WriteLine("Doing busy work");
                    Thread.Sleep(1000);

                    // run a command, in this case a GET
                    var myVal = db.StringGet("key");

                    Console.WriteLine(myVal);
                }
                catch (ArgumentOutOfRangeException e)
                {
                    // Set status upon error
                    span.Status = Status.Internal.WithDescription(e.ToString());
                }

                // Annotate our span to capture metadata about our operation
                var attributes = new Dictionary<string, object>
                {
                    { "use", "demo" },
                };
                span.AddEvent("Invoking DoWork", attributes);
            }
        }
    }
}

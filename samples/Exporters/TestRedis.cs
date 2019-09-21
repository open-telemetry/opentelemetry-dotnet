﻿// <copyright file="TestRedis.cs" company="OpenTelemetry Authors">
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
    using OpenTelemetry.Trace.Sampler;
    using StackExchange.Redis;

    internal class TestRedis
    {
        internal static object Run(string zipkinUri)
        {
            // 1. Configure exporter to export traces to Zipkin
            var exporter = new ZipkinTraceExporter(
                new ZipkinTraceExporterOptions()
                {
                    Endpoint = new Uri(zipkinUri),
                    ServiceName = "tracing-to-zipkin-service",
                },
                Tracing.SpanExporter);
            exporter.Start();

            // 2. Configure 100% sample rate for the purposes of the demo
            var traceConfig = Tracing.TraceConfig;
            var currentConfig = traceConfig.ActiveTraceParams;
            var newConfig = currentConfig.ToBuilder()
                .SetSampler(Samplers.AlwaysSample)
                .Build();
            traceConfig.UpdateActiveTraceParams(newConfig);

            // 3. Tracer is global singleton. You can register it via dependency injection if it exists
            // but if not - you can use it as follows:
            var tracer = Tracing.Tracer;

            var collector = new StackExchangeRedisCallsCollector(tracer);

            // connect to the server
            var connection = ConnectionMultiplexer.Connect("localhost:6379");
            connection.RegisterProfiler(collector.GetProfilerSessionsFactory());

            // select a database (by default, DB = 0)
            var db = connection.GetDatabase();

            // 4. Create a scoped span. It will end automatically when using statement ends
            using (tracer.WithSpan(tracer.SpanBuilder("Main").StartSpan()))
            {
                Console.WriteLine("About to do a busy work");
                for (var i = 0; i < 10; i++)
                {
                    DoWork(db);
                }
            }

            // 5. Gracefully shutdown the exporter so it'll flush queued traces to Zipkin.
            Tracing.SpanExporter.Dispose();

            return null;
        }

        private static void DoWork(IDatabase db)
        {
            // 6. Get the global singleton Tracer object
            var tracer = Tracing.Tracer;

            // 7. Start another span. If another span was already started, it'll use that span as the parent span.
            // In this example, the main method already started a span, so that'll be the parent span, and this will be
            // a child span.
            using (tracer.WithSpan(tracer.SpanBuilder("DoWork").StartSpan()))
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
                    // 6. Set status upon error
                    span.Status = Status.Internal.WithDescription(e.ToString());
                }

                // 7. Annotate our span to capture metadata about our operation
                var attributes = new Dictionary<string, object>
                {
                    { "use", "demo" },
                };
                span.AddEvent("Invoking DoWork", attributes);
            }
        }
    }
}

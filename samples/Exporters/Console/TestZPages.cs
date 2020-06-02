// <copyright file="TestZPages.cs" company="OpenTelemetry Authors">
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
using System.Threading;
using OpenTelemetry.Exporter.ZPages;
using OpenTelemetry.Exporter.ZPages.Implementation;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Configuration;
using OpenTelemetry.Trace.Export;

namespace Samples
{
    internal class TestZPages
    {
        internal static object Run()
        {
            var zpagesOptions = new ZPagesExporterOptions() { Url = "http://localhost:7284/rpcz/" };
            var zpagesExporter = new ZPagesExporter(zpagesOptions);
            var spanProcessor = new ZPagesSpanProcessor(zpagesExporter);
            ZPagesSpans.RetentionTime = 3600000;
            var httpServer = new ZPagesExporterStatsHttpServer(zpagesExporter, spanProcessor);

            // Start the server
            httpServer.Start();

            // Configure exporter
            using (var tracerFactory = TracerFactory.Create(builder => builder
                .AddProcessorPipeline(b => b
                    .SetExporter(zpagesExporter)
                    .SetExportingProcessor(e => spanProcessor))))
            {
                var tracer = tracerFactory.GetTracer("zpages-test");

                while (true)
                {
                    // Create a scoped span.
                    TelemetrySpan telemetrySpan = tracer.StartSpan("Main");
                    telemetrySpan.Status = Status.Unavailable;

                    using (tracer.WithSpan(telemetrySpan))
                    {
                        Console.WriteLine("Starting Span");
                    }

                    Thread.Sleep(3000);

                    telemetrySpan.End();

                    // Create a scoped span.
                    TelemetrySpan telemetrySpan2 = tracer.StartSpan("TestSpan");
                    telemetrySpan2.Status = Status.Ok;

                    using (tracer.WithSpan(telemetrySpan2))
                    {
                        Console.WriteLine("Starting Span2");
                    }

                    Thread.Sleep(5000);

                    telemetrySpan2.End();
                }
            }
        }
    }
}

// <copyright file="Program.cs" company="OpenTelemetry Authors">
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

namespace LoggingTracer.Demo.ConsoleApp
{
    using System.Threading.Tasks;
    using OpenTelemetry.Trace;
    using OpenTelemetry.Trace.Configuration;

    public class Program
    {
        public static async Task Main(string[] args)
        {
            // Run the "Foo" scenario using a custom SDK implementation (Tracer, Span, ...)
            // that logs the received calls to the console.
            await RunFooWithLogingTracer();

            // Run the "Foo" scenario using the default SDK with a custom exporter
            // that logs exported span data to the console.
            await RunFooWithLoggingExporter();
        }

        private static async Task RunFooWithLogingTracer()
        {
            Logger.Log("*** RunFooWithLogingTracer ***");
            await Foo(new LoggingTracerFactory());
        }

        private static async Task RunFooWithLoggingExporter()
        {
            Logger.Log("*** RunFooWithLoggingExporter ***");
            var exporter = new LoggingExporter();
            using var tracerFactory = TracerFactory.Create(builder => builder.AddProcessorPipeline(c => c.SetExporter(exporter)));
            await Foo(tracerFactory);
        }

        private static async Task Foo(TracerFactoryBase tracerFactory)
        {
            var tracer = tracerFactory.GetTracer("ConsoleApp", "semver:1.0.0");
            using (tracer.WithSpan(tracer.StartSpan("Main")))
            {
                using (tracer.WithSpan(tracer.StartSpan("Main (span1)")))
                {
                    await Task.Delay(100);
                    using (tracer.WithSpan(tracer.StartSpan("Foo (span2)")))
                    {
                        tracer.CurrentSpan.SetAttribute("myattribute", "mvalue");
                        await Task.Delay(100);
                    }
                }
            }
        }
    }
}

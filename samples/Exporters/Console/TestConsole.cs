// <copyright file="TestConsole.cs" company="OpenTelemetry Authors">
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

using OpenTelemetry.Exporter.Console;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Configuration;

namespace Samples
{
    internal class TestConsole
    {
        internal static object Run(ConsoleOptions options)
        {
            // map test project settings to ConsoleExporterSetting
            var exporterOptions = new ConsoleExporterOptions
            {
                Pretty = options.Pretty,
            };

            // create exporter
            var exporter = new ConsoleExporter(exporterOptions);

            // Create tracer
            using var tracerFactory = TracerFactory.Create(builder =>
            {
                builder.AddProcessorPipeline(p => p.SetExporter(exporter));
            });
            var tracer = tracerFactory.GetTracer("console-test");

            using (tracer.StartActiveSpan("parent", out var parent))
            {
                tracer.CurrentSpan.SetAttribute("key", 123);
                tracer.CurrentSpan.AddEvent("test-event");

                using (tracer.StartActiveSpan("child", out var child))
                {
                    child.SetAttribute("key", "value");
                }
            }

            return null;
        }
    }
}

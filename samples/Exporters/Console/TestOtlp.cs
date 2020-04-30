// <copyright file="TestOtlp.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Exporter.OpenTelemetryProtocol;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Configuration;

namespace Samples
{
    internal static class TestOtlp
    {
        internal static object Run(string endpoint)
        {
            using var tracerFactory = TracerFactory.Create(builder => builder
                .SetResource(Resources.CreateServiceResource("otlp-test"))
                .UseOpenTelemetryProtocolExporter(config => config.Endpoint = endpoint));
            var tracer = tracerFactory.GetTracer("otlp.test.tracer");

            using (tracer.StartActiveSpan("parent", out var parent))
            {
                tracer.CurrentSpan.SetAttribute("key", 123);
                tracer.CurrentSpan.AddEvent("test-event");

                using (tracer.StartActiveSpan("child", out var child))
                {
                    child.SetAttribute("key", "value");
                }
            }

            Console.WriteLine("Done... wait for events to arrive to backend!");
            Console.ReadLine();

            return null;
        }
    }
}

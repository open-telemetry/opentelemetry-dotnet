// <copyright file="TestOTelShimWithConsoleExporter.cs" company="OpenTelemetry Authors">
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

using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Examples.Console
{
    internal class TestOTelShimWithConsoleExporter
    {
        internal static object Run(OpenTelemetryShimOptions options)
        {
            // Enable OpenTelemetry for the source "MyCompany.MyProduct.MyWebServer"
            // and use a single pipeline with a custom MyProcessor, and Console exporter.
            using var tracerProvider = Sdk.CreateTracerProvider(
                (builder) => builder.AddActivitySource("MyCompany.MyProduct.MyWebServer")
                    .SetResource(Resources.CreateServiceResource("MyServiceName"))
                    .UseConsoleExporter(opt => opt.DisplayAsJson = options.DisplayAsJson));

            // The above line is required only in applications
            // which decide to use Open Telemetry.

            var tracer = TracerProvider.Default.GetTracer("MyCompany.MyProduct.MyWebServer");
            var span = tracer.StartSpan("parent span");
            span.SetAttribute("mystring", "value");
            span.SetAttribute("myint", 100);
            span.SetAttribute("mydouble", 101.089);
            span.SetAttribute("mybool", true);
            span.UpdateName("parent span new name");

            var spanChild = tracer.StartSpan("child span");
            spanChild.AddEvent("sample event").SetAttribute("ch", "value").SetAttribute("more", "attributes");
            spanChild.End();

            span.End();

            System.Console.WriteLine("Press Enter key to exit.");

            return null;
        }
    }
}

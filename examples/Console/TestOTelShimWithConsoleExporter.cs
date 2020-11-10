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
            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                    .AddSource("MyCompany.MyProduct.MyWebServer")
                    .SetResource(Resources.CreateServiceResource("MyServiceName"))
                    .AddConsoleExporter()
                    .Build();

            // The above line is required only in applications
            // which decide to use OpenTelemetry.

            var tracer = TracerProvider.Default.GetTracer("MyCompany.MyProduct.MyWebServer");
            using (var parentSpan = tracer.StartActiveSpan("parent span"))
            {
                parentSpan.SetAttribute("mystring", "value");
                parentSpan.SetAttribute("myint", 100);
                parentSpan.SetAttribute("mydouble", 101.089);
                parentSpan.SetAttribute("mybool", true);
                parentSpan.UpdateName("parent span new name");

                var childSpan = tracer.StartSpan("child span");
                childSpan.AddEvent("sample event").SetAttribute("ch", "value").SetAttribute("more", "attributes");
                childSpan.SetStatus(Status.Ok);
                childSpan.End();
            }

            System.Console.WriteLine("Press Enter key to exit.");

            return null;
        }
    }
}

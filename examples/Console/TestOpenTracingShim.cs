// <copyright file="TestOpenTracingShim.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Resources;
using OpenTelemetry.Shims.OpenTracing;
using OpenTelemetry.Trace;
using OpenTracing;

namespace Examples.Console
{
    internal class TestOpenTracingShim
    {
        internal static object Run(OpenTracingShimOptions options)
        {
            // Enable OpenTelemetry for the source "MyCompany.MyProduct.MyWebServer"
            // and use Console exporter.
            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                    .AddSource("MyCompany.MyProduct.MyWebServer")
                    .ConfigureResource(r => r.AddService("MyServiceName"))
                    .AddConsoleExporter()
                    .Build();

            // Instantiate the OpenTracing shim. The underlying OpenTelemetry tracer will create
            // spans using the "MyCompany.MyProduct.MyWebServer" source.
            var tracer = new TracerShim(
                TracerProvider.Default.GetTracer("MyCompany.MyProduct.MyWebServer"),
                Propagators.DefaultTextMapPropagator);

            // Not necessary for this example, though it is best practice per
            // the OpenTracing project to register a GlobalTracer.
            OpenTracing.Util.GlobalTracer.Register(tracer);

            // The code below is meant to resemble application code that has been instrumented
            // with the OpenTracing API.
            using (IScope parentScope = tracer.BuildSpan("Parent").StartActive(finishSpanOnDispose: true))
            {
                parentScope.Span.SetTag("my", "value");
                parentScope.Span.SetOperationName("parent span new name");

                // The child scope will automatically use parentScope as its parent.
                using IScope childScope = tracer.BuildSpan("Child").StartActive(finishSpanOnDispose: true);
                childScope.Span.SetTag("Child Tag", "Child Tag Value").SetTag("ch", "value").SetTag("more", "attributes");
            }

            System.Console.WriteLine("Press Enter key to exit.");
            System.Console.ReadLine();

            return null;
        }
    }
}

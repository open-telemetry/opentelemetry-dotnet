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

namespace Examples.Console;

internal class TestOpenTracingShim
{
    internal static object Run(OpenTracingShimOptions options)
    {
        // Enable OpenTelemetry for the source "opentracing-shim"
        // and use Console exporter.
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddSource("opentracing-shim")
                .ConfigureResource(r => r.AddService("MyServiceName"))
                .AddConsoleExporter()
                .Build();

        // Instantiate the OpenTracing shim. The underlying OpenTelemetry tracer will create
        // spans using the "opentracing-shim" source.
        var openTracingTracerShim = new TracerShim(
            TracerProvider.Default,
            Propagators.DefaultTextMapPropagator);

        // The OpenTracing Tracer shim instance must be registered prior to any calls
        // to GlobalTracer.Instance, otherwise GlobalTracer.Instance will register a NoopTracer
        // preventing sampling of any OpenTracing spans.
        OpenTracing.Util.GlobalTracer.Register(openTracingTracerShim);

        // The code ahead could just use the OpenTracing Tracer shim instance directly.
        // However, an instrumentation using OpenTracing API will use the GlobalTracer.Instance
        // to create spans.
        var openTracingTracer = OpenTracing.Util.GlobalTracer.Instance;

        // The code below is meant to resemble application code that has been instrumented
        // with the OpenTracing API.
        using (IScope parentScope = openTracingTracer.BuildSpan("Parent").StartActive(finishSpanOnDispose: true))
        {
            parentScope.Span.SetTag("my", "value");
            parentScope.Span.SetOperationName("parent span new name");

            // The child scope will automatically use parentScope as its parent.
            using IScope childScope = openTracingTracer.BuildSpan("Child").StartActive(finishSpanOnDispose: true);
            childScope.Span.SetTag("Child Tag", "Child Tag Value").SetTag("ch", "value").SetTag("more", "attributes");
        }

        System.Console.WriteLine("Press Enter key to exit.");
        System.Console.ReadLine();

        return null;
    }
}

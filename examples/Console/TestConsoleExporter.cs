// <copyright file="TestConsoleExporter.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Examples.Console
{
    internal class TestConsoleExporter
    {
        internal static object Run(ConsoleOptions options)
        {
            // Enable TracerProvider for the source "MyCompany.MyProduct.MyWebServer"
            // and use a custom MyProcessor, along with Console exporter.
            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddSource("MyCompany.MyProduct.MyWebServer")
                .SetResource(Resources.CreateServiceResource("MyServiceName"))
                .AddProcessor(new MyProcessor()) // This must be added before ConsoleExporter
                .AddConsoleExporter(opt => opt.DisplayAsJson = options.DisplayAsJson)
                .Build();

            // The above line is required only in applications
            // which decide to use OpenTelemetry.

            // Libraries would simply write the following lines of code to
            // emit activities, which are the .NET representation of OpenTelemetry Spans.
            var source = new ActivitySource("MyCompany.MyProduct.MyWebServer");

            // The below commented out line shows more likely code in a real world webserver.
            // using (var parent = source.StartActivity("HttpIn", ActivityKind.Server, HttpContext.Request.Headers["traceparent"] ))
            using (var parent = source.StartActivity("HttpIn", ActivityKind.Server))
            {
                // TagNames can follow the OpenTelemetry guidelines
                // from https://github.com/open-telemetry/opentelemetry-specification/tree/master/specification/trace/semantic_conventions
                parent?.SetTag("http.method", "GET");
                parent?.SetTag("http.host", "MyHostName");
                if (parent != null)
                {
                    parent.DisplayName = "HttpIn DisplayName";

                    // IsAllDataRequested is the equivalent of Span.IsRecording
                    if (parent.IsAllDataRequested)
                    {
                        parent.SetTag("expensive data", "This data is expensive to obtain. Avoid it if activity is not being recorded");
                    }
                }

                try
                {
                    // Actual code to achieve the purpose of the library.
                    // For websebserver example, this would be calling
                    // user middlware pipeline.

                    // There can be child activities.
                    // In this example HttpOut is a child of HttpIn.
                    using (var child = source.StartActivity("HttpOut", ActivityKind.Client))
                    {
                        child?.SetTag("http.url", "www.mydependencyapi.com");
                        try
                        {
                            // do actual work.

                            child?.AddEvent(new ActivityEvent("sample activity event."));
                            child?.SetTag("http.status_code", "200");
                        }
                        catch (Exception)
                        {
                            child?.SetTag("http.status_code", "500");
                        }
                    }

                    parent?.SetTag("http.status_code", "200");
                }
                catch (Exception)
                {
                    parent?.SetTag("http.status_code", "500");
                }
            }

            System.Console.WriteLine("Press Enter key to exit.");

            return null;
        }

        internal class MyProcessor : ActivityProcessor
        {
            public override void OnStart(Activity activity)
            {
                if (activity.IsAllDataRequested)
                {
                    if (activity.Kind == ActivityKind.Server)
                    {
                        activity.SetTag("customServerTag", "Custom Tag Value for server");
                    }
                    else if (activity.Kind == ActivityKind.Client)
                    {
                        activity.SetTag("customClientTag", "Custom Tag Value for Client");
                    }
                }
            }
        }
    }
}

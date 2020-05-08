// <copyright file="TestConsoleActivity.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Exporter.Console;
using OpenTelemetry.Trace.Configuration;
using OpenTelemetry.Trace.Export;

namespace Samples
{
    internal class TestConsoleActivity
    {
        internal static object Run(ConsoleActivityOptions options)
        {
            // Setup exporter
            var exporterOptions = new ConsoleActivityExporterOptions
            {
                DisplayAsJson = options.DisplayAsJson,
            };
            var activityExporter = new ConsoleActivityExporter(exporterOptions);

            // Setup processor
            var activityProcessor = new SimpleActivityProcessor(activityExporter);

            // Enable OpenTelemetry for the "source" named "MyCompany.MyProduct.MyWebServer".
            OpenTelemetrySDK.EnableOpenTelemetry("MyCompany.MyProduct.MyWebServer", activityProcessor);

            // Everything above this line is required only in Applications
            // which decide to use OT.

            // The following is generating activity.
            // A library would simply write the following line of code.
            var source = new ActivitySource("MyCompany.MyProduct.MyWebServer");
            using (var parent = source.StartActivity("HttpIn", ActivityKind.Server))
            {
                parent?.AddTag("http.method", "GET");
                parent?.AddTag("http.host", "MyHostName");
                if (parent != null)
                {
                    parent.DisplayName = "HttpIn DisplayName";
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
                        child?.AddTag("http.url", "www.mydependencyapi.com");
                        try
                        {
                            // do actual work.

                            child?.AddTag("http.status_code", "200");
                        }
                        catch (Exception)
                        {
                            child?.AddTag("http.status_code", "500");
                        }
                    }

                    parent?.AddTag("http.status_code", "200");
                }
                catch (Exception)
                {
                    parent?.AddTag("http.status_code", "500");
                }
            }

            return null;
        }
    }
}

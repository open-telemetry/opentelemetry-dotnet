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

using System.Diagnostics;
using OpenTelemetry.Exporter.Console;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Configuration;
using OpenTelemetry.Trace.Export;

namespace Samples
{
    internal class TestConsoleActivity
    {
        internal static object Run(ConsoleActivityOptions options)
        {
            // Setup exporter
            var exporterOptions = new ConsoleExporterOptions
            {
                Pretty = options.Pretty,
            };
            var activityExporter = new ConsoleActivityExporter(exporterOptions);

            // Setup processor
            var activityProcessor = new SimpleActivityProcessor(activityExporter);

            // Enable OpenTelemetry for the "source" named "DemoSource".
            OpenTelemetrySDK.EnableOpenTelemetry("DemoSource", activityProcessor);

            // Everything above this line is required only in Applications
            // which decide to use OT.

            // The following is generating activity.
            // A library would simply write the following line of code.
            var source = new ActivitySource("DemoSource");
            using (var parent = source.StartActivity("parent"))
            {
                parent?.AddTag("parent location", "parent location");

                using (var child = source.StartActivity("child"))
                {
                    child?.AddTag("child location", "child location");
                }
            }

            return null;
        }
    }
}

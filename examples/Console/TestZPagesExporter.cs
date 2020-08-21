// <copyright file="TestZPagesExporter.cs" company="OpenTelemetry Authors">
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
using System.Threading;
using OpenTelemetry;
using OpenTelemetry.Exporter.ZPages;
using OpenTelemetry.Trace;

namespace Examples.Console
{
    internal class TestZPagesExporter
    {
        internal static object Run()
        {
            var zpagesOptions = new ZPagesExporterOptions() { Url = "http://localhost:7284/rpcz/", RetentionTime = 3600000 };
            var zpagesExporter = new ZPagesExporter(zpagesOptions);
            var httpServer = new ZPagesExporterStatsHttpServer(zpagesExporter);

            // Start the server
            httpServer.Start();

            using var openTelemetry = Sdk.CreateTracerProviderBuilder()
                    .AddSource("zpages-test")
                    .AddZPagesExporter(o =>
                    {
                        o.Url = zpagesOptions.Url;
                        o.RetentionTime = zpagesOptions.RetentionTime;
                    })
                    .Build();

            ActivitySource activitySource = new ActivitySource("zpages-test");

            while (true)
            {
                // Create a scoped activity. It will end automatically when using statement ends
                using (activitySource.StartActivity("Main"))
                {
                    System.Console.WriteLine("About to do a busy work in Main");
                }

                Thread.Sleep(3000);

                // Create a scoped activity. It will end automatically when using statement ends
                using (activitySource.StartActivity("Test"))
                {
                    System.Console.WriteLine("About to do a busy work in Test");
                }

                Thread.Sleep(5000);
            }
        }
    }
}

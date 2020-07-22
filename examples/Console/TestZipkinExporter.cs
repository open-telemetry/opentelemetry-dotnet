// <copyright file="TestZipkinExporter.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Trace;

namespace Examples.Console
{
    internal class TestZipkinExporter
    {
        internal static object Run(string zipkinUri)
        {
            // Enable OpenTelemetry for the sources "Samples.SampleServer" and "Samples.SampleClient"
            // and use the Zipkin exporter.
            using var openTelemetry = TracerProviderSdk.EnableTracerProvider(
                builder => builder
                    .AddActivitySource("Samples.SampleServer")
                    .AddActivitySource("Samples.SampleClient")
                    .UseZipkinExporter(o =>
                    {
                        o.ServiceName = "test-zipkin";
                        o.Endpoint = new Uri(zipkinUri);
                    }));

            using (var sample = new InstrumentationWithActivitySource())
            {
                sample.Start();

                System.Console.WriteLine("Traces are being created and exported" +
                    "to Zipkin in the background. Use Zipkin to view them." +
                    "Press ENTER to stop.");
                System.Console.ReadLine();
            }

            return null;
        }
    }
}

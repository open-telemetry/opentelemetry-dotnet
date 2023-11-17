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

using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Examples.Console;

internal class TestZipkinExporter
{
    internal static object Run(string zipkinUri)
    {
        // Prerequisite for running this example.
        // Setup zipkin inside local docker using following command:
        // docker run -d -p 9411:9411 openzipkin/zipkin

        // To run this example, run the following command from
        // the reporoot\examples\Console\.
        // (eg: C:\repos\opentelemetry-dotnet\examples\Console\)
        //
        // dotnet run zipkin -u http://localhost:9411/api/v2/spans

        // Enable OpenTelemetry for the sources "Samples.SampleServer" and "Samples.SampleClient"
        // and use the Zipkin exporter.
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddSource("Samples.SampleClient", "Samples.SampleServer")
                .ConfigureResource(r => r.AddService("zipkin-test"))
                .AddZipkinExporter(o =>
                {
                    o.Endpoint = new Uri(zipkinUri);
                })
                .Build();

        using (var sample = new InstrumentationWithActivitySource())
        {
            sample.Start();

            System.Console.WriteLine("Traces are being created and exported " +
                "to Zipkin in the background. Use Zipkin to view them. " +
                "Press ENTER to stop.");
            System.Console.ReadLine();
        }

        return null;
    }
}

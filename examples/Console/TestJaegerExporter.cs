// <copyright file="TestJaegerExporter.cs" company="OpenTelemetry Authors">
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
    internal class TestJaegerExporter
    {
        internal static object Run(string host, int port)
        {
            // To run this example, run the following command from
            // the reporoot\examples\Console\.
            // (eg: C:\repos\opentelemetry-dotnet\examples\Console\)
            //
            // dotnet run jaeger -h localhost -p 6831
            return RunWithActivity(host, port);
        }

        internal static object RunWithActivity(string host, int port)
        {
            // Enable OpenTelemetry for the sources "Samples.SampleServer" and "Samples.SampleClient"
            // and use the Jaeger exporter.
            using var openTelemetry = Sdk.CreateTracerProviderBuilder()
                    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("jaeger-test"))
                    .AddSource("Samples.SampleClient", "Samples.SampleServer")
                    .AddJaegerExporter(o =>
                    {
                        o.AgentHost = host;
                        o.AgentPort = port;
                    })
                    .Build();

            // The above lines are required only in Applications
            // which decide to use OpenTelemetry.

            using (var sample = new InstrumentationWithActivitySource())
            {
                sample.Start();

                System.Console.WriteLine("Traces are being created and exported" +
                    "to Jaeger in the background. Use Jaeger to view them." +
                    "Press ENTER to stop.");
                System.Console.ReadLine();
            }

            return null;
        }
    }
}

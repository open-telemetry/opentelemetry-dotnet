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
            // Prerequisite for running this example.
            // Setup Jaegar inside local docker using following command (Source: https://www.jaegertracing.io/docs/1.21/getting-started/#all-in-one):
            /*
            $ docker run -d --name jaeger \
            -e COLLECTOR_ZIPKIN_HTTP_PORT=9411 \
            -p 5775:5775/udp \
            -p 6831:6831/udp \
            -p 6832:6832/udp \
            -p 5778:5778 \
            -p 16686:16686 \
            -p 14268:14268 \
            -p 14250:14250 \
            -p 9411:9411 \
            jaegertracing/all-in-one:1.21
            */

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

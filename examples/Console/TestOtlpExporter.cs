// <copyright file="TestOtlpExporter.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Examples.Console;

internal static class TestOtlpExporter
{
    internal static object Run(string endpoint, string protocol)
    {
        /*
         * Prerequisite to run this example:
         * Set up an OpenTelemetry Collector to run on local docker.
         *
         * Open a terminal window at the examples/Console/ directory and
         * launch the OpenTelemetry Collector with an OTLP receiver, by running:
         *
         *  - On Unix based systems use:
         *     docker run --rm -it -p 4317:4317 -p 4318:4318 -v $(pwd):/cfg otel/opentelemetry-collector:0.48.0 --config=/cfg/otlp-collector-example/config.yaml
         *
         *  - On Windows use:
         *     docker run --rm -it -p 4317:4317 -p 4318:4318 -v "%cd%":/cfg otel/opentelemetry-collector:0.48.0 --config=/cfg/otlp-collector-example/config.yaml
         *
         * Open another terminal window at the examples/Console/ directory and
         * launch the OTLP example by running:
         *
         *     dotnet run otlp
         *
         * The OpenTelemetry Collector will output all received spans to the stdout of its terminal until
         * it is stopped via CTRL+C.
         *
         * For more information about the OpenTelemetry Collector go to https://github.com/open-telemetry/opentelemetry-collector
         *
         */
        return RunWithActivitySource(endpoint, protocol);
    }

    private static object RunWithActivitySource(string endpoint, string protocol)
    {
        var otlpExportProtocol = ToOtlpExportProtocol(protocol);
        if (!otlpExportProtocol.HasValue)
        {
            System.Console.WriteLine($"Export protocol {protocol} is not supported. Default protocol 'grpc' will be used.");
            otlpExportProtocol = OtlpExportProtocol.Grpc;
        }

        // Enable OpenTelemetry for the sources "Samples.SampleServer" and "Samples.SampleClient"
        // and use OTLP exporter.
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddSource("Samples.SampleClient", "Samples.SampleServer")
                .ConfigureResource(r => r.AddService("otlp-test"))
                .AddOtlpExporter(opt =>
                {
                    // If endpoint was not specified, the proper one will be selected according to the protocol.
                    if (!string.IsNullOrEmpty(endpoint))
                    {
                        opt.Endpoint = new Uri(endpoint);
                    }

                    opt.Protocol = otlpExportProtocol.Value;

                    System.Console.WriteLine($"OTLP Exporter is using {opt.Protocol} protocol and endpoint {opt.Endpoint}");
                })
                .Build();

        // The above line is required only in Applications
        // which decide to use OpenTelemetry.
        using (var sample = new InstrumentationWithActivitySource())
        {
            sample.Start();

            System.Console.WriteLine("Traces are being created and exported " +
                "to the OpenTelemetry Collector in the background. " +
                "Press ENTER to stop.");
            System.Console.ReadLine();
        }

        return null;
    }

    private static OtlpExportProtocol? ToOtlpExportProtocol(string protocol) =>
        protocol.Trim().ToLower() switch
        {
            "grpc" => OtlpExportProtocol.Grpc,
            "http/protobuf" => OtlpExportProtocol.HttpProtobuf,
            _ => null,
        };
}

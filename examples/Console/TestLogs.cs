// <copyright file="TestLogs.cs" company="OpenTelemetry Authors">
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
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;

namespace Examples.Console
{
    internal class TestLogs
    {
        internal static object Run(LogsOptions options)
        {
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddOpenTelemetry((opt) =>
                {
                    opt.IncludeFormattedMessage = true;
                    opt.IncludeScopes = true;
                    if (options.UseExporter.Equals("otlp", StringComparison.OrdinalIgnoreCase))
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
                         *     dotnet run logs --useExporter otlp
                         *
                         * The OpenTelemetry Collector will output all received logs to the stdout of its terminal.
                         *
                         */

                        // Adding the OtlpExporter creates a GrpcChannel.
                        // This switch must be set before creating a GrpcChannel when calling an insecure gRPC service.
                        // See: https://docs.microsoft.com/aspnet/core/grpc/troubleshoot#call-insecure-grpc-services-with-net-core-client
                        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

                        if (options.Protocol.Trim().ToLower().Equals("grpc"))
                        {
                            opt.AddOtlpExporter(otlpOptions =>
                            {
                                otlpOptions.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
                            });
                        }
                        else if (options.Protocol.Trim().ToLower().Equals("http/protobuf"))
                        {
                            opt.AddOtlpExporter(otlpOptions =>
                            {
                                otlpOptions.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
                            });
                        }
                        else
                        {
                            System.Console.WriteLine($"Export protocol {options.Protocol} is not supported. Default protocol 'grpc' will be used.");
                            opt.AddOtlpExporter(otlpOptions =>
                            {
                                otlpOptions.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
                            });
                        }
                    }
                    else
                    {
                        opt.AddConsoleExporter();
                    }
                });
            });

            var logger = loggerFactory.CreateLogger<Program>();
            using (logger.BeginScope("My scope 1 with {food} and {color}", "apple", "green"))
            using (logger.BeginScope("My scope 2 with {food} and {color}", "banana", "yellow"))
            {
                logger.LogInformation("Hello from {name} {price}.", "tomato", 2.99);
            }

            return null;
        }
    }
}

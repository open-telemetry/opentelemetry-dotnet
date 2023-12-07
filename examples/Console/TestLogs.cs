// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace Examples.Console;

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
                     *     dotnet run logs --useExporter otlp -e http://localhost:4317
                     *
                     * The OpenTelemetry Collector will output all received logs to the stdout of its terminal.
                     *
                     */

                    // Adding the OtlpExporter creates a GrpcChannel.
                    // This switch must be set before creating a GrpcChannel when calling an insecure gRPC service.
                    // See: https://docs.microsoft.com/aspnet/core/grpc/troubleshoot#call-insecure-grpc-services-with-net-core-client
                    AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

                    var protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;

                    if (options.Protocol.Trim().ToLower().Equals("grpc"))
                    {
                        protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
                    }
                    else if (options.Protocol.Trim().ToLower().Equals("http/protobuf"))
                    {
                        protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
                    }
                    else
                    {
                        System.Console.WriteLine($"Export protocol {options.Protocol} is not supported. Default protocol 'grpc' will be used.");
                    }

                    var processorType = ExportProcessorType.Batch;
                    if (options.ProcessorType.Trim().ToLower().Equals("batch"))
                    {
                        processorType = ExportProcessorType.Batch;
                    }
                    else if (options.ProcessorType.Trim().ToLower().Equals("simple"))
                    {
                        processorType = ExportProcessorType.Simple;
                    }
                    else
                    {
                        System.Console.WriteLine($"Export processor type {options.ProcessorType} is not supported. Default processor type 'batch' will be used.");
                    }

                    opt.AddOtlpExporter((exporterOptions, processorOptions) =>
                    {
                        exporterOptions.Protocol = protocol;
                        if (!string.IsNullOrWhiteSpace(options.Endpoint))
                        {
                            exporterOptions.Endpoint = new Uri(options.Endpoint);
                        }

                        if (processorType == ExportProcessorType.Simple)
                        {
                            processorOptions.ExportProcessorType = ExportProcessorType.Simple;
                        }
                        else
                        {
                            processorOptions.ExportProcessorType = ExportProcessorType.Batch;
                            processorOptions.BatchExportProcessorOptions = new BatchExportLogRecordProcessorOptions() { ScheduledDelayMilliseconds = options.ScheduledDelayInMilliseconds };
                        }
                    });
                }
                else
                {
                    opt.AddConsoleExporter();
                }
            });
        });

        var logger = loggerFactory.CreateLogger<Program>();
        using (logger.BeginScope("{city}", "Seattle"))
        using (logger.BeginScope("{storeType}", "Physical"))
        {
            logger.LogInformation("Hello from {name} {price}.", "tomato", 2.99);
        }

        return null;
    }
}
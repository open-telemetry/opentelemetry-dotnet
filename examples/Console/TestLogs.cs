// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace Examples.Console;

internal sealed class TestLogs
{
    internal static int Run(LogsOptions options)
    {
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddOpenTelemetry((opt) =>
            {
                opt.IncludeFormattedMessage = true;
                opt.IncludeScopes = true;

                if ("otlp".Equals(options.UseExporter, StringComparison.OrdinalIgnoreCase))
                {
                    /*
                     * Prerequisite to run this example:
                     * Set up an OpenTelemetry Collector to run on local docker.
                     *
                     * Open a terminal window at the examples/Console/ directory and
                     * launch the OpenTelemetry Collector with an OTLP receiver, by running:
                     *
                     *  - On Unix based systems use:
                     *     docker run --rm -it -p 4317:4317 -p 4318:4318 -v $(pwd):/cfg otel/opentelemetry-collector:0.123.0 --config=/cfg/otlp-collector-example/config.yaml
                     *
                     *  - On Windows use:
                     *     docker run --rm -it -p 4317:4317 -p 4318:4318 -v "%cd%":/cfg otel/opentelemetry-collector:0.123.0 --config=/cfg/otlp-collector-example/config.yaml
                     *
                     * Open another terminal window at the examples/Console/ directory and
                     * launch the OTLP example by running:
                     *
                     *     dotnet run logs --useExporter otlp -e http://localhost:4317
                     *
                     * The OpenTelemetry Collector will output all received logs to the stdout of its terminal.
                     *
                     */

                    OpenTelemetry.Exporter.OtlpExportProtocol protocol = default;

                    if (!string.IsNullOrEmpty(options.Protocol))
                    {
                        switch (options.Protocol!.Trim())
                        {
                            case "grpc":
                                protocol = default;
                                break;
                            case "http/protobuf":
                                protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
                                break;
                            default:
                                System.Console.WriteLine($"Export protocol {options.Protocol} is not supported. Default protocol 'grpc' will be used.");
                                break;
                        }
                    }
                    else
                    {
                        System.Console.WriteLine("Protocol is null or empty. Default protocol 'grpc' will be used.");
                    }

                    var processorType = ExportProcessorType.Batch;

                    if (!string.IsNullOrEmpty(options.ProcessorType))
                    {
                        switch (options.ProcessorType!.Trim())
                        {
                            case "batch":
                                processorType = ExportProcessorType.Batch;
                                break;
                            case "simple":
                                processorType = ExportProcessorType.Simple;
                                break;
                            default:
                                System.Console.WriteLine($"Export processor type {options.ProcessorType} is not supported. Default processor type 'batch' will be used.");
                                break;
                        }
                    }
                    else
                    {
                        System.Console.WriteLine("Processor type is null or empty. Default processor type 'batch' will be used.");
                    }

                    opt.AddOtlpExporter((exporterOptions, processorOptions) =>
                    {
                        exporterOptions.Protocol = protocol;
#if NETFRAMEWORK
                        if (exporterOptions.Protocol == default)
                        {
                            exporterOptions.HttpClientFactory = () =>
                            {
                                var handler = new WinHttpHandler
                                {
                                    ServerCertificateValidationCallback = (_, _, _, _) => true,
                                };

                                return new HttpClient(handler)
                                {
                                    Timeout = TimeSpan.FromMilliseconds(exporterOptions.TimeoutMilliseconds),
                                };
                            };
                        }
#endif
                        if (!string.IsNullOrWhiteSpace(options.Endpoint))
                        {
                            exporterOptions.Endpoint = new Uri(options.Endpoint!);
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

        var logger = loggerFactory.CreateLogger<TestLogs>();
        using (logger.BeginCityScope("Seattle"))
        using (logger.BeginStoreTypeScope("Physical"))
        {
            logger.HelloFrom("tomato", 2.99);
        }

        return 0;
    }
}

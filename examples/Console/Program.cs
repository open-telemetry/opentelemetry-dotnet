// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using CommandLine;

namespace Examples.Console;

/// <summary>
/// Main samples entry point.
/// </summary>
internal static class Program
{
    /// <summary>
    /// Main method - invoke this using command line.
    /// For example:
    ///
    /// dotnet run --project Examples.Console.csproj -- console
    /// dotnet run --project Examples.Console.csproj -- inmemory
    /// dotnet run --project Examples.Console.csproj -- zipkin -u http://localhost:9411/api/v2/spans
    /// dotnet run --project Examples.Console.csproj -- prometheus -p 9464
    /// dotnet run --project Examples.Console.csproj -- otlp -e "http://localhost:4317" -p "grpc"
    /// dotnet run --project Examples.Console.csproj -- metrics --help
    ///
    /// To see all available examples in the project run:
    ///
    /// dotnet run --project Examples.Console.csproj -- --help
    ///
    /// The above must be run from the project root folder
    /// (eg: C:\repos\opentelemetry-dotnet\examples\Console\).
    /// </summary>
    /// <param name="args">Arguments from command line.</param>
    public static void Main(string[] args)
    {
        Parser.Default.ParseArguments<ZipkinOptions, PrometheusOptions, MetricsOptions, LogsOptions, GrpcNetClientOptions, HttpClientOptions, ConsoleOptions, OpenTelemetryShimOptions, OpenTracingShimOptions, OtlpOptions, InMemoryOptions>(args)
            .MapResult(
                (ZipkinOptions options) => TestZipkinExporter.Run(options),
                (PrometheusOptions options) => TestPrometheusExporter.Run(options),
                (MetricsOptions options) => TestMetrics.Run(options),
                (LogsOptions options) => TestLogs.Run(options),
                (GrpcNetClientOptions options) => TestGrpcNetClient.Run(options),
                (HttpClientOptions options) => TestHttpClient.Run(options),
                (ConsoleOptions options) => TestConsoleExporter.Run(options),
                (OpenTelemetryShimOptions options) => TestOTelShimWithConsoleExporter.Run(options),
                (OpenTracingShimOptions options) => TestOpenTracingShim.Run(options),
                (OtlpOptions options) => TestOtlpExporter.Run(options),
                (InMemoryOptions options) => TestInMemoryExporter.Run(options),
                errs => 1);
    }
}

#pragma warning disable SA1402 // File may only contain a single type

[Verb("zipkin", HelpText = "Specify the options required to test Zipkin exporter")]
internal class ZipkinOptions
{
    [Option('u', "uri", HelpText = "Please specify the uri of Zipkin backend", Required = true)]
    public required string Uri { get; set; }
}

[Verb("prometheus", HelpText = "Specify the options required to test Prometheus")]
internal class PrometheusOptions
{
    [Option('p', "port", Default = 9464, HelpText = "The port to expose metrics. The endpoint will be http://localhost:port/metrics/ (this is the port from which your Prometheus server scraps metrics from.)", Required = false)]
    public int Port { get; set; }
}

[Verb("metrics", HelpText = "Specify the options required to test Metrics")]
internal class MetricsOptions
{
    [Option('d', "IsDelta", HelpText = "Export Delta metrics", Required = false, Default = false)]
    public bool IsDelta { get; set; }

    [Option('g', "Gauge", HelpText = "Include Observable Gauge.", Required = false, Default = false)]
    public bool? FlagGauge { get; set; }

    [Option('c', "Counter", HelpText = "Include Counter.", Required = false, Default = true)]
    public bool? FlagCounter { get; set; }

    [Option('h', "Histogram", HelpText = "Include Histogram.", Required = false, Default = false)]
    public bool? FlagHistogram { get; set; }

    [Option("defaultCollection", Default = 1000, HelpText = "Default collection period in milliseconds.", Required = false)]
    public int DefaultCollectionPeriodMilliseconds { get; set; }

    [Option("useExporter", Default = "console", HelpText = "Options include otlp or console.", Required = false)]
    public string? UseExporter { get; set; }

    [Option('e', "endpoint", HelpText = "Target to which the exporter is going to send metrics (default value depends on protocol).", Default = null)]
    public string? Endpoint { get; set; }

    [Option('p', "useGrpc", HelpText = "Use gRPC or HTTP when using the OTLP exporter", Required = false, Default = true)]
    public bool UseGrpc { get; set; }
}

[Verb("grpc", HelpText = "Specify the options required to test Grpc.Net.Client")]
internal class GrpcNetClientOptions
{
}

[Verb("httpclient", HelpText = "Specify the options required to test HttpClient")]
internal class HttpClientOptions
{
}

[Verb("console", HelpText = "Specify the options required to test console exporter")]
internal class ConsoleOptions
{
}

[Verb("otelshim", HelpText = "Specify the options required to test OpenTelemetry Shim with console exporter")]
internal class OpenTelemetryShimOptions
{
}

[Verb("opentracing", HelpText = "Specify the options required to test OpenTracing Shim with console exporter")]
internal class OpenTracingShimOptions
{
}

[Verb("otlp", HelpText = "Specify the options required to test OpenTelemetry Protocol (OTLP)")]
internal class OtlpOptions
{
    [Option('e', "endpoint", HelpText = "Target to which the exporter is going to send traces (default value depends on protocol).", Default = null)]
    public string? Endpoint { get; set; }

    [Option('p', "protocol", HelpText = "Transport protocol used by exporter. Supported values: grpc and http/protobuf.", Default = "grpc")]
    public string? Protocol { get; set; }
}

[Verb("logs", HelpText = "Specify the options required to test Logs")]
internal class LogsOptions
{
    [Option("useExporter", Default = "otlp", HelpText = "Options include otlp or console.", Required = false)]
    public string? UseExporter { get; set; }

    [Option('e', "endpoint", HelpText = "Target to which the OTLP exporter is going to send logs (default value depends on protocol).", Default = null)]
    public string? Endpoint { get; set; }

    [Option('p', "protocol", HelpText = "Transport protocol used by OTLP exporter. Supported values: grpc and http/protobuf. Only applicable if Exporter is OTLP", Default = "grpc")]
    public string? Protocol { get; set; }

    [Option("processorType", Default = "batch", HelpText = "export processor type. Supported values: simple and batch", Required = false)]
    public string? ProcessorType { get; set; }

    [Option("scheduledDelay", Default = 5000, HelpText = "The delay interval in milliseconds between two consecutive exports.", Required = false)]
    public int ScheduledDelayInMilliseconds { get; set; }
}

[Verb("inmemory", HelpText = "Specify the options required to test InMemory Exporter")]
internal class InMemoryOptions
{
}

#pragma warning restore SA1402 // File may only contain a single type

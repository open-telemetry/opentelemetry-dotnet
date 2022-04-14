// <copyright file="Program.cs" company="OpenTelemetry Authors">
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

using CommandLine;

namespace Examples.Console
{
    /// <summary>
    /// Main samples entry point.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Main method - invoke this using command line.
        /// For example:
        ///
        /// dotnet run --project Examples.Console.csproj -- console
        /// dotnet run --project Examples.Console.csproj -- inmemory
        /// dotnet run --project Examples.Console.csproj -- zipkin -u http://localhost:9411/api/v2/spans
        /// dotnet run --project Examples.Console.csproj -- jaeger -h localhost -p 6831
        /// dotnet run --project Examples.Console.csproj -- prometheus -p 9464
        /// dotnet run --project Examples.Console.csproj -- otlp -e "http://localhost:4317" -p "grpc"
        /// dotnet run --project Examples.Console.csproj -- zpages
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
            Parser.Default.ParseArguments<JaegerOptions, ZipkinOptions, PrometheusOptions, MetricsOptions, LogsOptions, GrpcNetClientOptions, HttpClientOptions, RedisOptions, ZPagesOptions, ConsoleOptions, OpenTelemetryShimOptions, OpenTracingShimOptions, OtlpOptions, InMemoryOptions>(args)
                .MapResult(
                    (JaegerOptions options) => TestJaegerExporter.Run(options.Host, options.Port),
                    (ZipkinOptions options) => TestZipkinExporter.Run(options.Uri),
                    (PrometheusOptions options) => TestPrometheusExporter.Run(options.Port),
                    (MetricsOptions options) => TestMetrics.Run(options),
                    (LogsOptions options) => TestLogs.Run(options),
                    (GrpcNetClientOptions options) => TestGrpcNetClient.Run(),
                    (HttpClientOptions options) => TestHttpClient.Run(),
                    (RedisOptions options) => TestRedis.Run(options.Uri),
                    (ZPagesOptions options) => TestZPagesExporter.Run(),
                    (ConsoleOptions options) => TestConsoleExporter.Run(options),
                    (OpenTelemetryShimOptions options) => TestOTelShimWithConsoleExporter.Run(options),
                    (OpenTracingShimOptions options) => TestOpenTracingShim.Run(options),
                    (OtlpOptions options) => TestOtlpExporter.Run(options.Endpoint, options.Protocol),
                    (InMemoryOptions options) => TestInMemoryExporter.Run(options),
                    errs => 1);
        }
    }

#pragma warning disable SA1402 // File may only contain a single type

    [Verb("jaeger", HelpText = "Specify the options required to test Jaeger exporter")]
    internal class JaegerOptions
    {
        [Option('h', "host", HelpText = "Host of the Jaeger Agent", Default = "localhost")]
        public string Host { get; set; }

        [Option('p', "port", HelpText = "Port of the Jaeger Agent", Default = 6831)]
        public int Port { get; set; }
    }

    [Verb("zipkin", HelpText = "Specify the options required to test Zipkin exporter")]
    internal class ZipkinOptions
    {
        [Option('u', "uri", HelpText = "Please specify the uri of Zipkin backend", Required = true)]
        public string Uri { get; set; }
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
        public string UseExporter { get; set; }

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

    [Verb("redis", HelpText = "Specify the options required to test Redis with Zipkin")]
    internal class RedisOptions
    {
        [Option('u', "uri", HelpText = "Please specify the uri of Zipkin backend", Required = true)]
        public string Uri { get; set; }
    }

    [Verb("zpages", HelpText = "Specify the options required to test ZPages")]
    internal class ZPagesOptions
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
        [Option('e', "endpoint", HelpText = "Target to which the exporter is going to send traces or metrics (default value depends on protocol).", Default = null)]
        public string Endpoint { get; set; }

        [Option('p', "protocol", HelpText = "Transport protocol used by exporter. Supported values: grpc and http/protobuf.", Default = "grpc")]
        public string Protocol { get; set; }
    }

    [Verb("logs", HelpText = "Specify the options required to test Logs")]
    internal class LogsOptions
    {
        [Option("useExporter", Default = "otlp", HelpText = "Options include otlp or console.", Required = false)]
        public string UseExporter { get; set; }
    }

    [Verb("inmemory", HelpText = "Specify the options required to test InMemory Exporter")]
    internal class InMemoryOptions
    {
    }

#pragma warning restore SA1402 // File may only contain a single type

}

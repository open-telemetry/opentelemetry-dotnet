﻿// <copyright file="Program.cs" company="OpenTelemetry Authors">
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
using CommandLine;

namespace Samples
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
        /// dotnet Exporters.dll zipkin -u http://localhost:9411/api/v2/spans
        /// dotnet Exporters.dll jaeger -h localhost -o 6831
        /// dotnet Exporters.dll prometheus -i 15 -p 9184 -d 2
        ///
        /// The above must be run from the project bin folder
        /// (eg: C:\repos\opentelemetry-dotnet\src\samples\Exporters\Console\bin\Debug\netcoreapp3.1).
        /// </summary>
        /// <param name="args">Arguments from command line.</param>
        public static void Main(string[] args)
        {
            Parser.Default.ParseArguments<JaegerOptions, ZipkinOptions, PrometheusOptions, HttpClientOptions, ZPagesOptions, ConsoleOptions, ConsoleActivityOptions, OtlpOptions>(args)
                .MapResult(
                    (JaegerOptions options) => TestJaeger.Run(options.Host, options.Port),
                    (ZipkinOptions options) => TestZipkin.Run(options.Uri),
                    (PrometheusOptions options) => TestPrometheus.RunAsync(options.Port, options.PushIntervalInSecs, options.DurationInMins),
                    (HttpClientOptions options) => TestHttpClient.Run(),
                    (RedisOptions options) => TestRedis.Run(options.Uri),
                    (ZPagesOptions options) => TestZPages.Run(),
                    (ConsoleOptions options) => TestConsole.Run(options),
                    (ConsoleActivityOptions options) => TestConsoleActivity.Run(options),
                    (OtlpOptions options) => TestOtlp.Run(options.Endpoint, options.UseActivitySource),
                    errs => 1);

            Console.ReadLine();
        }
    }

#pragma warning disable SA1402 // File may only contain a single type

    [Verb("jaeger", HelpText = "Specify the options required to test Jaeger exporter")]
    internal class JaegerOptions
    {
        [Option('h', "host", HelpText = "Please specify the host of the Jaeger Agent", Required = true)]
        public string Host { get; set; }

        [Option('p', "port", HelpText = "Please specify the port of the Jaeger Agent", Required = true)]
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
        [Option('i', "pushIntervalInSecs", Default = 15, HelpText = "The interval at which Push controller pushes metrics.", Required = false)]
        public int PushIntervalInSecs { get; set; }

        [Option('p', "port", Default = 9184, HelpText = "The port to expose metrics. The endpoint will be http://localhost:port/metrics (This is the port from which your Prometheus server scraps metrics from.)", Required = false)]
        public int Port { get; set; }

        [Option('d', "duration", Default = 2, HelpText = "Total duration in minutes to run the demo. Run atleast for a min to see metrics flowing.", Required = false)]
        public int DurationInMins { get; set; }
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
        [Option('p', "pretty", HelpText = "Specify if the output should be pretty printed (default: true)", Default = true)]
        public bool Pretty { get; set; }
    }

    [Verb("consoleactivity", HelpText = "Specify the options required to test console activity exporter")]
    internal class ConsoleActivityOptions
    {
        [Option('p', "displayasjson", HelpText = "Specify if the output should be displayed as json or not (default: false)", Default = false)]
        public bool DisplayAsJson { get; set; }
    }

    [Verb("otlp", HelpText = "Specify the options required to test OpenTelemetry Protocol (OTLP)")]
    internal class OtlpOptions
    {
        [Option('e', "endpoint", HelpText = "Target to which the exporter is going to send traces or metrics", Default = "localhost:55680")]
        public string Endpoint { get; set; }

        [Option('a', "activity", HelpText = "Set it to true to export ActivitySource data", Default = false)]
        public bool UseActivitySource { get; set; }
    }

#pragma warning restore SA1402 // File may only contain a single type

}

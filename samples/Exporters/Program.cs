// <copyright file="Program.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
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

namespace Samples
{
    using System;
    using CommandLine;

    /// <summary>
    /// Main samples entry point.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Main method - invoke this using command line.
        /// For example:
        ///
        /// Samples.dll zipkin http://localhost:9411/api/v2/spans
        /// Sample.dll appInsights
        /// Sample.dll prometheus.
        /// </summary>
        /// <param name="args">Arguments from command line.</param>
        public static void Main(string[] args)
        {
            Parser.Default.ParseArguments<JaegerOptions, ZipkinOptions, ApplicationInsightsOptions, PrometheusOptions, HttpClientOptions, StackdriverOptions, LightStepOptions>(args)
                .MapResult(
                    (JaegerOptions options) => TestJaeger.Run(options.Host, options.Port),
                    (ZipkinOptions options) => TestZipkin.Run(options.Uri),
                    (ApplicationInsightsOptions options) => TestApplicationInsights.Run(),
                    (PrometheusOptions options) => TestPrometheus.Run(),
                    (HttpClientOptions options) => TestHttpClient.Run(),
                    (RedisOptions options) => TestRedis.Run(options.Uri),
                    (StackdriverOptions options) => TestStackdriver.Run(options.ProjectId),
                    (LightStepOptions options) => TestLightstep.Run(options.AccessToken),
                    errs => 1);

            Console.ReadLine();
        }
    }

    [Verb("lightstep", HelpText = "Specify the LightStep access token", Hidden = false)]
#pragma warning disable SA1402 // File may only contain a single type
    internal class LightStepOptions
    {
        [Option('t', "accessToken", HelpText = "Please specify the access token for your LightStep project", Required = true)]
        public string AccessToken { get; set; }
    }
    
    [Verb("stackdriver", HelpText = "Specify the options required to test Stackdriver exporter", Hidden = false)]
#pragma warning disable SA1402 // File may only contain a single type
    internal class StackdriverOptions
    {
        [Option('p', "projectId", HelpText = "Please specify the projectId of your GCP project", Required = true)]
        public string ProjectId { get; set; }
    }

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

    [Verb("appInsights", HelpText = "Specify the options required to test ApplicationInsights")]
    internal class ApplicationInsightsOptions
    {
    }

    [Verb("prometheus", HelpText = "Specify the options required to test Prometheus")]
    internal class PrometheusOptions
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

#pragma warning restore SA1402 // File may only contain a single type

}

namespace Samples
{
    using CommandLine;
    using System;

    [Verb("stackdriver", HelpText = "Specify the options required to test Stackdriver exporter", Hidden = false)]
    class StackdriverOptions
    {
        [Option('p', "projectId", HelpText = "Please specify the projectId of your GCP project", Required = true)]
        public string ProjectId { get; set; }
    }

    [Verb("jaeger", HelpText = "Specify the options required to test Jaeger exporter")]
    class JaegerOptions
    {
        [Option('h', "host", HelpText = "Please specify the host of the Jaeger Agent", Required = true)]
        public string Host { get; set; }

        [Option('p', "port", HelpText = "Please specify the port of the Jaeger Agent", Required = true)]
        public int Port { get; set; }
    }

    [Verb("zipkin", HelpText = "Specify the options required to test Zipkin exporter")]
    class ZipkinOptions
    {
        [Option('u', "uri", HelpText = "Please specify the uri of Zipkin backend", Required = true)]
        public string Uri { get; set; }
    }

    [Verb("appInsights", HelpText = "Specify the options required to test ApplicationInsights")]
    class ApplicationInsightsOptions
    {
    }

    [Verb("prometheus", HelpText = "Specify the options required to test Prometheus")]
    class PrometheusOptions
    {
    }

    [Verb("httpclient", HelpText = "Specify the options required to test HttpClient")]
    class HttpClientOptions
    {
    }

    [Verb("redis", HelpText = "Specify the options required to test Redis with Zipkin")]
    class RedisOptions
    {
        [Option('u', "uri", HelpText = "Please specify the uri of Zipkin backend", Required = true)]
        public string Uri { get; set; }
    }

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
        /// Sample.dll prometheus
        /// </summary>
        /// <param name="args">Arguments from command line.</param>
        public static void Main(string[] args)
        {
            Parser.Default.ParseArguments<JaegerOptions, ZipkinOptions, ApplicationInsightsOptions, PrometheusOptions, HttpClientOptions, StackdriverOptions>(args)
                .MapResult(
                    (JaegerOptions options) => TestJaeger.Run(options.Host, options.Port),
                    (ZipkinOptions options) => TestZipkin.Run(options.Uri),
                    (ApplicationInsightsOptions options) => TestApplicationInsights.Run(),
                    (PrometheusOptions options) => TestPrometheus.Run(),
                    (HttpClientOptions options) => TestHttpClient.Run(),
                    (RedisOptions options) => TestRedis.Run(options.Uri),
                    (StackdriverOptions options) => TestStackdriver.Run(options.ProjectId),
                    errs => 1);

            Console.ReadLine();
        }
    }
}

// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Trace;
using Utils.Messaging;

namespace WorkerService;

internal static class Program
{
    public static void Main(string[] args)
    {
        CreateHostBuilder(args).Build().Run();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices((hostContext, services) =>
            {
                services.AddHostedService<Worker>();

                services.AddSingleton<MessageReceiver>();

                services.AddOpenTelemetry()
                    .WithTracing(builder => builder
                        .AddSource(nameof(MessageReceiver))
                        .AddZipkinExporter(b =>
                        {
                            var zipkinHostName = Environment.GetEnvironmentVariable("ZIPKIN_HOSTNAME") ?? "localhost";
                            b.Endpoint = new Uri($"http://{zipkinHostName}:9411/api/v2/spans");
                        }));
            });
}

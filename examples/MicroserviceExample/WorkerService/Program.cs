using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Trace;

namespace WorkerService
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHostedService<RabbitMqConsumer>();

                    // TODO: Determine if this can be done here in a WorkerService. It does not seem to work... doing this in the RabbitMqConsumer for now.
                    // services.AddOpenTelemetry((builder) =>
                    // {
                    //     builder
                    //         .AddActivitySource(nameof(RabbitMqConsumer))
                    //         .UseZipkinExporter(b =>
                    //         {
                    //             var zipkinHostName = Environment.GetEnvironmentVariable("ZIPKIN_HOSTNAME") ?? "localhost";
                    //             b.ServiceName = "Worker";
                    //             b.Endpoint = new Uri($"http://{zipkinHostName}:9411/api/v2/spans");
                    //         });
                    // });
                });
    }
}

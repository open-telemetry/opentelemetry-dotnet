using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Utils.Messaging;

namespace WorkerService
{
    public partial class Worker : BackgroundService
    {
        private readonly MessageReceiver messageReceiver;
        private readonly TracerProvider tracerProvider;

        public Worker(MessageReceiver messageReceiver)
        {
            this.messageReceiver = messageReceiver;

            this.tracerProvider = Sdk.CreateTracerProvider((builder) =>
            {
                builder
                    .AddActivitySource(nameof(MessageReceiver))
                    .UseZipkinExporter(b =>
                    {
                        var zipkinHostName = Environment.GetEnvironmentVariable("ZIPKIN_HOSTNAME") ?? "localhost";
                        b.ServiceName = nameof(WorkerService);
                        b.Endpoint = new Uri($"http://{zipkinHostName}:9411/api/v2/spans");
                    });
            });
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            return base.StartAsync(cancellationToken);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            await base.StopAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            stoppingToken.ThrowIfCancellationRequested();

            this.messageReceiver.StartConsumer();

            await Task.CompletedTask;
        }
    }
}

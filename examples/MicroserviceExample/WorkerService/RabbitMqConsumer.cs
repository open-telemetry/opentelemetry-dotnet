using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Trace;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace WorkerService
{
    public class RabbitMqConsumer : BackgroundService
    {
        private const string QueueName = "TestQueue";

        private readonly ILogger<RabbitMqConsumer> logger;

        private IConnection connection;
        private IModel channel;
        private ActivitySource activitySource;
        private ITextFormat textFormat;
        private TracerProvider tracerProvider;

        public RabbitMqConsumer(ILogger<RabbitMqConsumer> logger)
        {
            this.logger = logger;
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            this.tracerProvider = Sdk.CreateTracerProvider((builder) =>
            {
                builder
                    .AddActivitySource(nameof(RabbitMqConsumer))
                    .UseZipkinExporter(b =>
                    {
                        var zipkinHostName = Environment.GetEnvironmentVariable("ZIPKIN_HOSTNAME") ?? "localhost";
                        b.ServiceName = nameof(RabbitMqConsumer);
                        b.Endpoint = new Uri($"http://{zipkinHostName}:9411/api/v2/spans");
                    });
            });

            var connectionFactory = new ConnectionFactory()
            {
                HostName = Environment.GetEnvironmentVariable("RABBIT_HOSTNAME") ?? "localhost",
                UserName = "guest",
                Password = "guest",
                Port = 5672,
                DispatchConsumersAsync = true,
                RequestedConnectionTimeout = TimeSpan.FromMilliseconds(3000),
            };

            this.connection = connectionFactory.CreateConnection();
            this.channel = this.connection.CreateModel();
            this.channel.QueueDeclarePassive(QueueName);
            this.channel.BasicQos(0, 1, false);

            this.activitySource = new ActivitySource(nameof(RabbitMqConsumer));

            this.textFormat = new TraceContextFormat();

            return base.StartAsync(cancellationToken);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            await base.StopAsync(cancellationToken);
            this.tracerProvider.Dispose();
            this.connection.Dispose();
            this.channel.Dispose();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            stoppingToken.ThrowIfCancellationRequested();

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.Received += async (bc, ea) =>
            {
                var parentContext = this.textFormat.Extract(ea.BasicProperties, ExtractTraceContextFromBasicProperties);

                string activityName = $"{nameof(RabbitMqConsumer)}.{nameof(ExecuteAsync)}";
                using (var activity = activitySource.StartActivity(activityName, ActivityKind.Server, parentContext))
                {
                    try
                    {
                        var message = Encoding.UTF8.GetString(ea.Body.Span);
                        activity.AddTag("message", message);

                        this.logger.LogInformation($"Received message: {message}.");

                        await Task.Delay(new Random().Next(1, 3) * 1000, stoppingToken);

                        channel.BasicAck(ea.DeliveryTag, false);
                    }
                    catch (Exception ex)
                    {
                        this.logger.LogError(ex, ex.Message);
                    }
                }
            };

            channel.BasicConsume(queue: QueueName, autoAck: false, consumer: consumer);

            await Task.CompletedTask;
        }

        private IEnumerable<string> ExtractTraceContextFromBasicProperties(IBasicProperties props, string key)
        {
            try
            {
                if (props.Headers.TryGetValue(key, out object value))
                {
                    var bytes = value as byte[];
                    return new[] { Encoding.UTF8.GetString(bytes) };
                }
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to extract trace context.");
            }

            return Enumerable.Empty<string>();
        }
    }
}

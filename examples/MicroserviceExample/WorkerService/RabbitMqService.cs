using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace WorkerService
{
    public partial class RabbitMqService : BackgroundService
    {
        private const string QueueName = "TestQueue";

        private readonly MessageProcessor receiver;

        private IConnection connection;
        private IModel channel;

        public RabbitMqService(MessageProcessor receiver)
        {
            this.receiver = receiver;
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
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

            return base.StartAsync(cancellationToken);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            await base.StopAsync(cancellationToken);
            this.connection.Dispose();
            this.channel.Dispose();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            stoppingToken.ThrowIfCancellationRequested();

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.Received += async (bc, ea) =>
            {
                await this.receiver.ProcessMessage(ea);
            };

            channel.BasicConsume(queue: QueueName, autoAck: true, consumer: consumer);

            await Task.CompletedTask;
        }
    }
}

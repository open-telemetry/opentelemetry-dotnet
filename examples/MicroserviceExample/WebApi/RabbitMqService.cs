using System;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace WebApi
{
    public class RabbitMqService
    {
        private const string QueueName = "TestQueue";

        private readonly ILogger<RabbitMqService> logger;
        private readonly MessageSender messageSender;
        private readonly ConnectionFactory connectionFactory;
        private readonly IConnection connection;
        private readonly IModel channel;

        public RabbitMqService(ILogger<RabbitMqService> logger, MessageSender messageSender)
        {
            this.logger = logger;

            this.messageSender = messageSender;

            this.connectionFactory = new ConnectionFactory()
            {
                HostName = Environment.GetEnvironmentVariable("RABBITMQ_HOSTNAME") ?? "localhost",
                UserName = Environment.GetEnvironmentVariable("RABBITMQ_DEFAULT_USER") ?? "localhost",
                Password = Environment.GetEnvironmentVariable("RABBITMQ_DEFAULT_PASS") ?? "localhost",
                Port = 5672,
                RequestedConnectionTimeout = TimeSpan.FromMilliseconds(3000),
            };

            this.connection = connectionFactory.CreateConnection();
            this.channel = connection.CreateModel();
            channel.QueueDeclare(
                queue: QueueName,
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: null);
        }

        public string PublishMessage()
        {
            try
            {
                return this.messageSender.PublishMessage(channel, QueueName);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex.ToString());
                throw;
            }
        }

        public void Dispose()
        {
            this.connection.Dispose();
            this.channel.Dispose();
        }
    }
}

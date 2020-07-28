using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Context.Propagation;
using RabbitMQ.Client;

namespace WebApi
{
    public interface IRabbitMqService : IDisposable
    {
        string PublishMessage();
    }

    public class RabbitMqService : IRabbitMqService
    {
        private const string QueueName = "TestQueue";

        private readonly ILogger<RabbitMqService> logger;
        private readonly ConnectionFactory connectionFactory;
        private readonly IConnection connection;
        private readonly IModel channel;
        private readonly ActivitySource activitySource;
        private readonly ITextFormat textFormat;

        public RabbitMqService(ILogger<RabbitMqService> logger)
        {
            this.logger = logger;

            this.connectionFactory = new ConnectionFactory()
            {
                HostName = Environment.GetEnvironmentVariable("RABBIT_HOSTNAME") ?? "localhost",
                UserName = "guest",
                Password = "guest",
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

            this.activitySource = new ActivitySource(nameof(RabbitMqService));

            this.textFormat = new TraceContextFormat();
        }

        public string PublishMessage()
        {
            try
            {
                string activityName = $"{nameof(RabbitMqService)}.{nameof(PublishMessage)}";
                using (var activity = activitySource.StartActivity(activityName))
                {
                    var props = this.channel.CreateBasicProperties();
                    props.ContentType = "text/plain";
                    props.DeliveryMode = 2;

                    textFormat.Inject(activity.Context, props, InjectTraceContextIntoBasicProperties);

                    var body = $"Published message. DateTime.Now = {DateTime.Now}.";

                    this.channel.BasicPublish(
                        exchange: string.Empty,
                        routingKey: QueueName,
                        basicProperties: props,
                        body: Encoding.UTF8.GetBytes(body));

                    this.logger.LogInformation($"Published message: {body}.");

                    return body;
                }
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
            this.activitySource.Dispose();
        }

        private void InjectTraceContextIntoBasicProperties(IBasicProperties props, string key, string value)
        {
            try
            {
                if (props.Headers == null)
                {
                    props.Headers = new Dictionary<string, object>();
                }

                props.Headers[key] = value;
            }
            catch (Exception ex)
            {
                this.logger.LogError($"Failed to inject trace context: {ex}");
            }
        }
    }
}

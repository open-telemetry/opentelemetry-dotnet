using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Context.Propagation;
using RabbitMQ.Client;

namespace WebApi
{
    public class MessageSender
    {
        private static readonly ITextFormat TextFormat = new TraceContextFormat();

        private readonly ActivitySource activitySource = new ActivitySource(nameof(MessageSender));
        private readonly ILogger<MessageSender> logger;

        public MessageSender(ILogger<MessageSender> logger)
        {
            this.activitySource = new ActivitySource(nameof(MessageSender));
            this.logger = logger;
        }

        internal string PublishMessage(IModel channel, string queueName)
        {
            // Start an activity with a name following the semantic convention of the OpenTelemetry messaging specification.
            // https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/trace/semantic_conventions/messaging.md#span-name
            string activityName = $"{queueName} send";
            using (var activity = activitySource.StartActivity(activityName))
            {
                var props = channel.CreateBasicProperties();

                // Inject the ActivityContext into the message headers.
                TextFormat.Inject(activity.Context, props, InjectTraceContextIntoBasicProperties);

                var body = $"Published message: DateTime.Now = {DateTime.Now}.";

                channel.BasicPublish(
                    exchange: string.Empty,
                    routingKey: queueName,
                    basicProperties: props,
                    body: Encoding.UTF8.GetBytes(body));

                return body;
            }
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

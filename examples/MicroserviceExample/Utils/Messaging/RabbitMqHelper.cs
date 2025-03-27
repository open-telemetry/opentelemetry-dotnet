// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Utils.Messaging;

public static class RabbitMqHelper
{
    public const string DefaultExchangeName = "";
    public const string TestQueueName = "TestQueue";

    private static readonly ConnectionFactory ConnectionFactory = new()
    {
        HostName = Environment.GetEnvironmentVariable("RABBITMQ_HOSTNAME") ?? "localhost",
        UserName = Environment.GetEnvironmentVariable("RABBITMQ_DEFAULT_USER") ?? "guest",
        Password = Environment.GetEnvironmentVariable("RABBITMQ_DEFAULT_PASS") ?? "guest",
        Port = 5672,
        RequestedConnectionTimeout = TimeSpan.FromMilliseconds(3000),
    };

    public static IConnection CreateConnection()
    {
        return ConnectionFactory.CreateConnection();
    }

    public static IModel CreateModelAndDeclareTestQueue(IConnection connection)
    {
        if (connection == null)
        {
            throw new ArgumentNullException(nameof(connection));
        }

        var channel = connection.CreateModel();

        channel.QueueDeclare(
            queue: TestQueueName,
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        return channel;
    }

    public static void StartConsumer(IModel channel, Action<BasicDeliverEventArgs> processMessage)
    {
        var consumer = new EventingBasicConsumer(channel);

        consumer.Received += (bc, ea) => processMessage(ea);

        channel.BasicConsume(queue: TestQueueName, autoAck: true, consumer: consumer);
    }

    public static void AddMessagingTags(Activity? activity)
    {
        // These tags are added demonstrating the semantic conventions of the OpenTelemetry messaging specification
        // See:
        //   * https://github.com/open-telemetry/semantic-conventions/blob/main/docs/messaging/messaging-spans.md#messaging-attributes
        //   * https://github.com/open-telemetry/semantic-conventions/blob/main/docs/messaging/rabbitmq.md
        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination_kind", "queue");
        activity?.SetTag("messaging.destination", DefaultExchangeName);
        activity?.SetTag("messaging.rabbitmq.routing_key", TestQueueName);
    }
}

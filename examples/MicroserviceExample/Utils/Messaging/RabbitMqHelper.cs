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

    public static async Task<IConnection> CreateConnectionAsync() =>
        await ConnectionFactory.CreateConnectionAsync().ConfigureAwait(false);

    public static async Task<IChannel> CreateModelAndDeclareTestQueueAsync(IConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var channel = await connection.CreateChannelAsync().ConfigureAwait(false);

        await channel.QueueDeclareAsync(
            queue: TestQueueName,
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: null).ConfigureAwait(false);

        return channel;
    }

    public static async Task StartConsumerAsync(IChannel channel, Func<BasicDeliverEventArgs, Task> processMessage)
    {
        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (bc, ea) => await processMessage(ea).ConfigureAwait(false);

        await channel.BasicConsumeAsync(queue: TestQueueName, autoAck: true, consumer: consumer).ConfigureAwait(false);
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

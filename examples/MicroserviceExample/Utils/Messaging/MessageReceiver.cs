// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Utils.Messaging;

public sealed class MessageReceiver : IDisposable
{
    private static readonly ActivitySource ActivitySource = new(nameof(MessageReceiver));
    private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;

    private readonly ILogger<MessageReceiver> logger;
    private IConnection? connection;
    private IChannel? channel;

    public MessageReceiver(ILogger<MessageReceiver> logger)
    {
        this.logger = logger;
    }

    public void Dispose()
    {
        this.channel?.Dispose();
        this.connection?.Dispose();
    }

    public async Task StartConsumerAsync()
    {
        this.connection = await RabbitMqHelper.CreateConnectionAsync().ConfigureAwait(false);
        this.channel = await RabbitMqHelper.CreateModelAndDeclareTestQueueAsync(this.connection).ConfigureAwait(false);
        await RabbitMqHelper.StartConsumerAsync(this.channel, this.ReceiveMessageAsync).ConfigureAwait(false);
    }

    public async Task ReceiveMessageAsync(BasicDeliverEventArgs ea)
    {
        this.EnsureStarted();

        ArgumentNullException.ThrowIfNull(ea);

        // Extract the PropagationContext of the upstream parent from the message headers.
        var parentContext = Propagator.Extract(default, ea.BasicProperties, this.ExtractTraceContextFromBasicProperties);
        Baggage.Current = parentContext.Baggage;

        // Start an activity with a name following the semantic convention of the OpenTelemetry messaging specification.
        // https://github.com/open-telemetry/semantic-conventions/blob/main/docs/messaging/messaging-spans.md#span-name
        var activityName = $"{ea.RoutingKey} receive";

        using var activity = ActivitySource.StartActivity(activityName, ActivityKind.Consumer, parentContext.ActivityContext);
        try
        {
            var message = Encoding.UTF8.GetString(ea.Body.Span.ToArray());

            this.logger.MessageReceived(message);

            activity?.SetTag("message", message);

            // The OpenTelemetry messaging specification defines a number of attributes. These attributes are added here.
            RabbitMqHelper.AddMessagingTags(activity);

            // Simulate some work
            await Task.Delay(1_000).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            this.logger.MessageProcessingFailed(ex);
        }
    }

    private IEnumerable<string> ExtractTraceContextFromBasicProperties(IReadOnlyBasicProperties props, string key)
    {
        try
        {
            if (props.Headers?.TryGetValue(key, out var value) is true)
            {
                var bytes = (byte[])value!;
                return [Encoding.UTF8.GetString(bytes)];
            }
        }
        catch (Exception ex)
        {
            this.logger.FailedToExtractTraceContext(ex);
        }

        return [];
    }

    [MemberNotNull(nameof(channel), nameof(connection))]
    private void EnsureStarted()
    {
        if (this.channel == null || this.connection == null)
        {
            throw new InvalidOperationException("The message sender has not been started.");
        }
    }
}

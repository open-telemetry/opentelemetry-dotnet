// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Utils.Messaging;

namespace WorkerService;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class Worker : BackgroundService
#pragma warning restore CA1812 // Avoid uninstantiated internal classes
{
    private readonly MessageReceiver messageReceiver;

    public Worker(MessageReceiver messageReceiver)
    {
        this.messageReceiver = messageReceiver;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        stoppingToken.ThrowIfCancellationRequested();

        this.messageReceiver.StartConsumer();

        await Task.CompletedTask.ConfigureAwait(false);
    }
}

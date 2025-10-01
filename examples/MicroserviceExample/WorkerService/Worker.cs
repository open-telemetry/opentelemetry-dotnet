// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Utils.Messaging;

namespace WorkerService;

internal sealed class Worker : BackgroundService
{
    private readonly MessageReceiver messageReceiver;

    public Worker(MessageReceiver messageReceiver)
    {
        this.messageReceiver = messageReceiver;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        stoppingToken.ThrowIfCancellationRequested();

        await this.messageReceiver.StartConsumerAsync().ConfigureAwait(false);
    }
}

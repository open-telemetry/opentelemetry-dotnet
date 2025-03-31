// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Utils.Messaging;

namespace WorkerService;

internal partial class Worker : BackgroundService
{
    private readonly MessageReceiver messageReceiver;

    public Worker(MessageReceiver messageReceiver)
    {
        this.messageReceiver = messageReceiver;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        return base.StartAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        stoppingToken.ThrowIfCancellationRequested();

        this.messageReceiver.StartConsumer();

        await Task.CompletedTask.ConfigureAwait(false);
    }
}

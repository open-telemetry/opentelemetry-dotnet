// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using DotNet.Testcontainers.Containers;

namespace OpenTelemetry.Tests;

public abstract class ContainerFixture : IAsyncDisposable
{
    private static readonly TimeSpan DisposeTimeout = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan StartTimeout = TimeSpan.FromMinutes(5);

    private bool started;

    protected abstract IContainer Container { get; }

    protected abstract string DockerfileName { get; }

    public virtual async ValueTask DisposeAsync()
    {
        if (this.started)
        {
            // IAsyncDisposable cannot be cancelled, so a stalled teardown is abandoned
            // rather than being allowed to block the test run from finishing.
            await WithTimeoutAsync(
                this.Container.DisposeAsync().AsTask(),
                DisposeTimeout,
                $"The {this.GetType().Name} container was not disposed within {DisposeTimeout.TotalSeconds} seconds.")
                .ConfigureAwait(false);
        }

        GC.SuppressFinalize(this);
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (!this.started)
        {
            using var timeout = new CancellationTokenSource(StartTimeout);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

            try
            {
                await this.Container.StartAsync(linked.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException(
                    $"The {this.GetType().Name} container did not start within {StartTimeout.TotalSeconds} seconds.");
            }

            this.started = true;
        }
    }

    public Uri GetBaseAddress(int port) =>
        new UriBuilder(Uri.UriSchemeHttp, this.Container.Hostname, this.Container.GetMappedPublicPort(port)).Uri;

    protected string GetImage()
    {
        var assembly = this.GetType().Assembly;

        using var stream = assembly.GetManifestResourceStream(this.DockerfileName);

#if NET
        using var reader = new StreamReader(stream!);
#else
        using var reader = new StreamReader(stream);
#endif

        var raw = reader.ReadToEnd();

        // Exclude FROM
        return raw.Substring(4).Trim();
    }

    private static async Task WithTimeoutAsync(Task task, TimeSpan timeout, string message)
    {
        using var cts = new CancellationTokenSource(timeout);

#if NET8_0_OR_GREATER
        await task.WaitAsync(cts.Token).ConfigureAwait(false);
#else
        var completed = await Task.WhenAny(task, Task.Delay(timeout, cts.Token)).ConfigureAwait(false);

        if (completed != task)
        {
            _ = task.ContinueWith(
                static (p) => _ = p.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);

            throw new TimeoutException(message);
        }

        await task.ConfigureAwait(false);
#endif
    }
}

// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using DotNet.Testcontainers.Containers;

namespace OpenTelemetry.Tests;

public abstract class ContainerFixture : IAsyncDisposable
{
    private bool started;

    protected abstract IContainer Container { get; }

    protected abstract string DockerfileName { get; }

    public async ValueTask DisposeAsync()
    {
        if (this.started)
        {
            await this.Container.DisposeAsync().ConfigureAwait(false);
        }

        GC.SuppressFinalize(this);
    }

    public async Task StartAsync()
    {
        if (!this.started)
        {
            await this.Container.StartAsync().ConfigureAwait(false);
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
}

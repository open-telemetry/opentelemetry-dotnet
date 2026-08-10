// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace OpenTelemetry.Exporter.Prometheus.Tests;

public sealed class PromToolFixture : PrometheusFixture
{
    private const string DockerInternalHost = "host.docker.internal";

    private static readonly TimeSpan CheckMetricsTimeout = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan ScrapeTimeout = TimeSpan.FromSeconds(30);

    public async Task<ExecResult> CheckMetricsAsync(
        Uri targetUri,
        string accept,
        CancellationToken cancellationToken = default)
    {
        // Route the request through Docker's internal host to
        // avoid issues with localhost resolution inside the container
        var metricsUri = new UriBuilder(targetUri)
        {
            Host = DockerInternalHost,
        };

        // Use wget to fetch the metrics and pipe them to promtool for validation.
        // The metrics text is output to a temporary file so that we can capture
        // the response to print to stdout to aid with debugging if necessary.
        string[] command =
        [
            "sh",
            "-c",
            $"set -eu;" +
            $"tmp=/tmp/metrics.$$;" +
            $"wget -qO \"$tmp\" -T {ScrapeTimeout.TotalSeconds:F0} --header=\"Accept: {accept}\" --header=\"Host: {targetUri.Host}\" \"{metricsUri}\"; " +
            $"cat \"$tmp\"; " +
            $"promtool check metrics --lint=all < \"$tmp\"",
        ];

        using var timeout = new CancellationTokenSource(CheckMetricsTimeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

        try
        {
            return await this.Container
                .ExecAsync(command, linked.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"promtool did not finish checking the metrics from {targetUri} within {CheckMetricsTimeout.TotalSeconds} seconds.");
        }
    }

    protected override IContainer CreateContainer() =>
        new ContainerBuilder(this.GetImage())
            .WithEntrypoint("sh", "-c")
            .WithCommand("sleep infinity")
            .WithExtraHost(DockerInternalHost, "host-gateway")
            .Build();
}

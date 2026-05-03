// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace OpenTelemetry.Exporter.Prometheus.Tests;

public sealed class PromToolFixture : PrometheusFixture
{
    public async Task<ExecResult> CheckMetricsAsync(
        Uri targetUri,
        string accept,
        CancellationToken cancellationToken = default)
    {
        // Route the request through Docker's internal host to
        // avoid issues with localhost resolution inside the container
        var metricsUri = new UriBuilder(targetUri)
        {
            Host = "host.docker.internal",
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
            $"wget -qO \"$tmp\" --header=\"Accept: {accept}\" --header=\"Host: {targetUri.Host}\" \"{metricsUri}\"; " +
            $"cat \"$tmp\"; " +
            $"promtool check metrics --lint=all < \"$tmp\"",
        ];

        return await this.Container
            .ExecAsync(command, cancellationToken)
            .ConfigureAwait(false);
    }

    protected override IContainer CreateContainer() =>
        new ContainerBuilder(this.GetImage())
            .WithEntrypoint("sh", "-c")
            .WithCommand("sleep infinity")
            .Build();
}

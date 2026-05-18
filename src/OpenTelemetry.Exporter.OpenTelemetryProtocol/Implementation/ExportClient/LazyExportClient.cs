// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;

internal sealed class LazyExportClient : IExportClient
{
    private readonly Lazy<IExportClient> exportClient;

    public LazyExportClient(Func<IExportClient> exportClientFactory)
    {
        this.exportClient = new(exportClientFactory ?? throw new ArgumentNullException(nameof(exportClientFactory)));
    }

    public ExportClientResponse SendExportRequest(byte[] buffer, int contentLength, DateTime deadlineUtc, CancellationToken cancellationToken = default)
        => this.exportClient.Value.SendExportRequest(buffer, contentLength, deadlineUtc, cancellationToken);

    public bool Shutdown(int timeoutMilliseconds)
        => !this.exportClient.IsValueCreated || this.exportClient.Value.Shutdown(timeoutMilliseconds);
}

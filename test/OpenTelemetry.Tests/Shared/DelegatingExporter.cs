// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Tests;

internal sealed class DelegatingExporter<T> : BaseExporter<T>
    where T : class
{
    public Func<Batch<T>, ExportResult> OnExportFunc { get; set; } = (batch) => default;

    public Func<int, bool> OnForceFlushFunc { get; set; } = (timeout) => true;

    public Func<int, bool> OnShutdownFunc { get; set; } = (timeout) => true;

    public override ExportResult Export(in Batch<T> batch) => this.OnExportFunc(batch);

    protected override bool OnForceFlush(int timeoutMilliseconds) => this.OnForceFlushFunc(timeoutMilliseconds);

    protected override bool OnShutdown(int timeoutMilliseconds) => this.OnShutdownFunc(timeoutMilliseconds);
}

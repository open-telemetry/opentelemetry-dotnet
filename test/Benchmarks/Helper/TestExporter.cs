// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Tests;

internal sealed class TestExporter<T> : BaseExporter<T>
    where T : class
{
    private readonly Action<Batch<T>> processBatchAction;

    public TestExporter(Action<Batch<T>> processBatchAction)
    {
        this.processBatchAction = processBatchAction ?? throw new ArgumentNullException(nameof(processBatchAction));
    }

    public override ExportResult Export(in Batch<T> batch)
    {
        this.processBatchAction(batch);

        return ExportResult.Success;
    }
}

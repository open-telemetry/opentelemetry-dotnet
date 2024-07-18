// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Trace;

internal sealed class SimpleMultithreadedActivityExportProcessor : SimpleMultithreadedExportProcessor<Activity>
{
    public SimpleMultithreadedActivityExportProcessor(
        BaseExporter<Activity> exporter)
        : base(exporter)
    {
    }

    /// <inheritdoc />
    protected override void OnExport(Activity data)
    {
        if (!data.Recorded)
        {
            return;
        }

        base.OnExport(data);
    }
}

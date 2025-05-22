// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Tests;

internal sealed class TestActivityExportProcessor : SimpleActivityExportProcessor
{
    public List<Activity> ExportedItems = new();

    public TestActivityExportProcessor(BaseExporter<Activity> exporter)
        : base(exporter)
    {
    }

    protected override void OnExport(Activity data)
    {
        this.ExportedItems.Add(data);
    }
}

// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry;

/// <summary>
/// Implements processor that exports <see cref="Activity"/> objects at each OnEnd call.
/// </summary>
public class SimpleActivityExportProcessor : SimpleExportProcessor<Activity>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SimpleActivityExportProcessor"/> class.
    /// </summary>
    /// <param name="exporter"><inheritdoc cref="SimpleExportProcessor{T}.SimpleExportProcessor" path="/param[@name='exporter']"/>.</param>
    public SimpleActivityExportProcessor(BaseExporter<Activity> exporter)
        : base(exporter)
    {
    }

    /// <inheritdoc />
    public override void OnEnd(Activity data)
    {
        if (!data.Recorded)
        {
            return;
        }

        this.OnExport(data);
    }
}
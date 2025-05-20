// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Internal;

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
        Guard.ThrowIfNull(data);
#pragma warning disable CA1062 // Validate arguments of public methods - needed for netstandard2.1
        if (!data.Recorded)
#pragma warning restore CA1062 // Validate arguments of public methods - needed for netstandard2.1
        {
            return;
        }

        this.OnExport(data);
    }
}

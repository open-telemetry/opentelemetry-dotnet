// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace;

/// <summary>
/// Options for configuring either a <see cref="SimpleActivityExportProcessor"/> or <see cref="BatchActivityExportProcessor"/>.
/// </summary>
public class ActivityExportProcessorOptions
{
    private BatchExportActivityProcessorOptions batchExportProcessorOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityExportProcessorOptions"/> class.
    /// </summary>
    public ActivityExportProcessorOptions()
        : this(new())
    {
    }

    internal ActivityExportProcessorOptions(
        BatchExportActivityProcessorOptions defaultBatchExportActivityProcessorOptions)
    {
        Debug.Assert(defaultBatchExportActivityProcessorOptions != null, "defaultBatchExportActivityProcessorOptions was null");

#pragma warning disable CA1508 // Avoid dead conditional code
        this.batchExportProcessorOptions = defaultBatchExportActivityProcessorOptions ?? new();
#pragma warning restore CA1508 // Avoid dead conditional code
    }

    /// <summary>
    /// Gets or sets the export processor type to be used. The default value is <see cref="ExportProcessorType.Batch"/>.
    /// </summary>
    public ExportProcessorType ExportProcessorType { get; set; } = ExportProcessorType.Batch;

    /// <summary>
    /// Gets or sets the batch export options. Ignored unless <see cref="ExportProcessorType"/> is <see cref="ExportProcessorType.Batch"/>.
    /// </summary>
    public BatchExportActivityProcessorOptions BatchExportProcessorOptions
    {
        get => this.batchExportProcessorOptions;
        set
        {
            Guard.ThrowIfNull(value);
            this.batchExportProcessorOptions = value;
        }
    }
}

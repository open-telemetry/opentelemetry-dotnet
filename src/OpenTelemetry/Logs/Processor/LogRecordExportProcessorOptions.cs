// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;

namespace OpenTelemetry.Logs;

/// <summary>
/// Options for configuring either a <see cref="SimpleLogRecordExportProcessor"/> or <see cref="BatchLogRecordExportProcessor"/>.
/// </summary>
public class LogRecordExportProcessorOptions
{
    private BatchExportLogRecordProcessorOptions batchExportProcessorOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="LogRecordExportProcessorOptions"/> class.
    /// </summary>
    public LogRecordExportProcessorOptions()
        : this(new())
    {
    }

    internal LogRecordExportProcessorOptions(
        BatchExportLogRecordProcessorOptions defaultBatchExportLogRecordProcessorOptions)
    {
        this.batchExportProcessorOptions = defaultBatchExportLogRecordProcessorOptions;
    }

    /// <summary>
    /// Gets or sets the export processor type to be used. The default value is <see cref="ExportProcessorType.Batch"/>.
    /// </summary>
    public ExportProcessorType ExportProcessorType { get; set; } = ExportProcessorType.Batch;

    /// <summary>
    /// Gets or sets the batch export options. Ignored unless <see cref="ExportProcessorType"/> is <see cref="ExportProcessorType.Batch"/>.
    /// </summary>
    public BatchExportLogRecordProcessorOptions BatchExportProcessorOptions
    {
        get => this.batchExportProcessorOptions;
        set
        {
            Guard.ThrowIfNull(value);
            this.batchExportProcessorOptions = value;
        }
    }
}

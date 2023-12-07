// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Logs;

namespace OpenTelemetry;

/// <summary>
/// Implements a simple log record export processor.
/// </summary>
public class SimpleLogRecordExportProcessor : SimpleExportProcessor<LogRecord>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SimpleLogRecordExportProcessor"/> class.
    /// </summary>
    /// <param name="exporter">Log record exporter.</param>
    public SimpleLogRecordExportProcessor(BaseExporter<LogRecord> exporter)
        : base(exporter)
    {
    }
}
// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry;
using OpenTelemetry.Logs;

/// <summary>
/// A custom processor for filtering <see cref="LogRecord"/> instances.
/// </summary>
internal sealed class MyFilteringProcessor : BatchLogRecordExportProcessor
{
    private readonly Func<LogRecord, bool> filter;

    /// <summary>
    /// Initializes a new instance of the <see cref="MyFilteringProcessor"/>
    /// class.
    /// </summary>
    /// <param name="exporter">Log record exporter.</param>
    /// <param name="filter">A predicate used to test if a <see cref="LogRecord"/>
    /// should be exported or dropped. Return <see langword="true"/> to export
    /// or <see langword="false"/> to drop.</param>
    public MyFilteringProcessor(BaseExporter<LogRecord> exporter, Func<LogRecord, bool> filter)
        : base(exporter)
    {
        this.filter = filter ?? throw new ArgumentNullException(nameof(filter));
    }

    public override void OnEnd(LogRecord data)
    {
        // Bypass export if the filter returns false.
        if (this.filter(data))
        {
            base.OnEnd(data);
        }
    }
}

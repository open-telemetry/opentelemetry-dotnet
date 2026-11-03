// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// The settings in a <c>batch</c> span processor section, with the schema's defaults applied.
/// </summary>
/// <param name="ScheduleDelayMilliseconds">The delay between exports, as written. Zero is valid in the schema.</param>
/// <param name="ExportTimeoutMilliseconds">The export timeout. Zero means no limit.</param>
/// <param name="MaxQueueSize">The maximum queue size.</param>
/// <param name="MaxExportBatchSize">The maximum batch size.</param>
internal sealed record BatchSpanProcessorSection(
    int ScheduleDelayMilliseconds,
    int ExportTimeoutMilliseconds,
    int MaxQueueSize,
    int MaxExportBatchSize)
{
    internal const int DefaultScheduleDelayMilliseconds = 5000;
    internal const int DefaultExportTimeoutMilliseconds = 30000;
    internal const int DefaultMaxQueueSize = 2048;
    internal const int DefaultMaxExportBatchSize = 512;
}

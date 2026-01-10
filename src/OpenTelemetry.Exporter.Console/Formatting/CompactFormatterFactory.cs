// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Exporter.Formatting.Compact;
using OpenTelemetry.Exporter.Formatting.Detail;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Exporter.Formatting;

internal sealed class CompactFormatterFactory : IFormatterFactory
{
    public IConsoleFormatter<Activity> GetActivityFormatter(ConsoleExporterOptions options) =>
        new CompactActivityFormatter(options);

    public IConsoleFormatter<LogRecord> GetLogRecordFormatter(ConsoleExporterOptions options) =>
        new CompactLogRecordFormatter(options);

    public IConsoleFormatter<Metric> GetMetricFormatter(ConsoleExporterOptions options) =>
        new DetailMetricFormatter(options);
}

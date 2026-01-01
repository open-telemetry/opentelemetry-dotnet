// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Exporter.Formatting.Simple;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Exporter.Formatting;

internal sealed class SimpleFormatterFactory : IFormatterFactory
{
    public IConsoleFormatter<Activity> GetActivityFormatter(ConsoleExporterOptions options) =>
        new SimpleActivityFormatter(options);

    public IConsoleFormatter<LogRecord> GetLogRecordFormatter(ConsoleExporterOptions options) =>
        new SimpleLogRecordFormatter(options);

    public IConsoleFormatter<Metric> GetMetricFormatter(ConsoleExporterOptions options) =>
        new SimpleMetricFormatter(options);
}

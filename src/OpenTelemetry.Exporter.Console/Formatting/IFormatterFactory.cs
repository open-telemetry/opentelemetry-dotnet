// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Exporter.Formatting;

internal interface IFormatterFactory
{
    IConsoleFormatter<Activity> GetActivityFormatter(ConsoleExporterOptions options);

    IConsoleFormatter<LogRecord> GetLogRecordFormatter(ConsoleExporterOptions options);

    IConsoleFormatter<Metric> GetMetricFormatter(ConsoleExporterOptions options);
}

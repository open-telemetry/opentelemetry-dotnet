// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Exporter.Formatting.Detail;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Exporter.Formatting;

internal sealed class DetailFormatterFactory : IFormatterFactory
{
    public IConsoleFormatter<Activity> GetActivityFormatter(ConsoleExporterOptions options) =>
        new DetailActivityFormatter(options);

    public IConsoleFormatter<LogRecord> GetLogRecordFormatter(ConsoleExporterOptions options) =>
        new DetailLogRecordFormatter(options);

    public IConsoleFormatter<Metric> GetMetricFormatter(ConsoleExporterOptions options) =>
        new DetailMetricFormatter(options);
}

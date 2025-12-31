// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Exporter.Formatting.KeyValue;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Exporter.Formatting;

internal sealed class KeyValueFormatterFactory : IFormatterFactory
{
    public IConsoleFormatter<Activity> GetActivityFormatter(ConsoleExporterOptions options) =>
        new KeyValueActivityFormatter(options);

    public IConsoleFormatter<LogRecord> GetLogRecordFormatter(ConsoleExporterOptions options) =>
        new KeyValueLogRecordFormatter(options);

    public IConsoleFormatter<Metric> GetMetricFormatter(ConsoleExporterOptions options) =>
        new KeyValueMetricFormatter(options);
}

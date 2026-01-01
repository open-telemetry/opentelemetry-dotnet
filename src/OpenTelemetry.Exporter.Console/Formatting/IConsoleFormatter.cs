// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter.Formatting;

/// <summary>
/// Internal formatter interface for console exporters.
/// </summary>
/// <typeparam name="T">The telemetry type.</typeparam>
internal interface IConsoleFormatter<T> : IDisposable
    where T : class
{
    ExportResult Export(in Batch<T> batch, ConsoleFormatterContext context);
}

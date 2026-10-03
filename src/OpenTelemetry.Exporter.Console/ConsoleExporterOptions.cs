// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter;

public class ConsoleExporterOptions
{
    /// <summary>
    /// Gets or sets the output targets for the console exporter.
    /// </summary>
    public ConsoleExporterOutputTargets Targets { get; set; } = ConsoleExporterOutputTargets.Console;

    /// <summary>
    /// Gets or sets a value indicating whether spans that are unsampled but recording
    /// should be exported. The default value is <see langword="false"/>.
    /// </summary>
    public bool ExportUnsampledSpans { get; set; }
}

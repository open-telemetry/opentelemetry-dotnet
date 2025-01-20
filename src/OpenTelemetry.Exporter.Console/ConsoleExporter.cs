// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter;

public abstract class ConsoleExporter<T> : BaseExporter<T>
    where T : class
{
    private readonly ConsoleExporterOptions options;

    protected ConsoleExporter(ConsoleExporterOptions options)
    {
        this.options = options;

        this.TagWriter = new ConsoleTagWriter(this.OnUnsupportedTagDropped);
    }

    internal ConsoleTagWriter TagWriter { get; }

    protected void WriteLine(string message)
    {
        if (this.options.Targets.HasFlag(ConsoleExporterOutputTargets.Console))
        {
            Console.WriteLine(message);
        }

        if (this.options.Targets.HasFlag(ConsoleExporterOutputTargets.Debug))
        {
            System.Diagnostics.Trace.WriteLine(message);
        }
    }

    private void OnUnsupportedTagDropped(
        string tagKey,
        string tagValueTypeFullName)
    {
        this.WriteLine($"Unsupported attribute value type '{tagValueTypeFullName}' for '{tagKey}'.");
    }
}

using System;
using System.Diagnostics;

namespace OpenTelemetry.Trace;

internal interface ITraceExporterDetector
{
    string Name { get; }

    BaseProcessor<Activity>? Create(IServiceProvider serviceProvider);
}

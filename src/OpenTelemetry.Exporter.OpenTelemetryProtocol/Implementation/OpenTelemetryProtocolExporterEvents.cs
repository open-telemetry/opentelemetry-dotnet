// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;

/// <summary>
/// Methods for logging internal events in OpenTelemetryProtocolExporter.
/// </summary>
internal static partial class OpenTelemetryProtocolExporterEvents
{
    // TODO: there's no concept of Event vs NonEvent in ILogger (as opposed to EventSource.)
    // Need to find a way to log "NonEvent" in ILogger to avoid the below situation:
    // When the Exception occurred in ExportMethod, the Export event got written to the pipe, and thus the Exporter keeps exporting.
    public static partial void ExportMethodException(this ILogger logger, string message);
}

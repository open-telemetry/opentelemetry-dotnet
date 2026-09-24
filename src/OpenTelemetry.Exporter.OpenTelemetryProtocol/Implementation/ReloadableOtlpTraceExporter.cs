// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Transmission;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;

internal sealed class ReloadableOtlpTraceExporter : OtlpTraceExporter
{
    private readonly ReloadableExportClient client;

    internal ReloadableOtlpTraceExporter(OtlpExporterOptions options, SdkLimitOptions sdkLimitOptions, ExperimentalOptions experimentalOptions, OtlpExporterTransmissionHandler transmissionHandler, ReloadableExportClient client)
        : base(options, sdkLimitOptions, experimentalOptions, transmissionHandler)
    {
        this.client = client;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            this.client.Dispose();
        }

        base.Dispose(disposing);
    }
}

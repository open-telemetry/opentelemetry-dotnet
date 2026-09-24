// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Transmission;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;

internal sealed class ReloadableOtlpMetricExporter : OtlpMetricExporter
{
    private readonly ReloadableExportClient client;

    internal ReloadableOtlpMetricExporter(OtlpExporterOptions options, ExperimentalOptions experimentalOptions, OtlpExporterTransmissionHandler transmissionHandler, ReloadableExportClient client)
        : base(options, experimentalOptions, transmissionHandler)
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

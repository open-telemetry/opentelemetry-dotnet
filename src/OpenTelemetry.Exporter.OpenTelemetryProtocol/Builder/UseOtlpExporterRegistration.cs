// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter;

// Note: This class is added to the IServiceCollection when UseOtlpExporter is
// called. Its purpose is to detect registrations so that subsequent calls and
// calls to signal-specific AddOtlpExporter can throw.
internal sealed class UseOtlpExporterRegistration
{
    public static readonly UseOtlpExporterRegistration Instance = new();

    private UseOtlpExporterRegistration()
    {
        // Note: Some dependency injection containers (ex: Unity, Grace) will
        // automatically create services if they have a public constructor even
        // if the service was never registered into the IServiceCollection. The
        // behavior of UseOtlpExporterRegistration requires that it should only
        // exist if registered. This private constructor is intended to prevent
        // automatic instantiation.
    }
}

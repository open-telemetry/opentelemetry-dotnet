// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

namespace OpenTelemetry.Exporter;

// Note: This class is added to the IServiceCollection when UseOtlpExporter is
// called. Its purpose is to detect registrations so that subsequent calls and
// calls to signal-specific AddOtlpExporter can throw.
internal sealed class UseOtlpExporterRegistration
{
}

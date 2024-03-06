// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Exporter;

namespace System;

internal static class OpenTelemetryBuilderServiceProviderExtensions
{
    public static void EnsureSingleUseOtlpExporterRegistration(this IServiceProvider serviceProvider)
    {
        var registrations = serviceProvider.GetServices<UseOtlpExporterRegistration>();
        if (registrations.Count() > 1)
        {
            throw new NotSupportedException("Multiple calls to UseOtlpExporter on the same IServiceCollection are not supported.");
        }
    }

    public static void EnsureNoUseOtlpExporterRegistrations(this IServiceProvider serviceProvider)
    {
        var registrations = serviceProvider.GetServices<UseOtlpExporterRegistration>();
        if (registrations.Any())
        {
            throw new NotSupportedException("Signal-specific AddOtlpExporter methods and the cross-cutting UseOtlpExporter method being invoked on the same IServiceCollection is not supported.");
        }
    }
}

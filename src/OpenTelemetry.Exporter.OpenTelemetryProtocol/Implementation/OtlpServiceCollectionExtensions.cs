// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter;

internal static class OtlpServiceCollectionExtensions
{
    public static void AddOtlpExporterSharedServices(this IServiceCollection services)
    {
        Debug.Assert(services != null, "services was null");

        services.RegisterOptionsFactory(OtlpExporterOptions.CreateOtlpExporterOptions);
        services.RegisterOptionsFactory(configuration => new SdkLimitOptions(configuration));
        services.RegisterOptionsFactory(configuration => new ExperimentalOptions(configuration));
    }
}

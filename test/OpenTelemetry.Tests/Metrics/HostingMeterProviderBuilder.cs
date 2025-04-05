// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if BUILDING_HOSTING_TESTS

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics;

namespace OpenTelemetry.Metrics.Tests;

#pragma warning disable CA1515 // Consider making public types internal
public sealed class HostingMeterProviderBuilder : MeterProviderBuilderBase
#pragma warning restore CA1515 // Consider making public types internal
{
    public HostingMeterProviderBuilder(IServiceCollection services)
        : base(services)
    {
    }

    public override MeterProviderBuilder AddMeter(params string[] names)
    {
        return this.ConfigureServices(services =>
        {
            foreach (var name in names)
            {
                // Note: The entire purpose of this class is to use the
                // IMetricsBuilder API to enable Metrics and NOT the
                // traditional AddMeter API.
                services.AddMetrics(builder => builder.EnableMetrics(name));
            }
        });
    }

    public MeterProviderBuilder AddSdkMeter(params string[] names)
    {
        return base.AddMeter(names);
    }
}
#endif

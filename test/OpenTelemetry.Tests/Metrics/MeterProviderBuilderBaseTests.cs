// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;
using static OpenTelemetry.OpenTelemetrySdk;

namespace OpenTelemetry.Metrics.Tests;

public sealed class MeterProviderBuilderBaseTests : IDisposable
{
    public MeterProviderBuilderBaseTests()
    {
        Environment.SetEnvironmentVariable(SdkConfigDefinitions.SdkDisableEnvVarName, null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(SdkConfigDefinitions.SdkDisableEnvVarName, null);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void ReturnNoopMeterProviderWhenSdkDisabledEnvVarSet()
    {
        Environment.SetEnvironmentVariable(SdkConfigDefinitions.SdkDisableEnvVarName, "true");
        var builder = new MeterProviderBuilderBase();

        using var provider = builder.Build();

        Assert.IsType<NoopMeterProvider>(provider);
    }

    [Fact]
    public void ReturnMeterProviderSdkWhenSdkDisabledEnvVarNotSet()
    {
        Environment.SetEnvironmentVariable(SdkConfigDefinitions.SdkDisableEnvVarName, null);
        var builder = new MeterProviderBuilderBase();

        using var provider = builder.Build();

        Assert.IsType<MeterProviderSdk>(provider);
    }
}

// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;
using static OpenTelemetry.OpenTelemetrySdk;

namespace OpenTelemetry.Logs.Tests;

public sealed class LoggerProviderBuilderBaseTests : IDisposable
{
    public LoggerProviderBuilderBaseTests()
    {
        Environment.SetEnvironmentVariable(SdkConfigDefinitions.SdkDisableEnvVarName, null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(SdkConfigDefinitions.SdkDisableEnvVarName, null);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void ReturnNoopLoggerProviderWhenSdkDisabledEnvVarSet()
    {
        Environment.SetEnvironmentVariable(SdkConfigDefinitions.SdkDisableEnvVarName, "true");
        var builder = new LoggerProviderBuilderBase();

        using var provider = builder.Build();

        Assert.IsType<NoopLoggerProvider>(provider);
    }

    [Fact]
    public void ReturnLoggerProviderSdkWhenSdkDisabledEnvVarNotSet()
    {
        Environment.SetEnvironmentVariable(SdkConfigDefinitions.SdkDisableEnvVarName, null);
        var builder = new LoggerProviderBuilderBase();

        using var provider = builder.Build();

        Assert.IsType<LoggerProviderSdk>(provider);
    }
}

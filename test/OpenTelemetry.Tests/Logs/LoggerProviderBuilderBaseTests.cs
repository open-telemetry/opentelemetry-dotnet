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

    [Theory]
    [InlineData("true", typeof(NoopLoggerProvider))]
    [InlineData("false", typeof(LoggerProviderSdk))]
    [InlineData(null, typeof(LoggerProviderSdk))]
    public void LoggerProviderIsExpectedType(string? value, Type expected)
    {
        Environment.SetEnvironmentVariable(SdkConfigDefinitions.SdkDisableEnvVarName, value);
        var builder = new LoggerProviderBuilderBase();

        using var provider = builder.Build();

        Assert.IsType(expected, provider);
    }
}

// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;
using static OpenTelemetry.OpenTelemetrySdk;

namespace OpenTelemetry.Logs.Tests;

public sealed class LoggerProviderBuilderBaseTests
{
    [Theory]
    [InlineData("true", typeof(NoopLoggerProvider))]
    [InlineData("false", typeof(LoggerProviderSdk))]
    [InlineData(null, typeof(LoggerProviderSdk))]
    public void LoggerProviderIsExpectedType(string? value, Type expected)
    {
        using (new EnvironmentVariableScope("OTEL_SDK_DISABLED", value))
        {
            Environment.SetEnvironmentVariable(SdkConfigDefinitions.SdkDisableEnvVarName, value);
            var builder = new LoggerProviderBuilderBase();

            using var provider = builder.Build();

            Assert.IsType(expected, provider);
        }
    }
}

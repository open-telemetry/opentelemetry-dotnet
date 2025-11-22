// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;
using static OpenTelemetry.OpenTelemetrySdk;

namespace OpenTelemetry.Metrics.Tests;

public sealed class MeterProviderBuilderBaseTests
{
    [Theory]
    [InlineData("true", typeof(NoopMeterProvider))]
    [InlineData("false", typeof(MeterProviderSdk))]
    [InlineData(null, typeof(MeterProviderSdk))]
    public void LoggerProviderIsExpectedType(string? value, Type expected)
    {
        using (new EnvironmentVariableScope("OTEL_SDK_DISABLED", value))
        {
            var builder = new MeterProviderBuilderBase();

            using var provider = builder.Build();

            Assert.IsType(expected, provider);
        }
    }
}

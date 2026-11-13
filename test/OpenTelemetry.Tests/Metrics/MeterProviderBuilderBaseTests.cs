// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Tests;
using static OpenTelemetry.OpenTelemetrySdk;

namespace OpenTelemetry.Metrics.Tests;

[Collection(EnvVarsCollectionDefinition.Name)]
public sealed class MeterProviderBuilderBaseTests
{
    [Theory]
    [InlineData("true", typeof(NoopMeterProvider))]
    [InlineData("false", typeof(MeterProviderSdk))]
    [InlineData(null, typeof(MeterProviderSdk))]
    public void LoggerProviderIsExpectedType(string? value, Type expected)
    {
        using (EnvironmentVariableScope.Create("OTEL_SDK_DISABLED", value))
        {
            var builder = new MeterProviderBuilderBase();

            using var provider = builder.Build();

            Assert.IsType(expected, provider);
        }
    }
}

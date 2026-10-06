// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Configuration.Declarative.Tests;

public sealed class StrictModeKeyScopeTests
{
    [Theory]
    [InlineData("OTEL_X", true)]
    [InlineData("otel_x", true)]
    [InlineData("OTEL_X:Y", true)]
    [InlineData("OTEL_CONFIG_FILE", false)]
    [InlineData("otel_config_file", false)]
    [InlineData("OTEL_CONFIG_FILE:X", false)]
    [InlineData("OTEL_CONFIG_FILE_EXTRA", true)]
    [InlineData("OTEL_DOTNET_X", false)]
    [InlineData("otel_dotnet_x", false)]
    [InlineData("OTEL_DOTNET_X:Y", false)]
    [InlineData("OTEL_", true)]
    [InlineData("", false)]
    [InlineData("OTELX", false)]
    [InlineData("OpenTelemetry:Console", true)]
    [InlineData("OpenTelemetry:Console:Targets", true)]
    [InlineData("opentelemetry:console:targets", true)]
    [InlineData("OpenTelemetry", false)]
    [InlineData("OpenTelemetry:X", false)]
    [InlineData("OpenTelemetry:ServiceName", false)]
    [InlineData("OpenTelemetry:Enabled", false)]
    [InlineData("OpenTelemetry:Tracing:SamplingRatio", false)]
    [InlineData("OpenTelemetry:Otlp:Endpoint", false)]
    [InlineData("OpenTelemetry:ConsoleFoo", false)]
    [InlineData("OpenTelemetry:ConsoleFoo:Targets", false)]
    [InlineData("OpenTelemetryFoo:X", false)]
    [InlineData("X_OTEL_Y", false)]
    public void IsInScope_ClassifiesKeyByFirstSegment(string key, bool expected)
        => Assert.Equal(expected, StrictModeKeyScope.IsInScope(key));
}

// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Internal;
using OpenTelemetry.SelfDiagnostics;

namespace OpenTelemetry.Tests.Diagnostics;

public class SelfDiagnosticsOptionsEnvironmentDefaultsTests
{
    [Theory]
    [InlineData("OTEL_LOG_LEVEL", "debug", true)]
    [InlineData("OTEL_LOG_LEVEL", "verbose", false)]
    [InlineData("OTEL_LOG_LEVEL", " ", false)]
    [InlineData("OTEL_DOTNET_SELF_DIAGNOSTICS_ENV_VARS", "all", true)]
    [InlineData("OTEL_DOTNET_SELF_DIAGNOSTICS_ENV_VARS", "some", false)]
    [InlineData("OTEL_DOTNET_SELF_DIAGNOSTICS_LOG_DIRECTORY", "/logs", true)]
    [InlineData("OTEL_DOTNET_SELF_DIAGNOSTICS_LOG_DIRECTORY", "", false)]
    [InlineData("OTEL_DOTNET_SELF_DIAGNOSTICS_SINKS", "stdout,syslog", true)]
    [InlineData("OTEL_DOTNET_SELF_DIAGNOSTICS_SINKS", "none", true)]
    [InlineData("OTEL_DOTNET_SELF_DIAGNOSTICS_SINKS", "file", true)]
    [InlineData("OTEL_DOTNET_SELF_DIAGNOSTICS_SINKS", "syslog", false)]
    [InlineData("OTEL_DOTNET_SELF_DIAGNOSTICS_SINKS", ",,", false)]
    public void ApplyEnvironmentVariables_RecordsOnlyTheVariablesItApplied(string name, string value, bool applied)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection([new(name, value)]).Build();
        var appliedVariables = new HashSet<string>();

        SelfDiagnosticsOptionsEnvironmentDefaults.ApplyEnvironmentVariables(
            new SelfDiagnosticsOptions(),
            configuration,
            static () => "/fallback",
            static _ => { },
            appliedVariables: appliedVariables);

        string[] expected = applied ? [name] : [];
        Assert.Equal(expected, appliedVariables);
    }

    [Theory]

    // OTel spec tokens.
    [InlineData("error", true, LogLevel.Error)]
    [InlineData("ERROR", true, LogLevel.Error)]
    [InlineData("warn", true, LogLevel.Warning)]
    [InlineData("info", true, LogLevel.Information)]
    [InlineData("debug", true, LogLevel.Debug)]
    [InlineData("trace", true, LogLevel.Trace)]
    [InlineData("none", true, LogLevel.None)]

    // The .NET LogLevel member names for Warning and Information are accepted as aliases.
    [InlineData("warning", true, LogLevel.Warning)]
    [InlineData("Information", true, LogLevel.Information)]

    // Unrecognised values, including those with no OTel equivalent, are rejected.
    [InlineData("critical", false, LogLevel.None)]
    [InlineData("verbose", false, LogLevel.None)]
    [InlineData("", false, LogLevel.None)]
    [InlineData("0", false, LogLevel.None)]
    [InlineData("+1", false, LogLevel.None)]
    [InlineData("-1", false, LogLevel.None)]
    [InlineData("some-other-value", false, LogLevel.None)]
    public void TryParseOtelLogLevel_Matrix(string value, bool expectedResult, LogLevel expectedLevel)
    {
        var result = SelfDiagnosticsOptionsEnvironmentDefaults.TryParseOtelLogLevel(value, out var level);

        Assert.Equal(expectedResult, result);
        Assert.Equal(expectedLevel, level);
    }
}

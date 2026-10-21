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
    [InlineData(SelfDiagnosticsOptions.LogLevelEnvVarName, "debug", true)]
    [InlineData(SelfDiagnosticsOptions.LogLevelEnvVarName, "verbose", false)]
    [InlineData(SelfDiagnosticsOptions.LogLevelEnvVarName, " ", false)]
    [InlineData(SelfDiagnosticsOptions.EnvironmentVariablesEnvVarName, "all", true)]
    [InlineData(SelfDiagnosticsOptions.EnvironmentVariablesEnvVarName, "some", false)]
    [InlineData(SelfDiagnosticsOptions.LogDirectoryEnvVarName, "/logs", true)]
    [InlineData(SelfDiagnosticsOptions.LogDirectoryEnvVarName, "", false)]
    [InlineData(SelfDiagnosticsOptions.SinksEnvVarName, "stdout,syslog", true)]
    [InlineData(SelfDiagnosticsOptions.SinksEnvVarName, "none", true)]
    [InlineData(SelfDiagnosticsOptions.SinksEnvVarName, "file", true)]
    [InlineData(SelfDiagnosticsOptions.SinksEnvVarName, "syslog", false)]
    [InlineData(SelfDiagnosticsOptions.SinksEnvVarName, ",,", false)]
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

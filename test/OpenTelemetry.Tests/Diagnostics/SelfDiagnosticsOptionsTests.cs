// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OpenTelemetry.Tests.Diagnostics;

public class SelfDiagnosticsOptionsTests
{
    [Theory]
    [InlineData("error", true, LogLevel.Error)]
    [InlineData("ERROR", true, LogLevel.Error)]
    [InlineData("warn", true, LogLevel.Warning)]
    [InlineData("info", true, LogLevel.Information)]
    [InlineData("debug", true, LogLevel.Debug)]
    [InlineData("trace", true, LogLevel.Trace)]
    [InlineData("none", true, LogLevel.None)]

    // The LogLevel member names are accepted as aliases for the spec tokens.
    [InlineData("warning", true, LogLevel.Warning)]
    [InlineData("Information", true, LogLevel.Information)]
    [InlineData("critical", true, LogLevel.Critical)]
    [InlineData("verbose", false, LogLevel.None)]
    [InlineData("", false, LogLevel.None)]

    // Numeric input is rejected rather than resolving through the enum's underlying values.
    [InlineData("0", false, LogLevel.None)]
    [InlineData("+1", false, LogLevel.None)]
    [InlineData("-1", false, LogLevel.None)]
    public void TryParseOtelLogLevel_Matrix(string value, bool expectedResult, LogLevel expectedLevel)
    {
        var result = SelfDiagnosticsOptions.TryParseOtelLogLevel(value, out var level);

        Assert.Equal(expectedResult, result);
        Assert.Equal(expectedLevel, level);
    }

    [Fact]
    public void ConfigurationCoordinator_NewestOwnerWins_AndDisposalRestoresLatestPreviousValue()
    {
        var applied = new List<SelfDiagnosticsOptions.SelfDiagnosticsConfiguration>();
        using var coordinator = new SelfDiagnosticsOptions.SelfDiagnosticsConfigurationCoordinator(applied.Add);
        var firstMonitor = new TestOptionsMonitor(new SelfDiagnosticsOptions { LogToStdout = true });
        var secondMonitor = new TestOptionsMonitor(new SelfDiagnosticsOptions { LogToStderr = true });

        using var firstRegistration = coordinator.Register(firstMonitor);
        Assert.True(Assert.Single(applied).LogToStdout);

        using (coordinator.Register(secondMonitor))
        {
            Assert.True(applied[applied.Count - 1].LogToStderr);

            firstMonitor.Set(new SelfDiagnosticsOptions { LogDirectory = "latest-first" });
            Assert.Equal(2, applied.Count);
        }

        Assert.Equal("latest-first", applied[applied.Count - 1].LogDirectory);
        firstRegistration.Dispose();
        Assert.Same(SelfDiagnosticsOptions.SelfDiagnosticsConfiguration.Disabled, applied[applied.Count - 1]);
    }

    [Fact]
    public void ConfigurationSnapshot_IsUnaffectedBySubsequentOptionsMutation()
    {
        var options = new SelfDiagnosticsOptions
        {
            LogToStdout = true,
            MinimumLevel = LogLevel.Warning,
        };
        var configuration = SelfDiagnosticsOptions.SelfDiagnosticsConfiguration.Create(options);

        options.LogToStdout = false;
        options.LogToStderr = true;
        options.MinimumLevel = LogLevel.Trace;

        Assert.True(configuration.LogToStdout);
        Assert.False(configuration.LogToStderr);
        Assert.Equal(LogLevel.Warning, configuration.MinimumLevel);
    }

    [Fact]
    public void NoSinkConfigured_ResolvesToNoLevel()
    {
        var configuration = SelfDiagnosticsOptions.SelfDiagnosticsConfiguration.Create(
            new SelfDiagnosticsOptions { MinimumLevel = LogLevel.Debug });

        Assert.False(configuration.HasConfiguredSink);
        Assert.Equal(LogLevel.None, configuration.EffectiveLevel);
    }

    [Fact]
    public void Defaults_AreSilentWithWarningLevel()
    {
        var options = new SelfDiagnosticsOptions();

        Assert.Equal(LogLevel.Warning, options.MinimumLevel);
        Assert.Null(options.LogDirectory);
        Assert.False(options.LogToStdout);
        Assert.False(options.LogToStderr);
        Assert.Equal(10_240, options.FileSizeLimitKilobytes);
        Assert.Equal(0, options.MaxRetainedFiles);
    }

    [Theory]
    [InlineData("error", LogLevel.Error)]
    [InlineData("warn", LogLevel.Warning)]
    [InlineData("info", LogLevel.Information)]
    [InlineData("debug", LogLevel.Debug)]
    [InlineData("trace", LogLevel.Trace)]
    [InlineData("none", LogLevel.None)]
    [InlineData("  DEBUG  ", LogLevel.Debug)]
    public void OtelLogLevel_SpecTokens_ApplyThroughTheConstructor(string value, LogLevel expected)
    {
        var options = CreateOptions(SelfDiagnosticsOptions.LogLevelEnvVarName, value);

        Assert.Equal(expected, options.MinimumLevel);
        Assert.Empty(options.ConfigurationWarnings);
    }

    [Theory]
    [InlineData("Warning", LogLevel.Warning)]
    [InlineData("Information", LogLevel.Information)]
    [InlineData("Critical", LogLevel.Critical)]
    [InlineData("critical", LogLevel.Critical)]
    public void OtelLogLevel_LogLevelEnumNames_AreAcceptedAsAliases(string value, LogLevel expected)
    {
        var options = CreateOptions(SelfDiagnosticsOptions.LogLevelEnvVarName, value);

        Assert.Equal(expected, options.MinimumLevel);
        Assert.Empty(options.ConfigurationWarnings);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("2")]
    [InlineData("+1")]
    [InlineData("-1")]
    public void OtelLogLevel_NumericInput_IsRejected(string value)
    {
        var options = CreateOptions(SelfDiagnosticsOptions.LogLevelEnvVarName, value);

        Assert.Equal(LogLevel.Warning, options.MinimumLevel);
        Assert.Contains(
            SelfDiagnosticsOptions.LogLevelEnvVarName,
            Assert.Single(options.ConfigurationWarnings),
            StringComparison.Ordinal);
    }

    [Fact]
    public void OtelLogLevel_Unparsable_KeepsTheDefaultAndRecordsAWarning()
    {
        var options = CreateOptions(SelfDiagnosticsOptions.LogLevelEnvVarName, "verbose");

        Assert.Equal(LogLevel.Warning, options.MinimumLevel);
        var warning = Assert.Single(options.ConfigurationWarnings);
        Assert.Contains(SelfDiagnosticsOptions.LogLevelEnvVarName, warning, StringComparison.Ordinal);
        Assert.Contains("verbose", warning, StringComparison.Ordinal);
    }

    [Fact]
    public void OtelLogDirectory_SetsLogDirectory()
    {
        var options = CreateOptions(SelfDiagnosticsOptions.LogDirectoryEnvVarName, "/var/log/otel");

        Assert.Equal("/var/log/otel", options.LogDirectory);
        Assert.Empty(options.ConfigurationWarnings);
    }

    [Fact]
    public void Sinks_None_ForcesMinimumLevelToNone()
    {
        // 'none' must silence diagnostics even when OTEL_LOG_LEVEL asked for verbose output.
        var options = CreateOptions(new Dictionary<string, string?>
        {
            [SelfDiagnosticsOptions.LogLevelEnvVarName] = "debug",
            [SelfDiagnosticsOptions.SinksEnvVarName] = "none",
        });

        Assert.Equal(LogLevel.None, options.MinimumLevel);
        Assert.Empty(options.ConfigurationWarnings);
    }

    [Fact]
    public void Sinks_None_OverridesEveryOtherToken()
    {
        var options = CreateOptions(new Dictionary<string, string?>
        {
            [SelfDiagnosticsOptions.LogDirectoryEnvVarName] = "/var/log/otel",
            [SelfDiagnosticsOptions.SinksEnvVarName] = "stdout,none,file",
        });

        Assert.Equal(LogLevel.None, options.MinimumLevel);
        Assert.Null(options.LogDirectory);
        Assert.False(options.LogToStdout);
        Assert.False(options.LogToStderr);
        Assert.False(SelfDiagnosticsOptions.SelfDiagnosticsConfiguration.Create(options).HasConfiguredSink);
    }

    [Theory]
    [InlineData("stdout", true, false)]
    [InlineData("stderr", false, true)]
    [InlineData("stdout,stderr", true, true)]
    [InlineData("console", true, true)]
    [InlineData(" STDOUT , stderr ", true, true)]
    [InlineData("stdout,,stderr,", true, true)]
    public void Sinks_SelectStreamsIndividually(string value, bool expectStdout, bool expectStderr)
    {
        var options = CreateOptions(SelfDiagnosticsOptions.SinksEnvVarName, value);

        Assert.Equal(expectStdout, options.LogToStdout);
        Assert.Equal(expectStderr, options.LogToStderr);
        Assert.Null(options.LogDirectory);
        Assert.Empty(options.ConfigurationWarnings);
    }

    [Fact]
    public void Sinks_FileAndAStream_EnableBoth()
    {
        var options = CreateOptions(new Dictionary<string, string?>
        {
            [SelfDiagnosticsOptions.LogDirectoryEnvVarName] = "/var/log/otel",
            [SelfDiagnosticsOptions.SinksEnvVarName] = "file,stderr",
        });

        Assert.Equal("/var/log/otel", options.LogDirectory);
        Assert.False(options.LogToStdout);
        Assert.True(options.LogToStderr);
        Assert.Empty(options.ConfigurationWarnings);
    }

    [Fact]
    public void Sinks_WithoutFile_DropsTheLogDirectory()
    {
        var options = CreateOptions(new Dictionary<string, string?>
        {
            [SelfDiagnosticsOptions.LogDirectoryEnvVarName] = "/var/log/otel",
            [SelfDiagnosticsOptions.SinksEnvVarName] = "stdout",
        });

        Assert.Null(options.LogDirectory);
        Assert.True(options.LogToStdout);
    }

    [Fact]
    public void Sinks_FileWithoutADirectory_WarnsAndEnablesNothing()
    {
        var options = CreateOptions(SelfDiagnosticsOptions.SinksEnvVarName, "file");

        Assert.Null(options.LogDirectory);
        Assert.False(options.LogToStdout);
        Assert.False(options.LogToStderr);

        var warning = Assert.Single(options.ConfigurationWarnings);
        Assert.Contains(SelfDiagnosticsOptions.LogDirectoryEnvVarName, warning, StringComparison.Ordinal);
    }

    [Fact]
    public void Sinks_UnrecognisedToken_WarnsButKeepsTheValidOnes()
    {
        var options = CreateOptions(SelfDiagnosticsOptions.SinksEnvVarName, "stdout,syslog");

        Assert.True(options.LogToStdout);

        var warning = Assert.Single(options.ConfigurationWarnings);
        Assert.Contains(SelfDiagnosticsOptions.SinksEnvVarName, warning, StringComparison.Ordinal);
        Assert.Contains("syslog", warning, StringComparison.Ordinal);
    }

    [Fact]
    public void Sinks_Absent_StillEnablesTheFileSinkFromTheLogDirectory()
    {
        var options = CreateOptions(new Dictionary<string, string?>
        {
            [SelfDiagnosticsOptions.LogDirectoryEnvVarName] = "/var/log/otel",
            [SelfDiagnosticsOptions.LogLevelEnvVarName] = "debug",
        });

        var configuration = SelfDiagnosticsOptions.SelfDiagnosticsConfiguration.Create(options);

        Assert.Equal("/var/log/otel", options.LogDirectory);
        Assert.True(configuration.HasConfiguredSink);
        Assert.Equal(LogLevel.Debug, configuration.EffectiveLevel);
    }

    [Fact]
    public void AgentEnvironmentVariables_AreNotRead()
    {
        var options = CreateOptions(new Dictionary<string, string?>
        {
            ["OTEL_DOTNET_AUTO_HOME"] = "/opt/otel-dotnet-auto",
            ["OTEL_DOTNET_AUTO_LOG_DIRECTORY"] = "/var/log/agent",
            ["OTEL_DOTNET_AUTO_LOGGER"] = "console",
        });

        Assert.Null(options.LogDirectory);
        Assert.False(options.LogToStdout);
        Assert.False(options.LogToStderr);
        Assert.Empty(options.ConfigurationWarnings);
        Assert.False(SelfDiagnosticsOptions.SelfDiagnosticsConfiguration.Create(options).HasConfiguredSink);
    }

    [Theory]

    // The documented tokens.
    [InlineData("none", EnvironmentVariableLogMode.None)]
    [InlineData("names", EnvironmentVariableLogMode.Names)]
    [InlineData("knownsafe", EnvironmentVariableLogMode.KnownSafeValues)]
    [InlineData("all", EnvironmentVariableLogMode.AllValues)]

    // The enum member names, so a value copied out of code works unchanged.
    [InlineData("knownsafevalues", EnvironmentVariableLogMode.KnownSafeValues)]
    [InlineData("allvalues", EnvironmentVariableLogMode.AllValues)]

    // Matching is case-insensitive.
    [InlineData("ALL", EnvironmentVariableLogMode.AllValues)]
    [InlineData("KnownSafe", EnvironmentVariableLogMode.KnownSafeValues)]
    public void SelfDiagnosticsEnvVars_TokensAndEnumAliases_ApplyThroughTheConstructor(
        string value,
        EnvironmentVariableLogMode expected)
    {
        var options = CreateOptions(SelfDiagnosticsOptions.EnvironmentVariablesEnvVarName, value);

        Assert.Equal(expected, options.EnvironmentVariables);
        Assert.Empty(options.ConfigurationWarnings);
    }

    [Fact]
    public void SelfDiagnosticsEnvVars_Unrecognised_KeepsKnownSafeValuesAndRecordsAWarning()
    {
        var options = CreateOptions(SelfDiagnosticsOptions.EnvironmentVariablesEnvVarName, "everything");

        Assert.Equal(EnvironmentVariableLogMode.KnownSafeValues, options.EnvironmentVariables);
        var warning = Assert.Single(options.ConfigurationWarnings);
        Assert.Contains(SelfDiagnosticsOptions.EnvironmentVariablesEnvVarName, warning, StringComparison.Ordinal);
        Assert.Contains("everything", warning, StringComparison.Ordinal);
    }

    [Fact]
    public void ConfigurationSnapshot_CarriesEnvironmentVariableModeAndWarnings()
    {
        var options = CreateOptions(new Dictionary<string, string?>
        {
            [SelfDiagnosticsOptions.EnvironmentVariablesEnvVarName] = "all",
            [SelfDiagnosticsOptions.LogLevelEnvVarName] = "loud",
            [SelfDiagnosticsOptions.LogDirectoryEnvVarName] = "/var/log/otel",
        });

        var configuration = SelfDiagnosticsOptions.SelfDiagnosticsConfiguration.Create(options);

        Assert.Equal(EnvironmentVariableLogMode.AllValues, configuration.EnvironmentVariables);
        Assert.Equal(options.ConfigurationWarnings, configuration.ConfigurationWarnings);
        Assert.Contains(
            SelfDiagnosticsOptions.LogLevelEnvVarName,
            Assert.Single(configuration.ConfigurationWarnings),
            StringComparison.Ordinal);
    }

    private static SelfDiagnosticsOptions CreateOptions(string name, string? value) =>
        CreateOptions(new Dictionary<string, string?> { [name] = value });

    private static SelfDiagnosticsOptions CreateOptions(Dictionary<string, string?> settings) =>
        new(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());

    private sealed class TestOptionsMonitor : IOptionsMonitor<SelfDiagnosticsOptions>
    {
        private Action<SelfDiagnosticsOptions, string?>? listener;

        public TestOptionsMonitor(SelfDiagnosticsOptions currentValue)
        {
            this.CurrentValue = currentValue;
        }

        public SelfDiagnosticsOptions CurrentValue { get; private set; }

        public SelfDiagnosticsOptions Get(string? name) => this.CurrentValue;

        public IDisposable OnChange(Action<SelfDiagnosticsOptions, string?> listener)
        {
            this.listener += listener;
            return new CallbackRegistration(() => this.listener -= listener);
        }

        public void Set(SelfDiagnosticsOptions value)
        {
            this.CurrentValue = value;
            this.listener?.Invoke(value, null);
        }

        private sealed class CallbackRegistration : IDisposable
        {
            private readonly Action unregister;
            private bool disposed;

            public CallbackRegistration(Action unregister)
            {
                this.unregister = unregister;
            }

            public void Dispose()
            {
                if (!this.disposed)
                {
                    this.disposed = true;
                    this.unregister();
                }
            }
        }
    }
}

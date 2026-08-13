// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Tests.Diagnostics;

public class SelfDiagnosticsOptionsTests
{
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
        Assert.Equal(10, options.MaxRetainedFiles);
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
    public void OtelLogLevel_DotNetAliases_AreAccepted(string value, LogLevel expected)
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
    public void Sinks_None_DoesNotAffectMinimumLevel()
    {
        // 'none' clears all sinks but preserves MinimumLevel so that a code-level
        // Configure<SelfDiagnosticsOptions> callback can re-enable a sink without also
        // having to re-specify the level. EffectiveLevel already returns LogLevel.None
        // when HasConfiguredSink is false, so the level override is not needed for silence.
        var options = CreateOptions(new Dictionary<string, string?>
        {
            [SelfDiagnosticsOptions.LogLevelEnvVarName] = "debug",
            [SelfDiagnosticsOptions.SinksEnvVarName] = "none",
        });

        Assert.Equal(LogLevel.Debug, options.MinimumLevel);
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
    public void Sinks_FileWithExplicitDirectory_DoesNotCallDefaultDirectoryResolver()
    {
        const string ExplicitDirectory = "/var/log/otel";
        var resolverCalls = 0;

        var options = CreateOptions(
            new Dictionary<string, string?>
            {
                [SelfDiagnosticsOptions.LogDirectoryEnvVarName] = ExplicitDirectory,
                [SelfDiagnosticsOptions.SinksEnvVarName] = "file",
            },
            () =>
            {
                resolverCalls++;
                return Path.Combine("default", "diagnostics");
            });

        Assert.Equal(ExplicitDirectory, options.LogDirectory);
        Assert.Equal(0, resolverCalls);
        Assert.Empty(options.ConfigurationWarnings);
    }

    [Fact]
    public void Sinks_FileWithoutADirectory_UsesDefaultDirectory()
    {
        var expectedDirectory = Path.Combine("default", "diagnostics");
        var options = CreateOptions(
            SelfDiagnosticsOptions.SinksEnvVarName,
            "file",
            () => expectedDirectory);

        Assert.Equal(expectedDirectory, options.LogDirectory);
        Assert.False(options.LogToStdout);
        Assert.False(options.LogToStderr);
        Assert.Empty(options.ConfigurationWarnings);
    }

    [Fact]
    public void Sinks_FileAndStreamWithoutADirectory_UsesDefaultDirectoryAndEnablesStream()
    {
        var expectedDirectory = Path.Combine("default", "diagnostics");
        var options = CreateOptions(
            SelfDiagnosticsOptions.SinksEnvVarName,
            "file,stderr",
            () => expectedDirectory);

        Assert.Equal(expectedDirectory, options.LogDirectory);
        Assert.False(options.LogToStdout);
        Assert.True(options.LogToStderr);
        Assert.Empty(options.ConfigurationWarnings);
    }

    [Fact]
    public void Sinks_FileWithoutResolvableDefaultDirectory_WarnsWritesToStderrAndEnablesNothing()
    {
        string? reported = null;
        var options = CreateOptions(
            new Dictionary<string, string?>
            {
                [SelfDiagnosticsOptions.SinksEnvVarName] = "file",
            },
            () => null,
            message => reported = message);

        Assert.Null(options.LogDirectory);
        Assert.False(options.LogToStdout);
        Assert.False(options.LogToStderr);
        var warning = Assert.Single(options.ConfigurationWarnings);
        Assert.Contains(SelfDiagnosticsOptions.LogDirectoryEnvVarName, warning, StringComparison.Ordinal);
        Assert.Equal(warning, reported);
    }

    [Fact]
    public void Sinks_Absent_DoesNotResolveTheDefaultDirectory()
    {
        var resolverCalls = 0;

        var options = CreateOptions(
            new Dictionary<string, string?>(),
            () =>
            {
                resolverCalls++;
                throw new InvalidOperationException("Should not be called.");
            });

        Assert.Null(options.LogDirectory);
        Assert.Equal(0, resolverCalls);
    }

    [Fact]
    public void DefaultLogDirectory_WindowsUsesLocalApplicationData()
    {
        var localAppData = Path.Combine("user", "local-app-data");

        var result = SelfDiagnosticsLogDirectoryResolver.Resolve(
            () => SelfDiagnosticsPlatform.Windows,
            folder => folder == Environment.SpecialFolder.LocalApplicationData ? localAppData : string.Empty,
            _ => null);

        Assert.Equal(Path.Combine(localAppData, "OpenTelemetry", "dotnet-diagnostics"), result);
    }

    [Fact]
    public void DefaultLogDirectory_WindowsReturnsNullWhenLocalApplicationDataIsUnavailable()
    {
        var result = SelfDiagnosticsLogDirectoryResolver.Resolve(
            () => SelfDiagnosticsPlatform.Windows,
            _ => string.Empty,
            _ => null);

        Assert.Null(result);
    }

    [Fact]
    public void DefaultLogDirectory_MacOSUsesUserLogsDirectory()
    {
        var home = Path.Combine("users", "test-user");

        var result = SelfDiagnosticsLogDirectoryResolver.Resolve(
            () => SelfDiagnosticsPlatform.MacOS,
            folder => folder == Environment.SpecialFolder.UserProfile ? home : string.Empty,
            _ => null);

        Assert.Equal(Path.Combine(home, "Library", "Logs", "OpenTelemetry", "dotnet-diagnostics"), result);
    }

    [Fact]
    public void DefaultLogDirectory_MacOSReturnsNullWhenUserProfileIsUnavailable()
    {
        var result = SelfDiagnosticsLogDirectoryResolver.Resolve(
            () => SelfDiagnosticsPlatform.MacOS,
            _ => string.Empty,
            _ => null);

        Assert.Null(result);
    }

    [Fact]
    public void DefaultLogDirectory_UnixUsesAbsoluteXdgStateHome()
    {
        var result = SelfDiagnosticsLogDirectoryResolver.Resolve(
            () => SelfDiagnosticsPlatform.Unix,
            _ => "/home/test-user",
            name => name == "XDG_STATE_HOME" ? "/var/user-state" : null);

        Assert.Equal(Path.Combine("/var/user-state", "opentelemetry", "dotnet-diagnostics"), result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("relative/state")]
    public void DefaultLogDirectory_UnixUsesUserProfileWhenXdgStateHomeIsNotAbsolute(string? xdgStateHome)
    {
        const string Home = "/home/test-user";

        var result = SelfDiagnosticsLogDirectoryResolver.Resolve(
            () => SelfDiagnosticsPlatform.Unix,
            _ => Home,
            name => name == "XDG_STATE_HOME" ? xdgStateHome : null);

        Assert.Equal(Path.Combine(Home, ".local", "state", "opentelemetry", "dotnet-diagnostics"), result);
    }

    [Fact]
    public void DefaultLogDirectory_ReturnsNullWhenNoUserDirectoryCanBeResolved()
    {
        var result = SelfDiagnosticsLogDirectoryResolver.Resolve(
            () => SelfDiagnosticsPlatform.Unix,
            _ => string.Empty,
            _ => null);

        Assert.Null(result);
    }

    [Fact]
    public void DefaultLogDirectory_ReturnsNullWhenPlatformResolutionThrows()
    {
        var result = SelfDiagnosticsLogDirectoryResolver.Resolve(
            () => throw new InvalidOperationException("Platform unavailable."),
            _ => throw new InvalidOperationException("Folder unavailable."),
            _ => throw new InvalidOperationException("Environment unavailable."));

        Assert.Null(result);
    }

    [Fact]
    public void GetDefaultLogDirectory_MatchesParameterlessResolver()
    {
        Assert.Equal(
            SelfDiagnosticsLogDirectoryResolver.Resolve(),
            SelfDiagnosticsOptions.GetDefaultLogDirectory());
    }

    [Fact]
    public void DefaultLogDirectory_ParameterlessResolve_DoesNotThrow()
    {
        var result = SelfDiagnosticsLogDirectoryResolver.Resolve();

        // Typical CI agents resolve a per-user directory; exotic hosts may return null.
        Assert.True(result is null || result.Length > 0);
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

    private static SelfDiagnosticsOptions CreateOptions(
        string name,
        string? value,
        Func<string?>? defaultLogDirectoryResolver = null,
        Action<string>? reportConfigurationError = null) =>
        CreateOptions(
            new Dictionary<string, string?> { [name] = value },
            defaultLogDirectoryResolver,
            reportConfigurationError);

    private static SelfDiagnosticsOptions CreateOptions(
        Dictionary<string, string?> settings,
        Func<string?>? defaultLogDirectoryResolver = null,
        Action<string>? reportConfigurationError = null) =>
        new(
            new ConfigurationBuilder().AddInMemoryCollection(settings).Build(),
            defaultLogDirectoryResolver ?? SelfDiagnosticsLogDirectoryResolver.Resolve,
            reportConfigurationError);

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

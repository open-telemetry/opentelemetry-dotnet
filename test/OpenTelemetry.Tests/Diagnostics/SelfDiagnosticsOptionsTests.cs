// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry.Diagnostics;

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
    [InlineData("warning", false, LogLevel.None)]
    [InlineData("verbose", false, LogLevel.None)]
    [InlineData("", false, LogLevel.None)]
    [InlineData("0", false, LogLevel.None)]
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
        Assert.Equal(3, options.MaxRetainedFiles);
    }

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

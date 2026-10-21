// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Internal;
using OpenTelemetry.SelfDiagnostics;

namespace OpenTelemetry.Tests.Diagnostics;

[Collection(EnvVarsCollectionDefinition.Name)]
public sealed class SelfDiagnosticsLegacyConfigSourceTests : IDisposable
{
    private readonly TemporaryDirectory directory = new();
    private readonly List<(SelfDiagnosticsOptions? Options, string? ConfigFilePath)> published = [];

    private string ConfigFilePath => Path.Combine(this.directory.Path, SelfDiagnosticsConfigParser.ConfigFileName);

    private SelfDiagnosticsOptions Latest => Assert.IsType<SelfDiagnosticsOptions>(this.published[this.published.Count - 1].Options);

    public void Dispose() => this.directory.Dispose();

    [Theory]
    [InlineData(null, 100_000_000L)]
    [InlineData(-10_000L, -10_000L)]
    [InlineData(10_000L, 10_000L)]
    [InlineData(12_345L, 12_345L)]
    [InlineData(42_949_672_940_000L, 42_949_672_940_000L)]
    public void Constructor_ResolvesValidPollInterval(long? requestedTicks, long expectedTicks)
    {
        this.WriteConfiguration();
        using var source = new SelfDiagnosticsLegacyConfigSource(
            new SelfDiagnosticsConfigParser(() => this.ConfigFilePath),
            requestedTicks is { } ticks ? TimeSpan.FromTicks(ticks) : null,
            this.Publish);

        Assert.Equal(TimeSpan.FromTicks(expectedTicks), source.PollInterval);
        Assert.Equal(expectedTicks != Timeout.InfiniteTimeSpan.Ticks, source.IsPolling);
        Assert.Equal(this.ConfigFilePath, source.ConfigFilePath);
        Assert.Equal(this.ConfigFilePath, Assert.Single(this.published).ConfigFilePath);
    }

    [Theory]
    [InlineData(long.MinValue)]
    [InlineData(-20_000L)]
    [InlineData(-10_001L)]
    [InlineData(-9_999L)]
    [InlineData(-1L)]
    [InlineData(0L)]
    [InlineData(1L)]
    [InlineData(9_999L)]
    [InlineData(42_949_672_940_001L)]
    [InlineData(long.MaxValue)]
    public void Constructor_InvalidPollIntervalThrowsBeforeReadingConfiguration(long ticks)
    {
        var reads = 0;
        var parser = new SelfDiagnosticsConfigParser(() =>
        {
            Interlocked.Increment(ref reads);
            return this.ConfigFilePath;
        });

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            using var source = new SelfDiagnosticsLegacyConfigSource(
                parser,
                TimeSpan.FromTicks(ticks),
                this.Publish);
        });

        Assert.Equal("pollInterval", exception.ParamName);
        Assert.Equal(TimeSpan.FromTicks(ticks), exception.ActualValue);
        Assert.Equal(0, Volatile.Read(ref reads));
        Assert.Empty(this.published);
    }

    [Theory]
    [InlineData("Critical", LogLevel.Critical)]
    [InlineData("Error", LogLevel.Error)]
    [InlineData("Warning", LogLevel.Warning)]
    [InlineData("Informational", LogLevel.Information)]
    [InlineData("Verbose", LogLevel.Debug)]
    [InlineData("LogAlways", LogLevel.Trace)]
    [InlineData("4", LogLevel.Information)]
    [InlineData("0", LogLevel.Trace)]
    [InlineData("9", LogLevel.Trace)]
    [InlineData("Error, Warning", LogLevel.Warning)]
    public void LevelMapping_PreservesLegacyCapture(string level, LogLevel expected)
    {
        this.WriteConfiguration(level);
        using var source = this.CreateSource();

        var options = Assert.IsType<SelfDiagnosticsOptions>(Assert.Single(this.published).Options);
        Assert.Equal(expected, options.MinimumLevel);
        Assert.Equal(Path.GetFullPath("legacy-logs"), options.LogDirectory);
        Assert.Equal(this.ConfigFilePath, source.ConfigFilePath);
        Assert.False(options.AnnounceLogFile, "AnnounceLogFile should be false for legacy configuration.");
        Assert.Equal(
            [
                $"Self-diagnostics was enabled by the legacy OTEL_DIAGNOSTICS.json file at '{this.ConfigFilePath}'. This file is superseded by SelfDiagnosticsOptions and the OTEL_DOTNET_SELF_DIAGNOSTICS_* environment variables.",
                $"OTEL_DIAGNOSTICS.json LogLevel '{level}' was applied as MinimumLevel {expected}.",
            ],
            options.ConfigurationWarnings);
    }

    [Theory]
    [InlineData(0, 1024)]
    [InlineData(100, 1024)]
    [InlineData(1024, 1024)]
    [InlineData(1025, 1025)]
    [InlineData(32768, 32768)]
    [InlineData(131072, 131072)]
    [InlineData(131073, 131072)]
    public void FileSize_IsClampedPerFileAndRetainsTwoFiles(int requested, int clamped)
    {
        this.WriteConfiguration(fileSize: requested);
        using var source = this.CreateSource();

        Assert.Equal(clamped, this.Latest.FileSizeLimitKilobytes);
        Assert.Equal(2, this.Latest.MaxRetainedFiles);

        if (requested == clamped)
        {
            Assert.Equal(2, this.Latest.ConfigurationWarnings.Count);
        }
        else
        {
            Assert.Equal(3, this.Latest.ConfigurationWarnings.Count);
            Assert.Equal(
                FormattableString.Invariant($"OTEL_DIAGNOSTICS.json FileSize {requested} KiB is outside [1024, 131072] and was clamped to {clamped} KiB."),
                this.Latest.ConfigurationWarnings[2]);
        }
    }

    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    [InlineData("null")]
    [InlineData("42")]
    [InlineData("\"invalid\"")]
    public void FormatMessage_IsIgnoredWithAWarning(string value)
    {
        this.WriteConfiguration(extra: $",\"FormatMessage\":{value}");
        using var source = this.CreateSource();

        Assert.Equal(3, this.Latest.ConfigurationWarnings.Count);
        Assert.Equal(
            "OTEL_DIAGNOSTICS.json FormatMessage is no longer supported and was ignored; messages are always formatted.",
            this.Latest.ConfigurationWarnings[2]);
    }

    // Guards against building the legacy options from IConfiguration, which would let
    // OTEL_LOG_LEVEL and OTEL_DOTNET_SELF_DIAGNOSTICS_* override the JSON file.
    [Fact]
    public void Options_DoNotApplyEnvironmentVariableDefaults()
    {
        using var environment = EnvironmentVariableScope.Create(
            ("OTEL_LOG_LEVEL", "none"),
            ("OTEL_DOTNET_SELF_DIAGNOSTICS_SINKS", "stdout,stderr"),
            ("OTEL_DOTNET_SELF_DIAGNOSTICS_LOG_DIRECTORY", "not-the-legacy-directory"),
            ("OTEL_DOTNET_SELF_DIAGNOSTICS_ENV_VARS", "all"));
        this.WriteConfiguration("Verbose");
        using var source = this.CreateSource();

        Assert.Equal(LogLevel.Debug, this.Latest.MinimumLevel);
        Assert.Equal(Path.GetFullPath("legacy-logs"), this.Latest.LogDirectory);
        Assert.Equal(EnvironmentVariableLogMode.KnownSafeValues, this.Latest.EnvironmentVariables);
        Assert.False(this.Latest.LogToStdout, "Expected LogToStdout to be false.");
        Assert.False(this.Latest.LogToStderr, "Expected LogToStderr to be false.");
    }

    // Parser validation is covered by SelfDiagnosticsConfigParserTests; this only checks that
    // a rejected file is not published.
    [Theory]
    [InlineData("not JSON")]
    [InlineData("""{"LogDirectory":"legacy-logs","FileSize":1024,"LogLevel":"invalid"}""")]
    public void InvalidFile_PublishesNothing(string text)
    {
        File.WriteAllText(this.ConfigFilePath, text);
        using var source = this.CreateSource();

        Assert.Empty(this.published);
        Assert.Null(source.ConfigFilePath);
    }

    [Fact]
    public void Refresh_PublishesOnlyChangesAndNullWhenFileIsDeleted()
    {
        using var source = this.CreateSource();

        Assert.False(source.Refresh());
        this.WriteConfiguration();
        Assert.True(source.Refresh());
        Assert.False(source.Refresh());
        this.WriteConfiguration("Error");
        Assert.True(source.Refresh());
        File.Delete(this.ConfigFilePath);
        Assert.True(source.Refresh());
        Assert.False(source.Refresh());

        Assert.Collection(
            this.published,
            change =>
            {
                Assert.Equal(LogLevel.Warning, change.Options!.MinimumLevel);
                Assert.Equal(this.ConfigFilePath, change.ConfigFilePath);
            },
            change =>
            {
                Assert.Equal(LogLevel.Error, change.Options!.MinimumLevel);
                Assert.Equal(this.ConfigFilePath, change.ConfigFilePath);
            },
            change =>
            {
                Assert.Null(change.Options);
                Assert.Null(change.ConfigFilePath);
            });
        Assert.Null(source.ConfigFilePath);
    }

    [Fact]
    public void Refresh_RawInputsAndFilePathArePartOfChangeDetection()
    {
        this.WriteConfiguration("0", fileSize: 10);
        using var source = this.CreateSource();

        this.WriteConfiguration("LogAlways", fileSize: 10);
        Assert.True(source.Refresh(), "Expected the source to refresh after writing new configuration.");
        Assert.Equal(LogLevel.Trace, this.Latest.MinimumLevel);
        Assert.Contains("'LogAlways'", this.Latest.ConfigurationWarnings[1], StringComparison.Ordinal);

        this.WriteConfiguration("LogAlways", fileSize: 20);
        Assert.True(source.Refresh(), "Expected the source to refresh after changing the file size.");
        Assert.Contains("FileSize 20 KiB", this.Latest.ConfigurationWarnings[2], StringComparison.Ordinal);

        this.WriteConfiguration("LogAlways", fileSize: 20, extra: ",\"FormatMessage\":false");
        Assert.True(source.Refresh(), "Expected the source to refresh after adding extra configuration.");
        this.WriteConfiguration("LogAlways", fileSize: 20, extra: ",\"FormatMessage\":true");
        Assert.False(source.Refresh(), "Expected the source to not refresh when there are no changes.");

        var otherPath = Path.Combine(this.directory.Path, "other.json");
        File.Copy(this.ConfigFilePath, otherPath);
        var selectedPath = this.ConfigFilePath;
        var located = new List<(SelfDiagnosticsOptions? Options, string? ConfigFilePath)>();
        using var locatedSource = new SelfDiagnosticsLegacyConfigSource(
            new SelfDiagnosticsConfigParser(() => selectedPath),
            Timeout.InfiniteTimeSpan,
            (options, path) => located.Add((options, path)));
        selectedPath = otherPath;
        Assert.True(locatedSource.Refresh(), "Expected the located source to refresh after changing the selected path.");
        Assert.Equal(otherPath, locatedSource.ConfigFilePath);
        Assert.Equal([this.ConfigFilePath, otherPath], located.Select(change => change.ConfigFilePath));
        Assert.Contains(otherPath, located[located.Count - 1].Options!.ConfigurationWarnings[0], StringComparison.Ordinal);
    }

    [Fact]
    public void LocatorFailure_IsSafeDuringConstructionAndCanRecover()
    {
        var fail = true;
        using var source = new SelfDiagnosticsLegacyConfigSource(
            new SelfDiagnosticsConfigParser(() => fail ? throw new IOException("Unavailable.") : this.ConfigFilePath),
            Timeout.InfiniteTimeSpan,
            this.Publish);

        Assert.Empty(this.published);
        fail = false;
        this.WriteConfiguration();
        Assert.True(source.Refresh(), "Expected the source to refresh after writing configuration.");
        Assert.NotNull(this.Latest.LogDirectory);
    }

    [Fact]
    public void PublishFailure_IsReportedAndNotRetriedUntilTheFileChanges()
    {
        this.WriteConfiguration();
        var publishes = 0;
        var originalError = Console.Error;
        using var error = new StringWriter();
        SelfDiagnosticsLegacyConfigSource source;

        Console.SetError(error);
        try
        {
            source = new SelfDiagnosticsLegacyConfigSource(
                new SelfDiagnosticsConfigParser(() => this.ConfigFilePath),
                Timeout.InfiniteTimeSpan,
                (_, _) =>
                {
                    publishes++;
                    throw new InvalidOperationException("Pipeline unavailable.");
                });
        }
        finally
        {
            Console.SetError(originalError);
        }

        using (source)
        {
            Assert.Contains("Pipeline unavailable.", error.ToString(), StringComparison.Ordinal);
            Assert.Equal(this.ConfigFilePath, source.ConfigFilePath);

            // The file is treated as applied, so an unchanged file does not republish on every poll.
            Assert.False(source.Refresh(), "Expected an unchanged file not to be republished.");
            Assert.Equal(1, publishes);

            this.WriteConfiguration("Error");
            Assert.Throws<InvalidOperationException>(() => source.Refresh());
            Assert.Equal(2, publishes);
        }
    }

    [Fact]
    public void PublishFailure_WhenStandardErrorIsUnavailable_DoesNotEscapeConstruction()
    {
        this.WriteConfiguration();
        var publishes = 0;
        var originalError = Console.Error;
        using var error = new StringWriter();
        error.Dispose();

        Console.SetError(error);
        try
        {
            using var source = new SelfDiagnosticsLegacyConfigSource(
                new SelfDiagnosticsConfigParser(() => this.ConfigFilePath),
                Timeout.InfiniteTimeSpan,
                (_, _) =>
                {
                    publishes++;
                    throw new InvalidOperationException("Pipeline unavailable.");
                });

            Assert.Equal(1, publishes);
            Assert.Equal(this.ConfigFilePath, source.ConfigFilePath);
            Assert.False(source.Refresh(), "Expected an unchanged file not to be republished.");
            Assert.Equal(1, publishes);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    [Fact]
    public void ConcurrentRefreshes_SerializeReadsAndPublication()
    {
        this.WriteConfiguration();
        var reads = 0;
        var notifications = new List<(bool HasOptions, string? ConfigFilePath)>();
        using var source = new SelfDiagnosticsLegacyConfigSource(
            new SelfDiagnosticsConfigParser(() => Interlocked.Increment(ref reads) % 2 == 0 ? this.ConfigFilePath : null),
            Timeout.InfiniteTimeSpan,
            (options, path) => notifications.Add((options is not null, path)));

        Parallel.For(0, 40, _ => source.Refresh());

        Assert.Equal(41, reads);
        Assert.Equal(40, notifications.Count);
        for (var i = 0; i < notifications.Count; i++)
        {
            Assert.Equal((i % 2 == 0, i % 2 == 0 ? this.ConfigFilePath : null), notifications[i]);
        }
    }

    [Fact]
    public void Timer_PollsUntilDisposed()
    {
        var reads = 0;
        var source = new SelfDiagnosticsLegacyConfigSource(
            new SelfDiagnosticsConfigParser(() =>
            {
                Interlocked.Increment(ref reads);
                return null;
            }),
            TimeSpan.FromMilliseconds(10),
            this.Publish);

        Assert.True(SelfDiagnosticsTestHelpers.WaitUntil(() => Volatile.Read(ref reads) >= 4), "Expected the timer to poll at least 4 times before disposal.");
        source.Dispose();
        var readsAfterDisposal = Volatile.Read(ref reads);
        Thread.Sleep(50);

        Assert.Equal(readsAfterDisposal, Volatile.Read(ref reads));
        Assert.False(source.Refresh(), "Expected the source to not refresh after disposal.");
        source.Dispose();
    }

    [Fact]
    public void Timer_CallbackAfterDisposal_DoesNotReadPublishOrRearm()
    {
        var reads = 0;
        using var source = new SelfDiagnosticsLegacyConfigSource(
            new SelfDiagnosticsConfigParser(() =>
            {
                reads++;
                return this.ConfigFilePath;
            }),
            Timeout.InfiniteTimeSpan,
            this.Publish);
        source.Dispose();
        this.WriteConfiguration();

        // Simulate a queued callback arriving after disposal without racing a real timer.
        var onTimer = typeof(SelfDiagnosticsLegacyConfigSource).GetMethod(
            "OnTimer",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(onTimer);
        onTimer.Invoke(source, null);

        Assert.Equal(1, reads);
        Assert.Empty(this.published);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Timer_DoesNotCaptureExecutionContext(bool alreadySuppressed)
    {
        var ambient = new AsyncLocal<string?> { Value = "caller-context" };
        var observed = new ConcurrentQueue<string?>();
        var parser = new SelfDiagnosticsConfigParser(() =>
        {
            observed.Enqueue(ambient.Value);
            return null;
        });

        SelfDiagnosticsLegacyConfigSource source;
        if (alreadySuppressed)
        {
            using (ExecutionContext.SuppressFlow())
            {
                source = new SelfDiagnosticsLegacyConfigSource(parser, TimeSpan.FromMilliseconds(10), this.Publish);
            }
        }
        else
        {
            source = new SelfDiagnosticsLegacyConfigSource(parser, TimeSpan.FromMilliseconds(10), this.Publish);
        }

        using (source)
        {
            Assert.True(SelfDiagnosticsTestHelpers.WaitUntil(() => observed.Count >= 3));
        }

        var contexts = observed.ToArray();
        Assert.Equal("caller-context", contexts[0]);
        Assert.All(contexts.Skip(1), Assert.Null);
    }

    private SelfDiagnosticsLegacyConfigSource CreateSource()
        => new(new SelfDiagnosticsConfigParser(() => this.ConfigFilePath), Timeout.InfiniteTimeSpan, this.Publish);

    private void Publish(SelfDiagnosticsOptions? options, string? configFilePath)
        => this.published.Add((options, configFilePath));

    private void WriteConfiguration(
        string level = "Warning",
        int fileSize = 32768,
        string logDirectory = "legacy-logs",
        string extra = "")
        => File.WriteAllText(this.ConfigFilePath, $$"""{"LogDirectory":"{{logDirectory}}","FileSize":{{fileSize}},"LogLevel":"{{level}}"{{extra}}}""");
}

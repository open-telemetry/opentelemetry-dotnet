// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;
using Microsoft.Extensions.Logging;
using OpenTelemetry.SelfDiagnostics;

namespace OpenTelemetry.Internal;

/// <summary>
/// Polls the legacy <c>OTEL_DIAGNOSTICS.json</c> file and maps it to
/// <see cref="SelfDiagnosticsOptions"/> for the process-wide self-diagnostics pipeline.
/// </summary>
#if NET
[System.Runtime.Versioning.UnsupportedOSPlatform("browser")]
#endif
internal sealed class SelfDiagnosticsLegacyConfigSource : IDisposable
{
    private const int MinFileSizeKilobytes = 1024;
    private const int MaxFileSizeKilobytes = 128 * 1024;

    // The legacy FileSize is split across this many files so total disk use stays close to the
    // configured limit, while the previous file survives a rollover.
    private const int RetainedFileCount = 2;

    private static readonly TimeSpan DefaultPollInterval = TimeSpan.FromSeconds(10);

    private readonly Lock refreshLock = new();
    private readonly SelfDiagnosticsConfigParser parser;
    private readonly Action<SelfDiagnosticsOptions?, string?> onChanged;
    private readonly TimeSpan pollInterval;
    private readonly Timer? timer;

    private SelfDiagnosticsLegacyConfiguration? current;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="SelfDiagnosticsLegacyConfigSource"/> class.
    /// Reads the file once, synchronously, then polls it every <paramref name="pollInterval"/>.
    /// </summary>
    /// <param name="parser">Reads and parses the legacy file.</param>
    /// <param name="pollInterval">The poll interval, or <see cref="Timeout.InfiniteTimeSpan"/> to disable polling.
    /// </param>
    /// <param name="onChanged">
    /// Receives the new options and file path, or <see langword="null"/> for both when no valid file exists.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">The poll interval is outside the supported range.</exception>
    internal SelfDiagnosticsLegacyConfigSource(
        SelfDiagnosticsConfigParser parser,
        TimeSpan? pollInterval,
        Action<SelfDiagnosticsOptions?, string?> onChanged)
    {
        this.pollInterval = pollInterval ?? DefaultPollInterval;
        if (this.pollInterval != Timeout.InfiniteTimeSpan
            && (this.pollInterval.TotalMilliseconds < 1 || this.pollInterval.TotalMilliseconds > uint.MaxValue - 1))
        {
            throw new ArgumentOutOfRangeException(
                nameof(pollInterval),
                pollInterval,
                "The poll interval must be between 1 and 4,294,967,294 milliseconds, or Timeout.InfiniteTimeSpan.");
        }

        this.parser = parser;
        this.onChanged = onChanged;

        this.TryRefresh();

        if (this.pollInterval != Timeout.InfiniteTimeSpan)
        {
            // Do not capture the ExecutionContext (and its AsyncLocal values) of whichever caller
            // first touched the SDK. SuppressFlow throws if flow is already suppressed.
            if (ExecutionContext.IsFlowSuppressed())
            {
                this.timer = this.CreateTimer();
            }
            else
            {
                using (ExecutionContext.SuppressFlow())
                {
                    this.timer = this.CreateTimer();
                }
            }

            this.timer.Change(this.pollInterval, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>
    /// Gets the resolved poll interval.
    /// </summary>
    internal TimeSpan PollInterval => this.pollInterval;

    /// <summary>
    /// Gets a value indicating whether the file is polled for changes.
    /// </summary>
    internal bool IsPolling => this.timer is not null;

    /// <summary>
    /// Gets the full path of the legacy file currently applied, or <see langword="null"/> when none is.
    /// </summary>
    internal string? ConfigFilePath
    {
        get
        {
            lock (this.refreshLock)
            {
                return this.current?.ConfigFilePath;
            }
        }
    }

    public void Dispose()
    {
        lock (this.refreshLock)
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
        }

        this.timer?.Dispose();
    }

    /// <summary>
    /// Re-reads the file and publishes the result when it changed.
    /// </summary>
    /// <returns><see langword="true"/> when a change was published.</returns>
    internal bool Refresh()
    {
        lock (this.refreshLock)
        {
            if (this.disposed)
            {
                return false;
            }

            SelfDiagnosticsLegacyConfiguration? next = this.parser.TryGetConfiguration(out var configuration)
                ? configuration
                : null;

            if (Nullable.Equals(this.current, next))
            {
                return false;
            }

            this.current = next;
            this.onChanged(next is { } value ? CreateOptions(value) : null, next?.ConfigFilePath);
            return true;
        }
    }

    private static SelfDiagnosticsOptions CreateOptions(SelfDiagnosticsLegacyConfiguration configuration)
    {
        // The legacy LogLevel is a capture threshold, not the level of an event: LogAlways (0) and
        // any value above Verbose meant "capture everything", so they map to Trace.
        var level = configuration.LogLevel switch
        {
            EventLevel.Critical => LogLevel.Critical,
            EventLevel.Error => LogLevel.Error,
            EventLevel.Warning => LogLevel.Warning,
            EventLevel.Informational => LogLevel.Information,
            EventLevel.Verbose => LogLevel.Debug,
            _ => LogLevel.Trace,
        };

        var fileSizeKilobytes = Math.Min(Math.Max(configuration.FileSizeKilobytes, MinFileSizeKilobytes), MaxFileSizeKilobytes);

        List<string> warnings =
        [
            $"Self-diagnostics was enabled by the legacy OTEL_DIAGNOSTICS.json file at '{configuration.ConfigFilePath}'. This file is superseded by SelfDiagnosticsOptions and the OTEL_DOTNET_SELF_DIAGNOSTICS_* environment variables.",
            $"OTEL_DIAGNOSTICS.json LogLevel '{configuration.RawLogLevel}' was applied as MinimumLevel {level}.",
        ];

        if (fileSizeKilobytes != configuration.FileSizeKilobytes)
        {
            warnings.Add(FormattableString.Invariant(
                $"OTEL_DIAGNOSTICS.json FileSize {configuration.FileSizeKilobytes} KiB is outside [{MinFileSizeKilobytes}, {MaxFileSizeKilobytes}] and was clamped to {fileSizeKilobytes} KiB."));
        }

        if (configuration.FormatMessageSpecified)
        {
            warnings.Add("OTEL_DIAGNOSTICS.json FormatMessage is no longer supported and was ignored; messages are always formatted.");
        }

        return new SelfDiagnosticsOptions
        {
            LogDirectory = configuration.LogDirectory,
            FileSizeLimitKilobytes = fileSizeKilobytes / RetainedFileCount,
            MaxRetainedFiles = RetainedFileCount,
            MinimumLevel = level,
            ConfigurationWarnings = [.. warnings],
            AnnounceLogFile = false,
        };
    }

    private Timer CreateTimer()
        => new(static state => ((SelfDiagnosticsLegacyConfigSource)state!).OnTimer(), this, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

    // Called from the constructor, which runs during SDK type initialization, and from the timer;
    // neither may throw.
    private void TryRefresh()
    {
        try
        {
            this.Refresh();
        }
        catch (Exception exception)
        {
            try
            {
                Console.Error.WriteLine($"OpenTelemetry self-diagnostics legacy configuration refresh failed: {exception}");
            }
            catch
            {
                // Standard error may be unavailable.
            }
        }
    }

    private void OnTimer()
    {
        lock (this.refreshLock)
        {
            if (this.disposed)
            {
                return;
            }

            this.TryRefresh();

            // One-shot timer re-armed after each poll so callbacks never overlap.
            this.timer!.Change(this.pollInterval, Timeout.InfiniteTimeSpan);
        }
    }
}

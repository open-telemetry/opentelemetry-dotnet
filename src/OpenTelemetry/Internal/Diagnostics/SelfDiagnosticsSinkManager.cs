// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;

namespace OpenTelemetry.Internal;

/// <summary>
/// Translates <see cref="SelfDiagnosticsOptions"/> into a sink set defining which sinks exist,
/// when a sink must be recreated versus updated in place, and in what order sinks are arranged.
/// </summary>
/// <remarks>
/// <para>
/// Not thread-safe by design: <see cref="SelfDiagnosticsLogger"/> serializes all calls through
/// its update lock. Disposal of replaced sinks is owned by
/// <see cref="SelfDiagnosticsSinkDispatcher.UpdateSinks"/>, so this class only
/// decides identity, never lifetime.
/// </para>
/// <para>
/// File sink policy: any change to directory, size limit, or retention swaps in a fresh sink
/// (the new sink self-heals if its target is temporarily unavailable - see
/// <see cref="SelfDiagnosticsFileSink"/>); an unchanged configuration keeps the current sink.
/// Console sink policy: created/dropped as the stdout/stderr flags toggle; flag changes on a
/// live sink are applied in place.
/// </para>
/// <para>
/// Sink order matters for the dispatcher's format-once optimization: sinks sharing a formatter
/// instance (file + console, both <see cref="SelfDiagnosticsTextFormatter.Instance"/>) are kept
/// adjacent, with the raw-entry external-logger sink last.
/// </para>
/// </remarks>
[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Sink lifetime is owned by SelfDiagnosticsSinkDispatcher: replaced sinks are disposed by UpdateSinks (reference diff) and the live set by the dispatcher's Dispose. This type only tracks sink identity to decide recreate-versus-update.")]
internal sealed class SelfDiagnosticsSinkManager
{
    private readonly Func<SelfDiagnosticsOptions.SelfDiagnosticsConfiguration, string> preambleBuilder;
    private readonly Action<string> reportError;
    private readonly Func<TextWriter>? stdoutWriter;
    private readonly Func<TextWriter>? stderrWriter;
    private readonly TimeSpan? fileRetryInterval;

    private SelfDiagnosticsFileSink? fileSink;
    private SelfDiagnosticsConsoleSink? consoleSink;

    private string? activeFileDirectory;
    private int activeFileSizeLimitKilobytes;
    private int activeMaxRetainedFiles;

    // The configuration the preamble should describe. Held as mutable state rather than captured
    // per sink so that a change affecting only the preamble (the environment variable disclosure
    // mode) is picked up at the next file open without recreating the file sink and cutting the
    // current file short.
    private SelfDiagnosticsOptions.SelfDiagnosticsConfiguration latestConfiguration
        = SelfDiagnosticsOptions.SelfDiagnosticsConfiguration.Disabled;

    internal SelfDiagnosticsSinkManager(
        Func<SelfDiagnosticsOptions.SelfDiagnosticsConfiguration, string> preambleBuilder,
        Action<string> reportError,
        Func<TextWriter>? stdoutWriter = null,
        Func<TextWriter>? stderrWriter = null,
        TimeSpan? fileRetryInterval = null)
    {
        this.preambleBuilder = preambleBuilder;
        this.reportError = reportError;
        this.stdoutWriter = stdoutWriter;
        this.stderrWriter = stderrWriter;
        this.fileRetryInterval = fileRetryInterval;
    }

    /// <summary>
    /// Applies the options and returns the resulting sink set for the dispatcher.
    /// </summary>
    /// <param name="configuration">The current configuration.</param>
    /// <returns>The sink array to install, ordered for format sharing.</returns>
    internal ISelfDiagnosticsSink[] ApplyOptions(SelfDiagnosticsOptions.SelfDiagnosticsConfiguration configuration)
    {
        this.latestConfiguration = configuration;
        this.ApplyFileOptions(configuration);
        this.ApplyConsoleOptions(configuration);
        return this.Snapshot();
    }

    private string BuildPreamble() => this.preambleBuilder(this.latestConfiguration);

    private void ApplyFileOptions(SelfDiagnosticsOptions.SelfDiagnosticsConfiguration configuration)
    {
        var fileEnabled = !string.IsNullOrEmpty(configuration.LogDirectory);
        if (!fileEnabled)
        {
            this.fileSink = null;
            this.activeFileDirectory = null;
            return;
        }

        if (this.fileSink is not null
            && configuration.LogDirectory == this.activeFileDirectory
            && configuration.FileSizeLimitKilobytes == this.activeFileSizeLimitKilobytes
            && configuration.MaxRetainedFiles == this.activeMaxRetainedFiles)
        {
            return; // unchanged
        }

        // Honor the latest configuration even if the new target is currently unavailable:
        // the replacement sink retries opening on its own (self-healing), whereas keeping the
        // old sink would silently ignore the requested change.
        //
        // The outgoing sink is still open at this point (the dispatcher disposes replaced sinks
        // only after this returns), so its current file is passed through as ineligible for
        // retention pruning.
        this.fileSink = new SelfDiagnosticsFileSink(
            configuration.LogDirectory!,
            configuration.FileSizeLimitKilobytes,
            configuration.MaxRetainedFiles,
            this.BuildPreamble,
            this.reportError,
            this.fileRetryInterval,
            excludeFromPruning: this.fileSink?.CurrentFilePath);

        this.activeFileDirectory = configuration.LogDirectory;
        this.activeFileSizeLimitKilobytes = configuration.FileSizeLimitKilobytes;
        this.activeMaxRetainedFiles = configuration.MaxRetainedFiles;
    }

    private void ApplyConsoleOptions(SelfDiagnosticsOptions.SelfDiagnosticsConfiguration configuration)
    {
        var consoleEnabled = configuration.LogToStdout || configuration.LogToStderr;
        if (!consoleEnabled)
        {
            this.consoleSink = null;
            return;
        }

        if (this.consoleSink is null)
        {
            this.consoleSink = new SelfDiagnosticsConsoleSink(
                configuration.LogToStdout,
                configuration.LogToStderr,
                this.stdoutWriter,
                this.stderrWriter);
        }
        else
        {
            this.consoleSink.UpdateConsoleFlags(configuration.LogToStdout, configuration.LogToStderr);
        }
    }

    private ISelfDiagnosticsSink[] Snapshot()
    {
        if (this.fileSink is not null && this.consoleSink is not null)
        {
            return [this.fileSink, this.consoleSink];
        }

        if (this.fileSink is not null)
        {
            return [this.fileSink];
        }

        if (this.consoleSink is not null)
        {
            return [this.consoleSink];
        }

        return [];
    }
}

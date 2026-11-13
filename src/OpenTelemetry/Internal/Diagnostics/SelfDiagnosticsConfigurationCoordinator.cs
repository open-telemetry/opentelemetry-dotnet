// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;
using OpenTelemetry.SelfDiagnostics;

using SelfDiagnosticsConfiguration = OpenTelemetry.SelfDiagnostics.SelfDiagnosticsOptions.SelfDiagnosticsConfiguration;

namespace OpenTelemetry.Internal;

/// <summary>
/// Coordinates configuration supplied by independently-owned providers and the
/// legacy <c>OTEL_DIAGNOSTICS.json</c> file.
/// </summary>
/// <remarks>
/// Ownership goes to the most recently registered provider with a configured sink,
/// then the legacy configuration with a configured sink, then the most recent provider.
/// This prevents providers using default options from disabling existing diagnostics.
/// </remarks>
internal sealed class SelfDiagnosticsConfigurationCoordinator : IDisposable
{
    private readonly Lock syncLock = new();
    private readonly List<Registration> registrations = [];
    private readonly Action<SelfDiagnosticsConfiguration> applyConfiguration;
    private readonly Action<string>? onLegacyConfigurationIgnored;

    private SelfDiagnosticsConfiguration? legacyConfiguration;
    private string? legacyConfigFilePath;
    private SelfDiagnosticsConfiguration activeConfiguration = SelfDiagnosticsConfiguration.Disabled;
    private bool legacyIgnoredReported;
    private bool disposed;

    internal SelfDiagnosticsConfigurationCoordinator(
        Action<SelfDiagnosticsConfiguration> applyConfiguration,
        Action<string>? onLegacyConfigurationIgnored = null)
    {
        this.applyConfiguration = applyConfiguration;
        this.onLegacyConfigurationIgnored = onLegacyConfigurationIgnored;
    }

    public void Dispose()
    {
        Registration[] registrations;

        lock (this.syncLock)
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
            registrations = [.. this.registrations];
            this.registrations.Clear();
            this.legacyConfiguration = null;
            this.legacyConfigFilePath = null;
            this.activeConfiguration = SelfDiagnosticsConfiguration.Disabled;

            foreach (var registration in registrations)
            {
                registration.Registered = false;
            }
        }

        foreach (var registration in registrations)
        {
            registration.DisposeSubscription();
        }
    }

    internal IDisposable Register(IOptionsMonitor<SelfDiagnosticsOptions> monitor)
    {
        Guard.ThrowIfNull(monitor);

        var registration = new Registration(this, monitor);
        registration.Subscribe();

        bool registered;
        string? ignoredConfigFilePath = null;
        lock (this.syncLock)
        {
            registered = !this.disposed;
            if (registered)
            {
                registration.Registered = true;
                this.registrations.Add(registration);
                ignoredConfigFilePath = this.ReevaluateActiveUnderLock();
            }
        }

        if (!registered)
        {
            registration.DisposeSubscription();
            throw new ObjectDisposedException(nameof(SelfDiagnosticsConfigurationCoordinator));
        }

        this.ReportIgnoredIf(ignoredConfigFilePath);
        return registration;
    }

    /// <summary>
    /// Sets the configuration read from the legacy <c>OTEL_DIAGNOSTICS.json</c> file.
    /// </summary>
    /// <param name="options">The legacy options, or <see langword="null"/> when no valid file exists.</param>
    /// <param name="configFilePath">The legacy file path, or <see langword="null"/> when no valid file exists.</param>
    internal void SetLegacyConfiguration(SelfDiagnosticsOptions? options, string? configFilePath)
    {
        if (options is not null)
        {
            Guard.ThrowIfNull(configFilePath);
        }

        var configuration = options is null ? null : SelfDiagnosticsConfiguration.Create(options);

        string? ignoredConfigFilePath;
        lock (this.syncLock)
        {
            if (this.disposed)
            {
                return;
            }

            this.legacyConfiguration = configuration;
            this.legacyConfigFilePath = options is null ? null : configFilePath;
            ignoredConfigFilePath = this.ReevaluateActiveUnderLock();
        }

        this.ReportIgnoredIf(ignoredConfigFilePath);
    }

    /// <summary>
    /// Selects and applies the active configuration snapshot when it changes.
    /// Must be called while holding <see cref="syncLock"/>.
    /// </summary>
    /// <returns>The ignored legacy file path to report after releasing the lock, or <see langword="null"/>.</returns>
    private string? ReevaluateActiveUnderLock()
    {
        var configuration = this.SelectConfigurationUnderLock();

        // Warn once per period in which provider configuration overrides the legacy file.
        var legacyIgnored = this.legacyConfiguration is { HasConfiguredSink: true }
            && !ReferenceEquals(configuration, this.legacyConfiguration);
        var ignoredConfigFilePath = legacyIgnored && !this.legacyIgnoredReported ? this.legacyConfigFilePath : null;
        this.legacyIgnoredReported = legacyIgnored;

        if (!ReferenceEquals(configuration, this.activeConfiguration))
        {
            this.activeConfiguration = configuration;
            this.applyConfiguration(configuration);
        }

        return ignoredConfigFilePath;
    }

    private SelfDiagnosticsConfiguration SelectConfigurationUnderLock()
    {
        // Prefer the newest provider with a sink; default options must not silence existing diagnostics.
        for (var i = this.registrations.Count - 1; i >= 0; i--)
        {
            var configuration = this.registrations[i].Configuration;
            if (configuration.HasConfiguredSink)
            {
                return configuration;
            }
        }

        if (this.legacyConfiguration is { HasConfiguredSink: true } legacy)
        {
            return legacy;
        }

        return this.registrations.Count > 0
            ? this.registrations[this.registrations.Count - 1].Configuration
            : SelfDiagnosticsConfiguration.Disabled;
    }

    // Invoked outside syncLock so the callback (which writes an EventSource event) cannot block
    // or re-enter configuration changes.
    private void ReportIgnoredIf(string? ignoredConfigFilePath)
    {
        if (ignoredConfigFilePath is not null)
        {
            this.onLegacyConfigurationIgnored?.Invoke(ignoredConfigFilePath);
        }
    }

    private void ConfigurationChanged(Registration registration, SelfDiagnosticsConfiguration configuration)
    {
        string? ignoredConfigFilePath = null;
        lock (this.syncLock)
        {
            if (this.disposed)
            {
                return;
            }

            registration.Configuration = configuration;
            if (registration.Registered)
            {
                ignoredConfigFilePath = this.ReevaluateActiveUnderLock();
            }
        }

        this.ReportIgnoredIf(ignoredConfigFilePath);
    }

    private void Unregister(Registration registration)
    {
        string? ignoredConfigFilePath;
        lock (this.syncLock)
        {
            if (!registration.Registered)
            {
                return;
            }

            registration.Registered = false;
            this.registrations.Remove(registration);

            ignoredConfigFilePath = this.ReevaluateActiveUnderLock();
        }

        this.ReportIgnoredIf(ignoredConfigFilePath);
    }

    private sealed class Registration : IDisposable
    {
        private readonly SelfDiagnosticsConfigurationCoordinator owner;
        private readonly IOptionsMonitor<SelfDiagnosticsOptions> monitor;
#pragma warning disable CA2213 // Disposed through an atomic exchange in Dispose/DisposeSubscription.
        private IDisposable? subscription;
#pragma warning restore CA2213

        internal Registration(
            SelfDiagnosticsConfigurationCoordinator owner,
            IOptionsMonitor<SelfDiagnosticsOptions> monitor)
        {
            this.owner = owner;
            this.monitor = monitor;
        }

        // Configuration and membership are accessed only under the coordinator's syncLock.
        internal SelfDiagnosticsConfiguration Configuration { get; set; } = SelfDiagnosticsConfiguration.Disabled;

        internal bool Registered { get; set; }

        public void Dispose()
        {
            this.owner.Unregister(this);
            this.DisposeSubscription();
        }

        internal void Subscribe()
        {
            this.Publish(this.monitor.CurrentValue);
            this.subscription = this.monitor.OnChange((options, _) => this.Publish(options));
            this.Publish(this.monitor.CurrentValue);
        }

        internal void DisposeSubscription() =>
            Interlocked.Exchange(ref this.subscription, null)?.Dispose();

        private void Publish(SelfDiagnosticsOptions options)
            => this.owner.ConfigurationChanged(this, SelfDiagnosticsConfiguration.Create(options));
    }
}

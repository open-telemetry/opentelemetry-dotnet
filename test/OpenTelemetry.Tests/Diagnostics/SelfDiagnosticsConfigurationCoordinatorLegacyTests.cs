// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry.Internal;
using OpenTelemetry.SelfDiagnostics;

using Configuration = OpenTelemetry.SelfDiagnostics.SelfDiagnosticsOptions.SelfDiagnosticsConfiguration;

namespace OpenTelemetry.Tests.Diagnostics;

public class SelfDiagnosticsConfigurationCoordinatorLegacyTests
{
    [Fact]
    public void LegacyConfiguration_WithoutProviders_IsAppliedImmediatelyAndOnUpdate()
    {
        var applied = new List<Configuration>();
        using var coordinator = new SelfDiagnosticsConfigurationCoordinator(applied.Add);

        coordinator.SetLegacyConfiguration(FileOptions("legacy"), "legacy.json");
        coordinator.SetLegacyConfiguration(FileOptions("updated-legacy"), "updated-legacy.json");

        Assert.Collection(
            applied,
            configuration => Assert.Equal("legacy", configuration.LogDirectory),
            configuration => Assert.Equal("updated-legacy", configuration.LogDirectory));
    }

    [Fact]
    public void ClearingAbsentLegacyConfiguration_AppliesNothing()
    {
        var applied = new List<Configuration>();
        using var coordinator = new SelfDiagnosticsConfigurationCoordinator(applied.Add);

        coordinator.SetLegacyConfiguration(null, null);

        Assert.Empty(applied);
    }

    [Fact]
    public void LegacyConfiguration_WithoutFilePathThrowsBeforeApplying()
    {
        var applied = new List<Configuration>();
        using var coordinator = new SelfDiagnosticsConfigurationCoordinator(applied.Add);

        var exception = Assert.Throws<ArgumentNullException>(
            () => coordinator.SetLegacyConfiguration(FileOptions("legacy"), null));

        Assert.Equal("configFilePath", exception.ParamName);
        Assert.Empty(applied);
    }

    [Theory]
    [InlineData(LogLevel.Warning)]
    [InlineData(LogLevel.Debug)]
    [InlineData(LogLevel.None)]
    public void ProviderWithoutSink_KeepsLegacyWithoutReapplying(LogLevel minimumLevel)
    {
        var applied = new List<Configuration>();
        var reports = 0;
        using var coordinator = new SelfDiagnosticsConfigurationCoordinator(applied.Add, _ => reports++);
        coordinator.SetLegacyConfiguration(FileOptions("legacy"), "legacy.json");

        using var provider = coordinator.Register(new TestMonitor(new SelfDiagnosticsOptions { MinimumLevel = minimumLevel }));

        Assert.Equal("legacy", Assert.Single(applied).LogDirectory);
        Assert.Equal(0, reports);
    }

    [Fact]
    public void ProviderWithSink_TakesOverAndReportsIgnoredLegacyOnce()
    {
        var applied = new List<Configuration>();
        var ignoredPaths = new List<string>();
        using var coordinator = new SelfDiagnosticsConfigurationCoordinator(applied.Add, path =>
        {
            Assert.True(applied[applied.Count - 1].LogToStdout, "Expected the last applied configuration to log to stdout.");
            ignoredPaths.Add(path);
        });
        coordinator.SetLegacyConfiguration(FileOptions("legacy"), "legacy.json");
        var providerMonitor = new TestMonitor(new SelfDiagnosticsOptions { LogToStdout = true });
        using var provider = coordinator.Register(providerMonitor);

        coordinator.SetLegacyConfiguration(FileOptions("ignored-update"), "ignored-update.json");
        providerMonitor.Set(new SelfDiagnosticsOptions { LogToStdout = true, MinimumLevel = LogLevel.Error });

        Assert.Collection(
            applied,
            configuration => Assert.Equal("legacy", configuration.LogDirectory),
            configuration => Assert.True(configuration.LogToStdout),
            configuration => Assert.Equal(LogLevel.Error, configuration.MinimumLevel));
        Assert.Equal("legacy.json", Assert.Single(ignoredPaths));
    }

    [Fact]
    public void LegacyArrivingAfterProviderWithSink_IsReportedAndNotApplied()
    {
        var applied = new List<Configuration>();
        var ignoredPaths = new List<string>();
        using var coordinator = new SelfDiagnosticsConfigurationCoordinator(applied.Add, ignoredPaths.Add);
        using var provider = coordinator.Register(FileMonitor("provider"));

        coordinator.SetLegacyConfiguration(FileOptions("legacy"), "legacy.json");

        Assert.Equal("provider", Assert.Single(applied).LogDirectory);
        Assert.Equal("legacy.json", Assert.Single(ignoredPaths));
    }

    [Fact]
    public void ProviderDisposal_RestoresLatestLegacyAndResetsIgnoredReporting()
    {
        var applied = new List<Configuration>();
        var ignoredPaths = new List<string>();
        using var coordinator = new SelfDiagnosticsConfigurationCoordinator(applied.Add, ignoredPaths.Add);
        coordinator.SetLegacyConfiguration(FileOptions("legacy"), "legacy.json");

        using (coordinator.Register(new TestMonitor(new SelfDiagnosticsOptions { LogToStdout = true })))
        {
            coordinator.SetLegacyConfiguration(FileOptions("latest-legacy"), "latest-legacy.json");
            Assert.Equal(2, applied.Count);
            Assert.Equal("legacy.json", Assert.Single(ignoredPaths));
        }

        Assert.Equal("latest-legacy", applied[applied.Count - 1].LogDirectory);
        using var nextProvider = coordinator.Register(new TestMonitor(new SelfDiagnosticsOptions { LogToStderr = true }));
        Assert.Equal(["legacy.json", "latest-legacy.json"], ignoredPaths);
    }

    [Fact]
    public void LegacyCleared_FallsBackToLatestProvider()
    {
        var applied = new List<Configuration>();
        using var coordinator = new SelfDiagnosticsConfigurationCoordinator(applied.Add);
        coordinator.SetLegacyConfiguration(FileOptions("legacy"), "legacy.json");
        using var first = coordinator.Register(new TestMonitor(new SelfDiagnosticsOptions { MinimumLevel = LogLevel.Debug }));
        using var latest = coordinator.Register(new TestMonitor(new SelfDiagnosticsOptions { MinimumLevel = LogLevel.Error }));

        coordinator.SetLegacyConfiguration(null, null);
        coordinator.SetLegacyConfiguration(FileOptions("recreated-legacy"), "recreated-legacy.json");

        Assert.Collection(
            applied,
            configuration => Assert.Equal("legacy", configuration.LogDirectory),
            configuration =>
            {
                Assert.False(configuration.HasConfiguredSink, "Expected the configuration to not have a configured sink.");
                Assert.Equal(LogLevel.Error, configuration.MinimumLevel);
            },
            configuration => Assert.Equal("recreated-legacy", configuration.LogDirectory));
    }

    [Fact]
    public void LegacyCleared_WithoutProvidersAppliesDisabled()
    {
        var applied = new List<Configuration>();
        using var coordinator = new SelfDiagnosticsConfigurationCoordinator(applied.Add);
        coordinator.SetLegacyConfiguration(FileOptions("legacy"), "legacy.json");

        coordinator.SetLegacyConfiguration(null, null);

        Assert.Equal(2, applied.Count);
        Assert.Same(Configuration.Disabled, applied[1]);
    }

    [Fact]
    public void NewestProviderWithSink_WinsOverLegacyAndSilentProviders()
    {
        var applied = new List<Configuration>();
        using var coordinator = new SelfDiagnosticsConfigurationCoordinator(applied.Add);
        coordinator.SetLegacyConfiguration(FileOptions("legacy"), "legacy.json");
        using var first = coordinator.Register(FileMonitor("first"));
        using var second = coordinator.Register(FileMonitor("second"));
        using var silent = coordinator.Register(new TestMonitor(new SelfDiagnosticsOptions()));

        Assert.Equal("second", applied[applied.Count - 1].LogDirectory);
        second.Dispose();
        Assert.Equal("first", applied[applied.Count - 1].LogDirectory);
        first.Dispose();
        Assert.Equal("legacy", applied[applied.Count - 1].LogDirectory);
    }

    [Fact]
    public void ActiveProviderReload_WithSameOptionsInstance_AppliesNewSnapshot()
    {
        var applied = new List<Configuration>();
        using var coordinator = new SelfDiagnosticsConfigurationCoordinator(applied.Add);
        var options = FileOptions("provider");
        var monitor = new TestMonitor(options);
        using var provider = coordinator.Register(monitor);
        var initial = Assert.Single(applied);

        monitor.Set(options);

        Assert.Equal(2, applied.Count);
        Assert.NotSame(initial, applied[1]);
        Assert.Equal("provider", applied[1].LogDirectory);
    }

    [Fact]
    public void InactiveProviderReloadAndDisposal_DoNotReapplyActiveSnapshot()
    {
        var applied = new List<Configuration>();
        using var coordinator = new SelfDiagnosticsConfigurationCoordinator(applied.Add);
        var monitor = FileMonitor("first");
        using var first = coordinator.Register(monitor);
        using var active = coordinator.Register(FileMonitor("active"));
        var activeConfiguration = applied[1];

        monitor.Set(FileOptions("updated-first"));
        first.Dispose();

        Assert.Equal(2, applied.Count);
        Assert.Same(activeConfiguration, applied[1]);
        Assert.Equal("active", applied[1].LogDirectory);
    }

    [Fact]
    public void ProviderWithSinkAndNoneLevel_StillOwnsConfiguration()
    {
        var applied = new List<Configuration>();
        var reports = 0;
        using var coordinator = new SelfDiagnosticsConfigurationCoordinator(applied.Add, _ => reports++);
        coordinator.SetLegacyConfiguration(FileOptions("legacy"), "legacy.json");

        using var provider = coordinator.Register(new TestMonitor(
            new SelfDiagnosticsOptions { LogToStdout = true, MinimumLevel = LogLevel.None }));

        Assert.True(applied[applied.Count - 1].LogToStdout, "Expected the provider to log to stdout.");
        Assert.Equal(LogLevel.None, applied[applied.Count - 1].EffectiveLevel);
        Assert.Equal(1, reports);
    }

    [Fact]
    public void ProviderLosesSink_HandsOwnershipBackToLegacy()
    {
        var applied = new List<Configuration>();
        var reports = 0;
        using var coordinator = new SelfDiagnosticsConfigurationCoordinator(applied.Add, _ => reports++);
        coordinator.SetLegacyConfiguration(FileOptions("legacy"), "legacy.json");
        var monitor = FileMonitor("provider");
        using var provider = coordinator.Register(monitor);

        monitor.Set(new SelfDiagnosticsOptions());
        Assert.Equal("legacy", applied[applied.Count - 1].LogDirectory);
        monitor.Set(new SelfDiagnosticsOptions { LogDirectory = "provider-again" });
        Assert.Equal("provider-again", applied[applied.Count - 1].LogDirectory);
        Assert.Equal(2, reports);
    }

    [Fact]
    public void LegacyClearedWhileIgnored_ResetsIgnoredReporting()
    {
        var applied = new List<Configuration>();
        var ignoredPaths = new List<string>();
        using var coordinator = new SelfDiagnosticsConfigurationCoordinator(applied.Add, ignoredPaths.Add);
        using var provider = coordinator.Register(FileMonitor("provider"));

        coordinator.SetLegacyConfiguration(FileOptions("legacy"), "legacy.json");
        coordinator.SetLegacyConfiguration(null, null);
        coordinator.SetLegacyConfiguration(FileOptions("legacy-again"), "legacy-again.json");

        Assert.Single(applied);
        Assert.Equal(["legacy.json", "legacy-again.json"], ignoredPaths);
    }

    [Fact]
    public void IgnoredCallback_UsesPathCapturedBeforeConfigurationApplication()
    {
        SelfDiagnosticsConfigurationCoordinator? coordinator = null;
        var ignoredPaths = new List<string>();
        coordinator = new SelfDiagnosticsConfigurationCoordinator(
            configuration =>
            {
                if (configuration.LogToStdout)
                {
                    coordinator!.SetLegacyConfiguration(FileOptions("updated-legacy"), "updated-legacy.json");
                }
            },
            ignoredPaths.Add);

        using (coordinator)
        {
            coordinator.SetLegacyConfiguration(FileOptions("legacy"), "legacy.json");
            using var provider = coordinator.Register(new TestMonitor(new SelfDiagnosticsOptions { LogToStdout = true }));

            Assert.Equal("legacy.json", Assert.Single(ignoredPaths));
        }
    }

    [Fact]
    public void IgnoredCallback_RunsOutsideCoordinatorLock()
    {
        SelfDiagnosticsConfigurationCoordinator? coordinator = null;
        var reports = 0;
        coordinator = new SelfDiagnosticsConfigurationCoordinator(_ => { }, path =>
        {
            var reentry = Task.Run(() => coordinator!.SetLegacyConfiguration(FileOptions("from-another-thread"), "another.json"));
            Assert.True(reentry.Wait(TimeSpan.FromSeconds(5)), "Ignored callback held the coordinator lock.");
            Assert.Equal("legacy.json", path);
            reports++;
        });

        using (coordinator)
        using (coordinator.Register(FileMonitor("provider")))
        {
            coordinator.SetLegacyConfiguration(FileOptions("legacy"), "legacy.json");
            Assert.Equal(1, reports);
        }
    }

    [Fact]
    public void Dispose_MakesLegacyAndProviderUpdatesNoOps()
    {
        var applied = new List<Configuration>();
        var reports = 0;
        var coordinator = new SelfDiagnosticsConfigurationCoordinator(applied.Add, _ => reports++);
        var providerMonitor = FileMonitor("provider");
        coordinator.SetLegacyConfiguration(FileOptions("legacy"), "legacy.json");
        using var provider = coordinator.Register(providerMonitor);
        var count = applied.Count;

        coordinator.Dispose();
        coordinator.SetLegacyConfiguration(FileOptions("ignored"), "ignored.json");
        coordinator.SetLegacyConfiguration(null, null);
        providerMonitor.Set(new SelfDiagnosticsOptions());
        provider.Dispose();

        Assert.Equal(count, applied.Count);
        Assert.Equal(1, reports);
        Assert.Equal(0, providerMonitor.SubscriptionCount);
        Assert.Equal(1, providerMonitor.SubscriptionDisposeCount);
        Assert.Throws<ObjectDisposedException>(() => coordinator.Register(providerMonitor));
        Assert.Equal(0, providerMonitor.SubscriptionCount);
        Assert.Equal(2, providerMonitor.SubscriptionDisposeCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RepeatedLeaseAndCoordinatorDisposal_UnsubscribesOnce(bool disposeCoordinatorFirst)
    {
        var applied = new List<Configuration>();
        using var coordinator = new SelfDiagnosticsConfigurationCoordinator(applied.Add);
        var monitor = FileMonitor("provider");
        using var provider = coordinator.Register(monitor);

        if (disposeCoordinatorFirst)
        {
            coordinator.Dispose();
        }

        provider.Dispose();
        provider.Dispose();
        coordinator.Dispose();

        Assert.Equal(0, monitor.SubscriptionCount);
        Assert.Equal(1, monitor.SubscriptionDisposeCount);
    }

    [Fact]
    public async Task ConcurrentLeaseAndCoordinatorDisposal_UnsubscribesOnce()
    {
        using var coordinator = new SelfDiagnosticsConfigurationCoordinator(_ => { });
        var monitor = FileMonitor("provider");
        using var provider = coordinator.Register(monitor);
        using var start = new ManualResetEventSlim();

        void DisposeBoth(int i)
        {
            start.Wait();
            if (i % 2 == 0)
            {
                coordinator.Dispose();
            }
            else
            {
                provider.Dispose();
            }
        }

        var work = Task.Run(() => Parallel.For(0, 20, DisposeBoth), TestContext.Current.CancellationToken);
        start.Set();
        await work;

        Assert.Equal(0, monitor.SubscriptionCount);
        Assert.Equal(1, monitor.SubscriptionDisposeCount);
    }

    [Fact]
    public void RegisterAfterDispose_UnsubscribesOutsideCoordinatorLock()
    {
        using var coordinator = new SelfDiagnosticsConfigurationCoordinator(_ => { });
        coordinator.Dispose();
        var monitor = FileMonitor("provider");
        monitor.OnSubscriptionDisposed = () =>
        {
            var reentry = Task.Run(() => coordinator.SetLegacyConfiguration(null, null));
            Assert.True(reentry.Wait(TimeSpan.FromSeconds(5)), "Subscription disposal held the coordinator lock.");
        };

        Assert.Throws<ObjectDisposedException>(() => coordinator.Register(monitor));
        Assert.Equal(0, monitor.SubscriptionCount);
        Assert.Equal(1, monitor.SubscriptionDisposeCount);
    }

    [Fact]
    public async Task ConcurrentLegacyUpdatesAndProviderRegistrations_DoNotDeadlockOrLeakRegistrations()
    {
        var applied = new List<Configuration>();
        var reports = 0;

        using var coordinator = new SelfDiagnosticsConfigurationCoordinator(
            applied.Add, // Invoked under the coordinator lock, so the list needs no extra synchronization.
            _ => Interlocked.Increment(ref reports));

        void Operate(int i)
        {
            if (i % 2 == 0)
            {
                coordinator.SetLegacyConfiguration(i % 4 == 0 ? null : FileOptions($"legacy-{i}"), $"legacy-{i}.json");
            }
            else
            {
                using var provider = coordinator.Register(FileMonitor($"provider-{i}"));
            }
        }

        var work = Task.Run(() => Parallel.For(0, 200, Operate), TestContext.Current.CancellationToken);

        var completed = await Task.WhenAny(work, Task.Delay(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));
        Assert.True(completed == work, "Concurrent coordinator operations deadlocked.");
        await work;
        coordinator.SetLegacyConfiguration(FileOptions("final"), "final.json");

        // A provider registration leaked by a race would still own the configuration here.
        Assert.Equal("final", applied[applied.Count - 1].LogDirectory);
    }

    private static SelfDiagnosticsOptions FileOptions(string directory)
        => new() { LogDirectory = directory };

    private static TestMonitor FileMonitor(string directory)
        => new(FileOptions(directory));

    private sealed class TestMonitor(SelfDiagnosticsOptions options) : IOptionsMonitor<SelfDiagnosticsOptions>
    {
        private Action<SelfDiagnosticsOptions, string?>? listeners;
        private int subscriptionDisposeCount;

        public SelfDiagnosticsOptions CurrentValue { get; private set; } = options;

        internal int SubscriptionCount => this.listeners?.GetInvocationList().Length ?? 0;

        internal int SubscriptionDisposeCount => this.subscriptionDisposeCount;

        internal Action? OnSubscriptionDisposed { get; set; }

        public SelfDiagnosticsOptions Get(string? name) => this.CurrentValue;

        public IDisposable OnChange(Action<SelfDiagnosticsOptions, string?> listener)
        {
            this.listeners += listener;
            return new Subscription(() =>
            {
                Interlocked.Increment(ref this.subscriptionDisposeCount);
                this.listeners -= listener;
                this.OnSubscriptionDisposed?.Invoke();
            });
        }

        internal void Set(SelfDiagnosticsOptions value)
        {
            this.CurrentValue = value;
            this.listeners?.Invoke(value, Options.DefaultName);
        }

        private sealed class Subscription(Action unsubscribe) : IDisposable
        {
            public void Dispose() => unsubscribe();
        }
    }
}

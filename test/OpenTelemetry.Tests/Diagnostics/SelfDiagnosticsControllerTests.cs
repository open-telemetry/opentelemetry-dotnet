// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry.Internal;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.SelfDiagnostics;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Tests.Diagnostics;

public sealed class SelfDiagnosticsControllerTests : IDisposable
{
    private readonly List<string> directories = [];

    public void Dispose()
    {
        foreach (var directory in this.directories)
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
            catch
            {
                // Best-effort cleanup; a sink may still hold a handle on a failed test.
            }
        }
    }

    [Fact]
    public void DefaultOptions_CreateNoLoggingStack()
    {
        // Silent by default: with no sink configured nothing is constructed at all, so the SDK
        // pays neither for a pump thread nor for an EventSource subscription.
        using var controller = new SelfDiagnosticsController();
        using var registration = controller.Register(Monitor(new SelfDiagnosticsOptions()));

        Assert.Null(controller.Logger);
    }

    [Fact]
    public void LevelWithoutSink_CreatesNoLoggingStack()
    {
        // A level on its own is not a request for output.
        using var controller = new SelfDiagnosticsController();
        using var registration = controller.Register(
            Monitor(new SelfDiagnosticsOptions { MinimumLevel = LogLevel.Trace }));

        Assert.Null(controller.Logger);
    }

    [Fact]
    public void SinkWithNoneLevel_CreatesNoLoggingStack()
    {
        // A sink with everything filtered out is also not a request for output.
        using var controller = new SelfDiagnosticsController();
        using var registration = controller.Register(
            Monitor(new SelfDiagnosticsOptions
            {
                MinimumLevel = LogLevel.None,
                LogToStdout = true,
            }));

        Assert.Null(controller.Logger);
    }

    [Fact]
    public void ConfiguredSink_BuildsTheStackAndWritesToDisk()
    {
        var directory = this.CreateDirectory();

        using var controller = new SelfDiagnosticsController();
        using var registration = controller.Register(Monitor(FileOptions(directory)));

        Assert.NotNull(controller.Logger);

        controller.Logger!.Log(LogLevel.Warning, default, "hello from the controller", null, static (m, _) => m);

        Assert.True(
            SelfDiagnosticsTestHelpers.WaitUntil(() => ReadLogs(directory).Contains("hello from the controller", StringComparison.Ordinal)),
            "the entry never reached the file sink");

        // Every file is self-contained, so the preamble is present too.
        Assert.Contains("=== end preamble ===", ReadLogs(directory), StringComparison.Ordinal);
    }

    [Fact]
    public void SdkEventSourceEvents_ReachTheFileSink()
    {
        // The end-to-end path: the listener is constructed eagerly precisely so that events from
        // sources registered before the controller existed are still captured.
        var directory = this.CreateDirectory();

        using var controller = new SelfDiagnosticsController();
        using var registration = controller.Register(Monitor(FileOptions(directory)));

        ControllerTestEventSource.Log.Warn("event source reached the sink");

        Assert.True(
            SelfDiagnosticsTestHelpers.WaitUntil(() => ReadLogs(directory).Contains("event source reached the sink", StringComparison.Ordinal)),
            "the EventSource event never reached the file sink");
    }

    [Fact]
    public void SecondRegistrationWithoutASink_DoesNotDisableTheFirst()
    {
        var directory = this.CreateDirectory();

        using var controller = new SelfDiagnosticsController();
        using var owner = controller.Register(Monitor(FileOptions(directory)));

        Assert.True(
            SelfDiagnosticsTestHelpers.WaitUntil(() => ReadLogs(directory).Length > 0),
            "the file sink never opened");

        using var interloper = controller.Register(Monitor(new SelfDiagnosticsOptions()));

        controller.Logger!.Log(LogLevel.Warning, default, "still logging", null, static (m, _) => m);

        Assert.True(
            SelfDiagnosticsTestHelpers.WaitUntil(() => ReadLogs(directory).Contains("still logging", StringComparison.Ordinal)),
            "a registration with no sink took ownership and silenced the configured one");
    }

    [Fact]
    public void RegistrationWithASink_TakesOwnershipFromOneWithout()
    {
        var directory = this.CreateDirectory();

        using var controller = new SelfDiagnosticsController();
        using var silent = controller.Register(Monitor(new SelfDiagnosticsOptions()));

        Assert.Null(controller.Logger);

        using var configured = controller.Register(Monitor(FileOptions(directory)));

        Assert.NotNull(controller.Logger);
        controller.Logger!.Log(LogLevel.Warning, default, "took over", null, static (m, _) => m);

        Assert.True(
            SelfDiagnosticsTestHelpers.WaitUntil(() => ReadLogs(directory).Contains("took over", StringComparison.Ordinal)),
            "the registration that configured a sink did not take ownership");
    }

    [Fact]
    public void DisposingTheOnlyRegistration_StopsOutput()
    {
        var directory = this.CreateDirectory();

        using var controller = new SelfDiagnosticsController();
        var registration = controller.Register(Monitor(FileOptions(directory)));

        controller.Logger!.Log(LogLevel.Warning, default, "before dispose", null, static (m, _) => m);
        Assert.True(
            SelfDiagnosticsTestHelpers.WaitUntil(() => ReadLogs(directory).Contains("before dispose", StringComparison.Ordinal)),
            "the entry never reached the file sink");

        registration.Dispose();

        // The stack is retained (its EventSource subscription is what allows reconfiguration
        // later) but the sink set is dropped, so nothing further is written.
        Assert.True(
            SelfDiagnosticsTestHelpers.WaitUntil(() => !controller.Logger!.IsEnabled(LogLevel.Warning)),
            "diagnostics remained enabled after the last registration was disposed");

        // IsEnabled is false so Log() is a no-op at the logger level; the entry never enters the
        // pump, so DoesNotContain is deterministic without any sleep.
        controller.Logger!.Log(LogLevel.Warning, default, "after dispose", null, static (m, _) => m);

        Assert.DoesNotContain("after dispose", ReadLogs(directory), StringComparison.Ordinal);
    }

    [Fact]
    public void OptionsReload_SwitchesTheSinkAtRuntime()
    {
        var directory = this.CreateDirectory();

        using var controller = new SelfDiagnosticsController();
        var monitor = Monitor(new SelfDiagnosticsOptions());
        using var registration = controller.Register(monitor);

        Assert.Null(controller.Logger);

        monitor.Set(FileOptions(directory));

        Assert.NotNull(controller.Logger);
        controller.Logger!.Log(LogLevel.Warning, default, "enabled by reload", null, static (m, _) => m);

        Assert.True(
            SelfDiagnosticsTestHelpers.WaitUntil(() => ReadLogs(directory).Contains("enabled by reload", StringComparison.Ordinal)),
            "the reloaded configuration did not enable the file sink");
    }

    [Fact]
    public void OptionsReload_FileReplacementEnforcesRetentionAfterHandover()
    {
        var directory = this.CreateDirectory();
        var initial = FileOptions(directory);
        initial.FileSizeLimitKilobytes = 1_024;
        initial.MaxRetainedFiles = 1;

        using var controller = new SelfDiagnosticsController();
        var monitor = Monitor(initial);
        using var registration = controller.Register(monitor);

        Assert.True(
            SelfDiagnosticsTestHelpers.WaitUntil(
                () => Directory.GetFiles(directory, "*.log").Any(file => file.EndsWith("-1.log", StringComparison.Ordinal))),
            "the initial file sink never opened");

        var replacement = FileOptions(directory);
        replacement.FileSizeLimitKilobytes = 2_048;
        replacement.MaxRetainedFiles = 1;
        monitor.Set(replacement);

        Assert.True(
            SelfDiagnosticsTestHelpers.WaitUntil(
                () => Directory.GetFiles(directory, "*.log").Any(file => file.EndsWith("-2.log", StringComparison.Ordinal))),
            "the replacement file sink never opened");
        Assert.True(
            SelfDiagnosticsTestHelpers.WaitUntil(() => Directory.GetFiles(directory, "*.log").Length == 1),
            "the outgoing file remained outside the steady-state retention limit");
    }

    [Fact]
    public async Task OptionsReload_RacingRegistrationDisposal_DoesNotReenableOrThrow()
    {
        var directory = this.CreateDirectory();
        using var controller = new SelfDiagnosticsController();
        var monitor = Monitor(FileOptions(directory));
        var registration = controller.Register(monitor);
        using var start = new ManualResetEventSlim();

        var reloadTask = Task.Run(() =>
        {
            start.Wait();
            for (var i = 0; i < 100; i++)
            {
                monitor.Set(i % 2 == 0 ? FileOptions(directory) : new SelfDiagnosticsOptions());
            }
        });
        var disposeTask = Task.Run(() =>
        {
            start.Wait();
            registration.Dispose();
        });

        start.Set();
        await Task.WhenAll(reloadTask, disposeTask);

        Assert.True(
            SelfDiagnosticsTestHelpers.WaitUntil(() => controller.Logger?.IsEnabled(LogLevel.Warning) != true),
            "a racing options callback re-enabled diagnostics after its registration was disposed");
    }

    [Theory]
    [InlineData("traces")]
    [InlineData("metrics")]
    [InlineData("logs")]
    public void ProviderBuilders_RegisterAndReleaseSelfDiagnostics(string providerKind)
    {
        var directory = this.CreateDirectory();
        var beforeDispose = $"{providerKind} provider registered diagnostics";

        using (var provider = BuildProvider(providerKind, directory))
        {
            ProviderIntegrationEventSource.Log.Warn(beforeDispose);

            Assert.True(
                SelfDiagnosticsTestHelpers.WaitUntil(() => ReadLogs(directory).Contains(beforeDispose, StringComparison.Ordinal)),
                $"the {providerKind} provider did not register its self-diagnostics options");
        }

        // provider.Dispose() synchronously tears down the EventListener and joins the pump thread,
        // so by the time control reaches here the subscription is gone and the event below fires
        // into a disposed listener. DoesNotContain is deterministic without any sleep.
        var afterDispose = $"{providerKind} provider was disposed";
        ProviderIntegrationEventSource.Log.Warn(afterDispose);

        Assert.DoesNotContain(afterDispose, ReadLogs(directory), StringComparison.Ordinal);
    }

    [Fact]
    public void ProviderOwnership_DisposalRestoresThePreviousConfiguredProvider()
    {
        var firstDirectory = this.CreateDirectory();
        var secondDirectory = this.CreateDirectory();

        using var first = BuildProvider("traces", firstDirectory);
        using var second = BuildProvider("metrics", secondDirectory);

        ProviderIntegrationEventSource.Log.Warn("owned by the second provider");
        Assert.True(
            SelfDiagnosticsTestHelpers.WaitUntil(
                () => ReadLogs(secondDirectory).Contains("owned by the second provider", StringComparison.Ordinal)),
            "the most recently built configured provider did not take ownership");
        Assert.DoesNotContain("owned by the second provider", ReadLogs(firstDirectory), StringComparison.Ordinal);

        second.Dispose();
        ProviderIntegrationEventSource.Log.Warn("restored to the first provider");

        Assert.True(
            SelfDiagnosticsTestHelpers.WaitUntil(
                () => ReadLogs(firstDirectory).Contains("restored to the first provider", StringComparison.Ordinal)),
            "disposing the active provider did not restore the previous configured provider");
    }

    [Fact]
    public void RegisterAfterDispose_Throws()
    {
        var controller = new SelfDiagnosticsController();
        controller.Dispose();

        Assert.Throws<ObjectDisposedException>(
            () => controller.Register(Monitor(new SelfDiagnosticsOptions())));
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var controller = new SelfDiagnosticsController();
        using var registration = controller.Register(
            Monitor(FileOptions(this.CreateDirectory())));

        controller.Dispose();
        controller.Dispose();

        Assert.Null(controller.Logger);
    }

    [Fact]
    public void DisposingRegistrationAfterController_DoesNotThrow()
    {
        // Teardown order is not guaranteed: a DI container may dispose the controller's owner
        // before the provider that holds the lease.
        var controller = new SelfDiagnosticsController();
        var registration = controller.Register(Monitor(new SelfDiagnosticsOptions()));

        controller.Dispose();
        registration.Dispose();
    }

    private static SelfDiagnosticsOptions FileOptions(string directory)
        => new()
        {
            MinimumLevel = LogLevel.Warning,
            LogDirectory = directory,

            // Keep the preamble small and deterministic; these tests are about the wiring.
            EnvironmentVariables = EnvironmentVariableLogMode.None,
        };

    private static TestMonitor Monitor(SelfDiagnosticsOptions options) => new(options);

    private static IDisposable BuildProvider(string providerKind, string directory)
    {
        void Configure(IServiceCollection services)
            => services.Configure<SelfDiagnosticsOptions>(options =>
            {
                options.MinimumLevel = LogLevel.Warning;
                options.LogDirectory = directory;
                options.EnvironmentVariables = EnvironmentVariableLogMode.None;
            });

        return providerKind switch
        {
            "traces" => Sdk.CreateTracerProviderBuilder().ConfigureServices(Configure).Build(),
            "metrics" => Sdk.CreateMeterProviderBuilder().ConfigureServices(Configure).Build(),
            "logs" => Sdk.CreateLoggerProviderBuilder().ConfigureServices(Configure).Build(),
            _ => throw new ArgumentOutOfRangeException(nameof(providerKind)),
        };
    }

    private static string ReadLogs(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder();

        foreach (var file in Directory.GetFiles(directory, "*.log"))
        {
            try
            {
                builder.AppendLine(SelfDiagnosticsTestHelpers.ReadAllTextShared(file));
            }
            catch (IOException)
            {
                // The sink may be mid-write; the caller polls.
            }
        }

        return builder.ToString();
    }

    private string CreateDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            $"otel-controller-{Guid.NewGuid():N}");

        Directory.CreateDirectory(directory);
        this.directories.Add(directory);
        return directory;
    }

    [EventSource(Name = "OpenTelemetry-SelfDiagnosticsProviderIntegrationTests")]
    private sealed class ProviderIntegrationEventSource : EventSource
    {
        internal static readonly ProviderIntegrationEventSource Log = new();

        [Event(1, Message = "{0}", Level = EventLevel.Warning)]
        public void Warn(string message) => this.WriteEvent(1, message);
    }

    [EventSource(Name = "OpenTelemetry-SelfDiagnosticsControllerTests")]
    private sealed class ControllerTestEventSource : EventSource
    {
        internal static readonly ControllerTestEventSource Log = new();

        [Event(1, Message = "{0}", Level = EventLevel.Warning)]
        public void Warn(string message) => this.WriteEvent(1, message);
    }

    private sealed class TestMonitor : IOptionsMonitor<SelfDiagnosticsOptions>
    {
        private readonly List<Action<SelfDiagnosticsOptions, string?>> listeners = [];

        internal TestMonitor(SelfDiagnosticsOptions current)
        {
            this.CurrentValue = current;
        }

        public SelfDiagnosticsOptions CurrentValue { get; private set; }

        public SelfDiagnosticsOptions Get(string? name) => this.CurrentValue;

        public IDisposable OnChange(Action<SelfDiagnosticsOptions, string?> listener)
        {
            lock (this.listeners)
            {
                this.listeners.Add(listener);
            }

            return new Subscription(this, listener);
        }

        internal void Set(SelfDiagnosticsOptions value)
        {
            this.CurrentValue = value;

            Action<SelfDiagnosticsOptions, string?>[] snapshot;
            lock (this.listeners)
            {
                snapshot = [.. this.listeners];
            }

            foreach (var listener in snapshot)
            {
                listener(value, null);
            }
        }

        private sealed class Subscription : IDisposable
        {
            private readonly TestMonitor owner;
            private readonly Action<SelfDiagnosticsOptions, string?> listener;

            internal Subscription(TestMonitor owner, Action<SelfDiagnosticsOptions, string?> listener)
            {
                this.owner = owner;
                this.listener = listener;
            }

            public void Dispose()
            {
                lock (this.owner.listeners)
                {
                    this.owner.listeners.Remove(this.listener);
                }
            }
        }
    }
}

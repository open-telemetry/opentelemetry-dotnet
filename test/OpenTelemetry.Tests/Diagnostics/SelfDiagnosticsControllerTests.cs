// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry.Internal;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.SelfDiagnostics;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Tests.Diagnostics;

[Collection(EnvVarsCollectionDefinition.Name)]
public sealed class SelfDiagnosticsControllerTests : IDisposable
{
    private readonly List<TemporaryDirectory> directories = [];

    public void Dispose()
    {
        foreach (var directory in this.directories)
        {
            directory.Dispose();
        }
    }

    [Fact]
    public void LegacyConfiguration_ActivatesImmediatelyAndWritesSdkEventsAndPreamble()
    {
        var directory = this.CreateDirectory();
        using var controller = new SelfDiagnosticsController();
        using var source = this.CreateLegacySource(controller, directory);

        Assert.NotNull(controller.Logger);
        OpenTelemetrySdkEventSource.Log.ObservableInstrumentCallbackException("legacy JSON reached the shared pipeline");

        AssertLogsContain(
            directory,
            "legacy JSON reached the shared pipeline",
            "The legacy configuration did not capture the SDK event.");
        controller.Dispose();

        var file = Assert.Single(Directory.GetFiles(directory, "otel-dotnet-*.log"));
        var text = File.ReadAllText(file);

        Assert.Contains("=== end preamble ===", text, StringComparison.Ordinal);
        Assert.Contains("Self-diagnostics was enabled by the legacy OTEL_DIAGNOSTICS.json", text, StringComparison.Ordinal);
        Assert.Contains(source.ConfigFilePath!, text, StringComparison.Ordinal);
        Assert.Contains("LogLevel 'Warning' was applied as MinimumLevel Warning", text, StringComparison.Ordinal);
        Assert.Contains("[Warning]", text, StringComparison.Ordinal);
        Assert.Contains("EventName: ObservableInstrumentCallbackException", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(LogLevel.Warning)]
    [InlineData(LogLevel.None)]
    public void ProviderWithoutSink_DoesNotDisplaceLegacyConfiguration(LogLevel minimumLevel)
    {
        var directory = this.CreateDirectory();
        using var controller = new SelfDiagnosticsController();
        using var source = this.CreateLegacySource(controller, directory);
        using var provider = controller.Register(Monitor(new SelfDiagnosticsOptions { MinimumLevel = minimumLevel }));

        ControllerTestEventSource.Log.Warn("captured after provider registration");

        AssertLogsContain(
            directory,
            "captured after provider registration",
            "A provider without a sink displaced the legacy configuration.");
    }

    [Fact]
    public void ProviderWithSink_RegisteredFirst_ReportsIgnoredFileWithoutCreatingLegacyLog()
    {
        var legacyDirectory = this.CreateDirectory();
        var providerDirectory = this.CreateDirectory();
        var configFilePath = Path.Combine(this.CreateDirectory(), SelfDiagnosticsConfigParser.ConfigFileName);
        var ignoredPaths = new List<string>();

        WriteLegacyConfiguration(configFilePath, legacyDirectory);

        using var controller = new SelfDiagnosticsController(ignoredPaths.Add);
        using var provider = controller.Register(Monitor(FileOptions(providerDirectory)));
        using var source = new SelfDiagnosticsLegacyConfigSource(
            new SelfDiagnosticsConfigParser(() => configFilePath), Timeout.InfiniteTimeSpan, controller.SetLegacyConfiguration);

        AssertLogsContain(
            providerDirectory,
            "=== end preamble ===",
            "The provider file sink did not open.");
        Assert.Equal(configFilePath, Assert.Single(ignoredPaths));
        Assert.Empty(Directory.GetFiles(legacyDirectory));
    }

    [Fact]
    public void ProviderWithSink_TakesOverFromActiveLegacyConfiguration()
    {
        var legacyDirectory = this.CreateDirectory();
        var providerDirectory = this.CreateDirectory();
        var ignoredPaths = new List<string>();

        using var controller = new SelfDiagnosticsController(ignoredPaths.Add);
        using var source = this.CreateLegacySource(controller, legacyDirectory);

        AssertLogsContain(
            legacyDirectory,
            "=== end preamble ===",
            "The legacy file sink did not write its preamble.");

        using var provider = controller.Register(Monitor(FileOptions(providerDirectory)));
        ControllerTestEventSource.Log.Warn("after provider takeover");

        AssertLogsContain(
            providerDirectory,
            "after provider takeover",
            "The provider did not take ownership from the legacy configuration.");
        Assert.DoesNotContain("after provider takeover", ReadLogs(legacyDirectory), StringComparison.Ordinal);
        Assert.Equal(source.ConfigFilePath, Assert.Single(ignoredPaths));
    }

    [Theory]
    [InlineData("Warning")]
    [InlineData("Error")]
    [InlineData("Critical")]
    public void LegacyConfiguration_CapturesLogAlwaysEventsAtRestrictiveLevels(string level)
    {
        var directory = this.CreateDirectory();
        var configFilePath = Path.Combine(this.CreateDirectory(), SelfDiagnosticsConfigParser.ConfigFileName);
        WriteLegacyConfiguration(configFilePath, directory, level);

        using var controller = new SelfDiagnosticsController();
        using var source = new SelfDiagnosticsLegacyConfigSource(
            new SelfDiagnosticsConfigParser(() => configFilePath), Timeout.InfiniteTimeSpan, controller.SetLegacyConfiguration);

        ControllerTestEventSource.Log.Always("log always under legacy");
        ControllerTestEventSource.Log.Critical("critical marker under legacy");

        AssertLogsContain(
            directory,
            "critical marker under legacy",
            "The legacy configuration did not capture the Critical marker.");
        controller.Dispose();

        Assert.Contains("log always under legacy", ReadLogs(directory), StringComparison.Ordinal);
    }

    [Fact]
    public void NoneLevelProviderWithSink_TakesOverFromActiveLegacyConfiguration_WithoutOutput()
    {
        var legacyDirectory = this.CreateDirectory();
        var providerDirectory = this.CreateDirectory();
        var ignoredPaths = new List<string>();

        using var controller = new SelfDiagnosticsController(ignoredPaths.Add);
        using var source = this.CreateLegacySource(controller, legacyDirectory);

        AssertLogsContain(
            legacyDirectory,
            "=== end preamble ===",
            "The legacy file sink did not write its preamble.");

        var options = FileOptions(providerDirectory);
        options.MinimumLevel = LogLevel.None;
        using var provider = controller.Register(Monitor(options));

        Assert.False(controller.Logger!.IsEnabled(LogLevel.Critical), "The provider should not be enabled for Critical level after setting it to None.");
        ControllerTestEventSource.Log.Always("after none takeover");
        ControllerTestEventSource.Log.Critical("after none takeover");

        // Disposal joins the pump, so any sink the takeover created has been opened by now.
        controller.Dispose();

        Assert.Empty(Directory.GetFiles(providerDirectory));
        Assert.DoesNotContain("after none takeover", ReadLogs(legacyDirectory), StringComparison.Ordinal);
        Assert.Equal(source.ConfigFilePath, Assert.Single(ignoredPaths));
    }

    [Fact]
    public void NoneLevelProviderWithSink_ReEnablingAfterLegacyTakeover_RecreatesItsSink()
    {
        var legacyDirectory = this.CreateDirectory();
        var providerDirectory = this.CreateDirectory();

        using var controller = new SelfDiagnosticsController();
        using var source = this.CreateLegacySource(controller, legacyDirectory);

        var disabled = FileOptions(providerDirectory);
        disabled.MinimumLevel = LogLevel.None;
        var monitor = Monitor(disabled);
        using var provider = controller.Register(monitor);

        ControllerTestEventSource.Log.Critical("while provider is disabled");

        monitor.Set(FileOptions(providerDirectory));
        ControllerTestEventSource.Log.Critical("after provider re-enabled");

        AssertLogsContain(
            providerDirectory,
            "after provider re-enabled",
            "Re-enabling the provider level did not recreate its file sink.");
        controller.Dispose();

        var text = ReadLogs(providerDirectory) + ReadLogs(legacyDirectory);
        Assert.Single(Directory.GetFiles(providerDirectory));
        Assert.DoesNotContain("while provider is disabled", text, StringComparison.Ordinal);
    }

    [Fact]
    public void LegacyUpdatesWhileIgnored_AreAppliedWhenProviderDisablesItsSink()
    {
        var legacyDirectory = this.CreateDirectory();
        var updatedDirectory = this.CreateDirectory();
        var providerDirectory = this.CreateDirectory();
        using var controller = new SelfDiagnosticsController();
        var monitor = Monitor(FileOptions(providerDirectory));
        using var provider = controller.Register(monitor);
        using var source = this.CreateLegacySource(controller, legacyDirectory);

        WriteLegacyConfiguration(source.ConfigFilePath!, updatedDirectory, "Error");
        Assert.True(source.Refresh());
        Assert.Empty(Directory.GetFiles(legacyDirectory));
        Assert.Empty(Directory.GetFiles(updatedDirectory));

        monitor.Set(new SelfDiagnosticsOptions());
        ControllerTestEventSource.Log.Warn("legacy warning should be filtered");
        OpenTelemetrySdkEventSource.Log.TracerProviderException("legacy error after handover", "test exception");

        AssertLogsContain(
            updatedDirectory,
            "legacy error after handover",
            "Disabling the provider sink did not activate the updated legacy configuration.");
        Assert.DoesNotContain("legacy warning should be filtered", ReadLogs(updatedDirectory), StringComparison.Ordinal);
        Assert.Contains("LogLevel 'Error' was applied as MinimumLevel Error", ReadLogs(updatedDirectory), StringComparison.Ordinal);
    }

    [Fact]
    public void LegacyFileDeletionAndRecreation_DisableAndEnableThePipeline()
    {
        var firstDirectory = this.CreateDirectory();
        var secondDirectory = this.CreateDirectory();
        using var controller = new SelfDiagnosticsController();
        using var source = this.CreateLegacySource(controller, firstDirectory);
        var configFilePath = source.ConfigFilePath!;
        ControllerTestEventSource.Log.Warn("before legacy file deletion");
        AssertLogsContain(
            firstDirectory,
            "before legacy file deletion",
            "The legacy file sink did not capture the event before configuration deletion.");

        File.Delete(configFilePath);
        Assert.True(source.Refresh());
        Assert.False(controller.Logger!.IsEnabled(LogLevel.Warning));
        ControllerTestEventSource.Log.Warn("while legacy file is absent");

        WriteLegacyConfiguration(configFilePath, secondDirectory, "Error");
        Assert.True(source.Refresh());
        ControllerTestEventSource.Log.Warn("warning after legacy file recreation");
        OpenTelemetrySdkEventSource.Log.TracerProviderException("error after legacy file recreation", "test exception");

        AssertLogsContain(
            secondDirectory,
            "error after legacy file recreation",
            "Recreating the legacy configuration did not enable the new file sink.");
        controller.Dispose();
        var text = ReadLogs(firstDirectory) + ReadLogs(secondDirectory);
        Assert.DoesNotContain("while legacy file is absent", text, StringComparison.Ordinal);
        Assert.DoesNotContain("warning after legacy file recreation", text, StringComparison.Ordinal);
    }

    [Fact]
    public void LegacyFileCreatedLater_IsPickedUpByThePollTimer()
    {
        var directory = this.CreateDirectory();
        var configFilePath = Path.Combine(this.CreateDirectory(), SelfDiagnosticsConfigParser.ConfigFileName);
        using var controller = new SelfDiagnosticsController();
        using var source = new SelfDiagnosticsLegacyConfigSource(
            new SelfDiagnosticsConfigParser(() => File.Exists(configFilePath) ? configFilePath : null),
            TimeSpan.FromMilliseconds(20),
            controller.SetLegacyConfiguration);
        Assert.Null(controller.Logger);

        WriteLegacyConfiguration(configFilePath, directory);

        AssertLogsContain(
            directory,
            "=== end preamble ===",
            "The poll timer did not activate the new legacy configuration.");
        Assert.NotNull(controller.Logger);
    }

    [Fact]
    public void LegacyRefreshAfterControllerDisposal_DoesNotReapplyConfiguration()
    {
        var firstDirectory = this.CreateDirectory();
        var secondDirectory = this.CreateDirectory();
        var controller = new SelfDiagnosticsController();
        using var source = this.CreateLegacySource(controller, firstDirectory);
        Assert.NotNull(controller.Logger);

        controller.Dispose();
        WriteLegacyConfiguration(source.ConfigFilePath!, secondDirectory);

        Assert.True(source.Refresh(), "Expected the source to refresh successfully.");
        Assert.Null(controller.Logger);
        Assert.Empty(Directory.GetFiles(secondDirectory));
    }

    [Fact]
    public void SelfDiagnostics_ReportsIgnoredLegacyFileWithItsPath()
    {
        var legacyDirectory = this.CreateDirectory();
        var providerDirectory = this.CreateDirectory();
        var configFilePath = Path.Combine(this.CreateDirectory(), SelfDiagnosticsConfigParser.ConfigFileName);
        WriteLegacyConfiguration(configFilePath, legacyDirectory);

        using var selfDiagnostics = new Internal.SelfDiagnostics(
            new SelfDiagnosticsConfigParser(() => configFilePath), Timeout.InfiniteTimeSpan);
        using var provider = selfDiagnostics.Register(Monitor(FileOptions(providerDirectory)));

        AssertLogsContain(
            providerDirectory,
            "SelfDiagnosticsLegacyConfigIgnored",
            "The provider did not report the ignored legacy configuration.");

        var text = ReadLogs(providerDirectory);

        Assert.Contains(configFilePath, text, StringComparison.Ordinal);
        Assert.Contains("was ignored because self-diagnostics sinks are configured", text, StringComparison.Ordinal);
        Assert.Contains("EventId: 60", text, StringComparison.Ordinal);
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

        AssertLogsContain(
            directory,
            "hello from the controller",
            "The entry never reached the file sink.");

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

        AssertLogsContain(
            directory,
            "event source reached the sink",
            "The EventSource event never reached the file sink.");
    }

    [Fact]
    public void SecondRegistrationWithoutASink_DoesNotDisableTheFirst()
    {
        var directory = this.CreateDirectory();

        using var controller = new SelfDiagnosticsController();
        using var owner = controller.Register(Monitor(FileOptions(directory)));

        Assert.True(
            SelfDiagnosticsTestHelpers.WaitUntil(() => ReadLogs(directory).Length > 0),
            "The file sink never opened.");

        using var interloper = controller.Register(Monitor(new SelfDiagnosticsOptions()));

        controller.Logger!.Log(LogLevel.Warning, default, "still logging", null, static (m, _) => m);

        AssertLogsContain(
            directory,
            "still logging",
            "A registration without a sink silenced the configured sink.");
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

        AssertLogsContain(
            directory,
            "took over",
            "The configured registration did not take ownership.");
    }

    [Fact]
    public void DisposingTheOnlyRegistration_StopsOutput()
    {
        var directory = this.CreateDirectory();

        using var controller = new SelfDiagnosticsController();
        var registration = controller.Register(Monitor(FileOptions(directory)));

        controller.Logger!.Log(LogLevel.Warning, default, "before dispose", null, static (m, _) => m);
        AssertLogsContain(
            directory,
            "before dispose",
            "The entry never reached the file sink.");

        registration.Dispose();

        // The stack is retained (its EventSource subscription is what allows reconfiguration
        // later) but the sink set is dropped, so nothing further is written.
        Assert.True(
            SelfDiagnosticsTestHelpers.WaitUntil(() => !controller.Logger!.IsEnabled(LogLevel.Warning)),
            "Diagnostics remained enabled after the last registration was disposed.");

        // IsEnabled is false so Log() is a no-op at the logger level; the entry never enters the
        // pump, so DoesNotContain is deterministic without any sleep.
        controller.Logger!.Log(LogLevel.Warning, default, "after dispose", null, static (m, _) => m);

        Assert.DoesNotContain("after dispose", ReadLogs(directory), StringComparison.Ordinal);
    }

    [Fact]
    public void AutomaticConfigurationReload_EnablesAndMovesTheFileSink()
    {
        var firstDirectory = this.CreateDirectory();
        var secondDirectory = this.CreateDirectory();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var services = new ServiceCollection();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddOpenTelemetrySharedProviderBuilderServices();

        using var provider = services.BuildServiceProvider();
        using var controller = new SelfDiagnosticsController();
        using var registration = controller.Register(provider.GetRequiredService<IOptionsMonitor<SelfDiagnosticsOptions>>());

        Assert.Null(controller.Logger);

        configuration["OpenTelemetry:SelfDiagnostics:LogDirectory"] = firstDirectory;
        configuration["OpenTelemetry:SelfDiagnostics:MinimumLevel"] = "info";
        configuration.Reload();

        Assert.NotNull(controller.Logger);
        controller.Logger!.Log(LogLevel.Information, default, "first automatic directory", null, static (m, _) => m);
        Assert.True(SelfDiagnosticsTestHelpers.WaitUntil(() => ReadLogs(firstDirectory).Contains("first automatic directory", StringComparison.Ordinal)), "The first automatic directory log entry was not found.");

        configuration["OpenTelemetry:SelfDiagnostics:LogDirectory"] = secondDirectory;
        configuration["OpenTelemetry:SelfDiagnostics:MinimumLevel"] = "Error";
        configuration.Reload();

        Assert.True(SelfDiagnosticsTestHelpers.WaitUntil(() => !controller.Logger.IsEnabled(LogLevel.Warning) && controller.Logger.IsEnabled(LogLevel.Error)));

        controller.Logger.Log(LogLevel.Warning, default, "filtered after automatic reload", null, static (m, _) => m);
        controller.Logger.Log(LogLevel.Error, default, "second automatic directory", null, static (m, _) => m);

        Assert.True(SelfDiagnosticsTestHelpers.WaitUntil(() => ReadLogs(secondDirectory).Contains("second automatic directory", StringComparison.Ordinal)), "The second automatic directory log entry was not found.");
        Assert.DoesNotContain("filtered after automatic reload", ReadLogs(secondDirectory), StringComparison.Ordinal);
        Assert.DoesNotContain("second automatic directory", ReadLogs(firstDirectory), StringComparison.Ordinal);
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

        AssertLogsContain(
            directory,
            "enabled by reload",
            "The reloaded configuration did not enable the file sink.");
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
            "The initial file sink never opened.");

        var replacement = FileOptions(directory);
        replacement.FileSizeLimitKilobytes = 2_048;
        replacement.MaxRetainedFiles = 1;
        monitor.Set(replacement);

        Assert.True(
            SelfDiagnosticsTestHelpers.WaitUntil(
                () => Directory.GetFiles(directory, "*.log").Any(file => file.EndsWith("-2.log", StringComparison.Ordinal))),
            "The replacement file sink never opened.");
        Assert.True(
            SelfDiagnosticsTestHelpers.WaitUntil(() => Directory.GetFiles(directory, "*.log").Length == 1),
            "The outgoing file exceeded the retention limit.");
    }

    [Fact]
    public async Task OptionsReload_RacingRegistrationDisposal_DoesNotReenableOrThrow()
    {
        var directory = this.CreateDirectory();
        using var controller = new SelfDiagnosticsController();
        var monitor = Monitor(FileOptions(directory));
        var registration = controller.Register(monitor);
        using var start = new ManualResetEventSlim();

        var reloadTask = Task.Run(
            () =>
            {
                start.Wait();
                for (var i = 0; i < 100; i++)
                {
                    monitor.Set(i % 2 == 0 ? FileOptions(directory) : new SelfDiagnosticsOptions());
                }
            },
            TestContext.Current.CancellationToken);
        var disposeTask = Task.Run(
            () =>
            {
                start.Wait();
                registration.Dispose();
            },
            TestContext.Current.CancellationToken);

        start.Set();
        await Task.WhenAll(reloadTask, disposeTask);

        Assert.True(
            SelfDiagnosticsTestHelpers.WaitUntil(() => controller.Logger?.IsEnabled(LogLevel.Warning) != true),
            "An options callback re-enabled diagnostics after registration disposal.");
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

            AssertLogsContain(
                directory,
                beforeDispose,
                $"The {providerKind} provider did not register its self-diagnostics options.");
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
        AssertLogsContain(
            secondDirectory,
            "owned by the second provider",
            "The latest configured provider did not take ownership.");
        Assert.DoesNotContain("owned by the second provider", ReadLogs(firstDirectory), StringComparison.Ordinal);

        second.Dispose();
        ProviderIntegrationEventSource.Log.Warn("restored to the first provider");

        AssertLogsContain(
            firstDirectory,
            "restored to the first provider",
            "Disposing the active provider did not restore the previous provider.");
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

    private static void WriteLegacyConfiguration(string path, string directory, string level = "Warning")
    {
#if NET
        var escapedDirectory = directory.Replace("\\", "\\\\", StringComparison.Ordinal);
#else
        var escapedDirectory = directory.Replace("\\", "\\\\");
#endif
        File.WriteAllText(path, $$"""{"LogDirectory":"{{escapedDirectory}}","FileSize":32768,"LogLevel":"{{level}}"}""");
    }

    private static IDisposable BuildProvider(string providerKind, string directory)
    {
        void Configure(IServiceCollection services)
        {
            services.Configure<SelfDiagnosticsOptions>(options =>
            {
                options.MinimumLevel = LogLevel.Warning;
                options.LogDirectory = directory;
                options.EnvironmentVariables = EnvironmentVariableLogMode.None;
            });
        }

        return providerKind switch
        {
            "traces" => Sdk.CreateTracerProviderBuilder().ConfigureServices(Configure).Build(),
            "metrics" => Sdk.CreateMeterProviderBuilder().ConfigureServices(Configure).Build(),
            "logs" => Sdk.CreateLoggerProviderBuilder().ConfigureServices(Configure).Build(),
            _ => throw new ArgumentOutOfRangeException(nameof(providerKind)),
        };
    }

    private static void AssertLogsContain(string directory, string expected, string userMessage) =>
        Assert.True(
            SelfDiagnosticsTestHelpers.WaitUntil(() => ReadLogs(directory).Contains(expected, StringComparison.Ordinal)),
            userMessage);

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
        var directory = new TemporaryDirectory();
        this.directories.Add(directory);
        return directory.Path;
    }

    private SelfDiagnosticsLegacyConfigSource CreateLegacySource(SelfDiagnosticsController controller, string directory)
    {
        var path = Path.Combine(this.CreateDirectory(), SelfDiagnosticsConfigParser.ConfigFileName);
        WriteLegacyConfiguration(path, directory);
        return new SelfDiagnosticsLegacyConfigSource(
            new SelfDiagnosticsConfigParser(() => path),
            Timeout.InfiniteTimeSpan,
            controller.SetLegacyConfiguration);
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

        [Event(2, Message = "{0}", Level = EventLevel.LogAlways)]
        public void Always(string message) => this.WriteEvent(2, message);

        [Event(3, Message = "{0}", Level = EventLevel.Critical)]
        public void Critical(string message) => this.WriteEvent(3, message);
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

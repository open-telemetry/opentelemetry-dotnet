// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Exporter.Console.Tests;

[Collection(ConsoleOutputCollectionDefinition.Name)]
public class ConsoleExporterBindingTests
{
    [Fact]
    public void SectionAbsent_DefaultTargets_ConsoleOnly()
    {
        using var capture = new ConsoleOutputCapture();
        var name = Utils.GetCurrentMethodName();
        using var activitySource = new ActivitySource(name);

        var config = new ConfigurationBuilder().Build();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .ConfigureServices(s => s.AddSingleton<IConfiguration>(config))
            .AddSource(name)
            .AddConsoleExporter()
            .Build();

        activitySource.StartActivity("work")?.Stop();

        Assert.Contains("Activity.DisplayName", capture.ConsoleOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("Activity.DisplayName", capture.TraceOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Section_BindsDefaultName_TargetsDebug_TracingSwitchesToTrace()
    {
        using var capture = new ConsoleOutputCapture();
        var name = Utils.GetCurrentMethodName();
        using var activitySource = new ActivitySource(name);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["OpenTelemetry:Exporters:Console:Targets"] = "Debug" })
            .Build();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .ConfigureServices(s => s.AddSingleton<IConfiguration>(config))
            .AddSource(name)
            .AddConsoleExporter()
            .Build();

        activitySource.StartActivity("work")?.Stop();

        Assert.DoesNotContain("Activity.DisplayName", capture.ConsoleOutput, StringComparison.Ordinal);
        Assert.Contains("Activity.DisplayName", capture.TraceOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void NamedOptions_NotBound_SectionIgnored()
    {
        using var capture = new ConsoleOutputCapture();
        var name = Utils.GetCurrentMethodName();
        using var activitySource = new ActivitySource(name);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["OpenTelemetry:Exporters:Console:Targets"] = "Debug" })
            .Build();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .ConfigureServices(s => s.AddSingleton<IConfiguration>(config))
            .AddSource(name)
            .AddConsoleExporter("custom-name", configure: null)
            .Build();

        activitySource.StartActivity("work")?.Stop();

        // A non-default name is not automatically bound, so the option's default target
        // (Console) still applies, not the section's "Debug".
        Assert.Contains("Activity.DisplayName", capture.ConsoleOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("Activity.DisplayName", capture.TraceOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void NamedOptions_ExplicitSectionBinding_Applies()
    {
        using var capture = new ConsoleOutputCapture();
        var name = Utils.GetCurrentMethodName();
        using var activitySource = new ActivitySource(name);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["OpenTelemetry:Exporters:Console:Targets"] = "Debug" })
            .Build();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .ConfigureServices(s => s.AddSingleton<IConfiguration>(config))
            .ConfigureServices(s => s.Configure<ConsoleExporterOptions>(
                "custom-name", config.GetSection("OpenTelemetry:Exporters:Console")))
            .AddSource(name)
            .AddConsoleExporter("custom-name", configure: null)
            .Build();

        activitySource.StartActivity("work")?.Stop();

        Assert.DoesNotContain("Activity.DisplayName", capture.ConsoleOutput, StringComparison.Ordinal);
        Assert.Contains("Activity.DisplayName", capture.TraceOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplicitEmptyName_BehavesAsDefault()
    {
        using var capture = new ConsoleOutputCapture();
        var name = Utils.GetCurrentMethodName();
        using var activitySource = new ActivitySource(name);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["OpenTelemetry:Exporters:Console:Targets"] = "Debug" })
            .Build();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .ConfigureServices(s => s.AddSingleton<IConfiguration>(config))
            .AddSource(name)
            .AddConsoleExporter(Options.DefaultName, configure: null)
            .Build();

        activitySource.StartActivity("work")?.Stop();

        Assert.DoesNotContain("Activity.DisplayName", capture.ConsoleOutput, StringComparison.Ordinal);
        Assert.Contains("Activity.DisplayName", capture.TraceOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Configure_RegisteredBeforeAddConsoleExporter_OverridesSectionBinding()
    {
        using var capture = new ConsoleOutputCapture();
        var name = Utils.GetCurrentMethodName();
        using var activitySource = new ActivitySource(name);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["OpenTelemetry:Exporters:Console:Targets"] = "Debug" })
            .Build();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .ConfigureServices(s => s.AddSingleton<IConfiguration>(config))
            .ConfigureServices(s => s.Configure<ConsoleExporterOptions>(o => o.Targets = ConsoleExporterOutputTargets.Console))
            .AddSource(name)
            .AddConsoleExporter()
            .Build();

        activitySource.StartActivity("work")?.Stop();

        Assert.Contains("Activity.DisplayName", capture.ConsoleOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("Activity.DisplayName", capture.TraceOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Configure_RegisteredAfterAddConsoleExporter_OverridesSectionBinding()
    {
        using var capture = new ConsoleOutputCapture();
        var name = Utils.GetCurrentMethodName();
        using var activitySource = new ActivitySource(name);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["OpenTelemetry:Exporters:Console:Targets"] = "Debug" })
            .Build();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .ConfigureServices(s => s.AddSingleton<IConfiguration>(config))
            .AddSource(name)
            .AddConsoleExporter()
            .ConfigureServices(s => s.Configure<ConsoleExporterOptions>(o => o.Targets = ConsoleExporterOutputTargets.Console))
            .Build();

        activitySource.StartActivity("work")?.Stop();

        Assert.Contains("Activity.DisplayName", capture.ConsoleOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("Activity.DisplayName", capture.TraceOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void PostConfigure_OverridesConfigureAndSectionBinding()
    {
        using var capture = new ConsoleOutputCapture();
        var name = Utils.GetCurrentMethodName();
        using var activitySource = new ActivitySource(name);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["OpenTelemetry:Exporters:Console:Targets"] = "Debug" })
            .Build();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .ConfigureServices(s => s.AddSingleton<IConfiguration>(config))
            .AddSource(name)
            .AddConsoleExporter(o => o.Targets = ConsoleExporterOutputTargets.Debug)
            .ConfigureServices(s => s.PostConfigure<ConsoleExporterOptions>(o => o.Targets = ConsoleExporterOutputTargets.Console))
            .Build();

        activitySource.StartActivity("work")?.Stop();

        Assert.Contains("Activity.DisplayName", capture.ConsoleOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("Activity.DisplayName", capture.TraceOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void DefaultNameBinding_AppliesThroughLoggingOnlyRegistration()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["OpenTelemetry:Exporters:Console:Targets"] = "Debug" })
            .Build();

        IServiceProvider? capturedServices = null;

        using var loggerProvider = Sdk.CreateLoggerProviderBuilder()
            .ConfigureServices(s => s.AddSingleton<IConfiguration>(config))
            .AddConsoleExporter()
            .AddProcessor(sp =>
            {
                capturedServices = sp;
                return new SimpleLogRecordExportProcessor(new InMemoryExporter<LogRecord>([]));
            })
            .Build();

        var options = capturedServices!.GetRequiredService<IOptionsMonitor<ConsoleExporterOptions>>().Get(Options.DefaultName);

        Assert.Equal(ConsoleExporterOutputTargets.Debug, options.Targets);
    }

    [Fact]
    public void DefaultNameBinding_AppliesThroughMetricsOnlyRegistration()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["OpenTelemetry:Exporters:Console:Targets"] = "Debug" })
            .Build();

        IServiceProvider? capturedServices = null;

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .ConfigureServices(s => s.AddSingleton<IConfiguration>(config))
            .AddConsoleExporter()
            .ConfigureResource(resource => resource.AddDetector(sp =>
            {
                capturedServices = sp;
                return new EmptyResourceDetector();
            }))
            .Build();

        var options = capturedServices!.GetRequiredService<IOptionsMonitor<ConsoleExporterOptions>>().Get(Options.DefaultName);

        Assert.Equal(ConsoleExporterOutputTargets.Debug, options.Targets);
    }

    [Fact]
    public void DefaultNameBinding_EnvironmentOnlyFallback_NoExplicitIConfiguration()
    {
        const string envVarName = "OpenTelemetry__Exporters__Console__Targets";
        Environment.SetEnvironmentVariable(envVarName, "Debug");
        try
        {
            using var capture = new ConsoleOutputCapture();
            var name = Utils.GetCurrentMethodName();
            using var activitySource = new ActivitySource(name);

            // No IConfiguration registered: the SDK's environment-only fallback configuration
            // still sees the section-shaped environment variable spelling.
            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddSource(name)
                .AddConsoleExporter()
                .Build();

            activitySource.StartActivity("work")?.Stop();

            Assert.DoesNotContain("Activity.DisplayName", capture.ConsoleOutput, StringComparison.Ordinal);
            Assert.Contains("Activity.DisplayName", capture.TraceOutput, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVarName, null);
        }
    }

    [Theory]
    [InlineData("Console", ConsoleExporterOutputTargets.Console)]
    [InlineData("Debug", ConsoleExporterOutputTargets.Debug)]
    [InlineData("Console, Debug", ConsoleExporterOutputTargets.Console | ConsoleExporterOutputTargets.Debug)]
    [InlineData("Console,Debug", ConsoleExporterOutputTargets.Console | ConsoleExporterOutputTargets.Debug)]
    [InlineData("0", (ConsoleExporterOutputTargets)0)]
    [InlineData("3", ConsoleExporterOutputTargets.Console | ConsoleExporterOutputTargets.Debug)]
    [InlineData("Console, Console", ConsoleExporterOutputTargets.Console)]
    [InlineData("Debug, Console, Debug", ConsoleExporterOutputTargets.Console | ConsoleExporterOutputTargets.Debug)]
    [InlineData("4", (ConsoleExporterOutputTargets)4)]
    public void SectionBinding_SourceGenPath_BindsExpectedTargets(string configuredValue, ConsoleExporterOutputTargets expected)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["OpenTelemetry:Exporters:Console:Targets"] = configuredValue })
            .Build();

        // Production path: exercises AddConsoleExporterServices as compiled inside
        // OpenTelemetry.Exporter.Console, which enables the configuration binding source
        // generator on supported TFMs.
        Assert.Equal(expected, CreateDefaultOptions(config).Targets);
    }

    [Theory]
    [InlineData("Console", ConsoleExporterOutputTargets.Console)]
    [InlineData("Debug", ConsoleExporterOutputTargets.Debug)]
    [InlineData("Console, Debug", ConsoleExporterOutputTargets.Console | ConsoleExporterOutputTargets.Debug)]
    [InlineData("Console,Debug", ConsoleExporterOutputTargets.Console | ConsoleExporterOutputTargets.Debug)]
    [InlineData("0", (ConsoleExporterOutputTargets)0)]
    [InlineData("3", ConsoleExporterOutputTargets.Console | ConsoleExporterOutputTargets.Debug)]
    [InlineData("Console, Console", ConsoleExporterOutputTargets.Console)]
    [InlineData("Debug, Console, Debug", ConsoleExporterOutputTargets.Console | ConsoleExporterOutputTargets.Debug)]
    [InlineData("4", (ConsoleExporterOutputTargets)4)]
    public void SectionBinding_ReflectionPath_BindsExpectedTargets(string configuredValue, ConsoleExporterOutputTargets expected)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["OpenTelemetry:Exporters:Console:Targets"] = configuredValue })
            .Build();

        // Reflection path: ConfigurationBinder.Bind compiled in this test assembly, which does
        // not enable the generator, so this always uses reflection regardless of TFM.
        var reflection = new ConsoleExporterOptions();
        ConfigurationBinder.Bind(config.GetSection("OpenTelemetry:Exporters:Console"), reflection);

        Assert.Equal(expected, reflection.Targets);
    }

    [Fact]
    public void UnderDeclarativeStrictMode_SectionReadsAbsent_DefaultTargetsApply()
    {
        using var capture = new ConsoleOutputCapture();
        var name = Utils.GetCurrentMethodName();
        using var activitySource = new ActivitySource(name);

        var yamlPath = Path.Combine(Path.GetTempPath(), $"{Path.GetRandomFileName()}.yaml");
        File.WriteAllText(yamlPath, "file_format: \"1.0\"\ndisabled: false\n");

        try
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["OpenTelemetry:Exporters:Console:Targets"] = "Debug" })
                .AddOpenTelemetryDeclarativeConfiguration(yamlPath)
                .Build();

            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .ConfigureServices(s => s.AddSingleton<IConfiguration>(config))
                .AddSource(name)
                .AddConsoleExporter()
                .Build();

            activitySource.StartActivity("work")?.Stop();

            // The declarative mask hides "OpenTelemetry:Exporters:Console:Targets" from the section binder,
            // so the default (Console) applies, not the earlier source's "Debug".
            Assert.Contains("Activity.DisplayName", capture.ConsoleOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("Activity.DisplayName", capture.TraceOutput, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(yamlPath);
        }
    }

    [Fact]
    public void SameKeyInTwoProviders_LastProviderWins()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["OpenTelemetry:Exporters:Console:Targets"] = "Debug" })
            .AddInMemoryCollection(new Dictionary<string, string?> { ["OpenTelemetry:Exporters:Console:Targets"] = "Console" })
            .Build();

        Assert.Equal(ConsoleExporterOutputTargets.Console, CreateDefaultOptions(config).Targets);
    }

    [Theory]
    [InlineData("Consloe")]
    [InlineData("Console|Debug")]
    [InlineData("abc")]
    [InlineData("1.5")]
    [InlineData("Console;Debug")]
    [InlineData("\u0000")]
    [InlineData("\uD800")]
    public void InvalidTargets_Throws(string invalidValue)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["OpenTelemetry:Exporters:Console:Targets"] = invalidValue })
            .Build();

        Assert.Throws<InvalidOperationException>(() => CreateDefaultOptions(config));
    }

    [Fact]
    public void InvalidTargets_ThrowsWhenProviderIsBuilt()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["OpenTelemetry:Exporters:Console:Targets"] = "Consloe" })
            .Build();

        var builder = Sdk.CreateTracerProviderBuilder()
            .ConfigureServices(s => s.AddSingleton<IConfiguration>(config))
            .AddConsoleExporter();

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void InvalidTargets_UnderNamedRegistration_DoesNotThrow()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["OpenTelemetry:Exporters:Console:Targets"] = "Consloe" })
            .Build();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .ConfigureServices(s => s.AddSingleton<IConfiguration>(config))
            .AddConsoleExporter("custom-name", configure: null)
            .Build();

        Assert.NotNull(tracerProvider);
    }

    [Theory]
    [InlineData("debug", ConsoleExporterOutputTargets.Debug)]
    [InlineData("CONSOLE", ConsoleExporterOutputTargets.Console)]
    [InlineData(" Debug ", ConsoleExporterOutputTargets.Debug)]
    public void Targets_ValueIsCaseInsensitive_AndTrimmed(string configuredValue, ConsoleExporterOutputTargets expected)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["OpenTelemetry:Exporters:Console:Targets"] = configuredValue })
            .Build();

        Assert.Equal(expected, CreateDefaultOptions(config).Targets);
    }

    [Fact]
    public void EmptyTargets()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["OpenTelemetry:Exporters:Console:Targets"] = string.Empty })
            .Build();

#if NET8_0
        // The source-generated binder in Microsoft.Extensions.Configuration.Binder 8.0.2 does not
        // skip an empty enum value, unlike the reflection binder used on the other targets.
        Assert.Throws<InvalidOperationException>(() => CreateDefaultOptions(config));
#else
        Assert.Equal(new ConsoleExporterOptions().Targets, CreateDefaultOptions(config).Targets);
#endif
    }

    [Fact]
    public void UnknownSiblingKeys_AreIgnored()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenTelemetry:Exporters:Console:Targets"] = "Debug",
                ["OpenTelemetry:Exporters:Console:NotAnOption"] = "x",
                ["OpenTelemetry:Exporters:ConsoleFoo:Targets"] = "Console",
                ["OpenTelemetry:ServiceName"] = "app-owned",
            })
            .Build();

        Assert.Equal(ConsoleExporterOutputTargets.Debug, CreateDefaultOptions(config).Targets);
    }

    [Fact]
    public void AddConsoleExporterServices_CalledRepeatedly_RegistersOneFactoryAndNoChangeTokenSource()
    {
        var services = new ServiceCollection();
        services.AddConsoleExporterServices();
        services.AddConsoleExporterServices();

        Assert.Single(services, d => d.ServiceType == typeof(IOptionsFactory<ConsoleExporterOptions>));
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(IOptionsChangeTokenSource<ConsoleExporterOptions>));
    }

    [Fact]
    public void Configure_StillApplies_WhenSectionIsAbsent()
    {
        var services = new ServiceCollection();
        services.AddOptions();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddConsoleExporterServices();
        services.Configure<ConsoleExporterOptions>(o => o.Targets = ConsoleExporterOutputTargets.Debug);
        using var sp = services.BuildServiceProvider();

        var options = sp.GetRequiredService<IOptionsMonitor<ConsoleExporterOptions>>().Get(Options.DefaultName);

        Assert.Equal(ConsoleExporterOutputTargets.Debug, options.Targets);
    }

    private static ConsoleExporterOptions CreateDefaultOptions(IConfiguration config)
    {
        var services = new ServiceCollection();
        services.AddOptions();
        services.AddSingleton(config);
        services.AddConsoleExporterServices();
        using var sp = services.BuildServiceProvider();

        return sp.GetRequiredService<IOptionsMonitor<ConsoleExporterOptions>>().Get(Options.DefaultName);
    }

    private sealed class EmptyResourceDetector : IResourceDetector
    {
        public Resource Detect() => Resource.Empty;
    }
}

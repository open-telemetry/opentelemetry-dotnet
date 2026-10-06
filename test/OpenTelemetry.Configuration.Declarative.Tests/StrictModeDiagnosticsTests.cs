// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Configuration.Declarative.Tests;

public sealed class StrictModeDiagnosticsTests
{
    private const int SettingsIgnoredEventId = 36;
    private const int LaterSourceEventId = 37;
    private const int LaterEnvironmentVariablesEventId = 38;
    private const int ExplicitFilePathEventId = 39;
    private const int UnavailableEventId = 40;
    private const int SourceUnreachableEventId = 41;

    [Fact]
    public void EarlierSettings_ReportedOnceAcrossSignals_NamesSortedAndValuesOmitted()
    {
        const string secret = "api-key=secret-value";
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: false);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OTEL_TRACES_SAMPLER"] = "always_off",
                ["OTEL_EXPORTER_OTLP_HEADERS"] = secret,
            })
            .Build();

        using var listener = CreateListener(EventLevel.Warning);

        BuildAllSignals(config, yamlFile.Path);

        var evt = Assert.Single(listener.Messages, e => e.EventId == SettingsIgnoredEventId);
        Assert.Equal(yamlFile.Path, evt.Payload![0]);
        Assert.Equal("OTEL_EXPORTER_OTLP_HEADERS, OTEL_TRACES_SAMPLER", evt.Payload[1]);
        Assert.DoesNotContain(
            listener.Messages,
            e => e.Payload!.Any(p => p is string s && s.Contains("secret-value", StringComparison.Ordinal)));
    }

    [Fact]
    public void EarlierSettings_ReferencedThroughSubstitution_AreNotReported()
    {
        using var disabledScope = EnvironmentVariableScope.Create("OTEL_SDK_DISABLED", "false");
        using var endpointScope = EnvironmentVariableScope.Create("OTEL_EXPORTER_OTLP_ENDPOINT", "http://collector:4318");
        using var samplerScope = EnvironmentVariableScope.Create("OTEL_TRACES_SAMPLER", "always_off");
        using var yamlFile = DeclarativeYamlTestFile.CreateYamlFile("""
            file_format: "1.0"
            disabled: ${OTEL_SDK_DISABLED:-false}
            custom_section:
              endpoint: "${OTEL_EXPORTER_OTLP_ENDPOINT}"
            """);

        var config = new ConfigurationBuilder()
            .Add(new TestEnvironmentVariablesSource(new Dictionary<string, string?>
            {
                ["OTEL_SDK_DISABLED"] = "false",
                ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://collector:4318",
                ["OTEL_TRACES_SAMPLER"] = "always_off",
            }))
            .Build();

        using var listener = CreateListener(EventLevel.Warning);

        BuildAllSignals(config, yamlFile.Path);

        var evt = Assert.Single(listener.Messages, e => e.EventId == SettingsIgnoredEventId);
        Assert.Equal("OTEL_TRACES_SAMPLER", evt.Payload![1]);
    }

    [Fact]
    public void EarlierNonEnvironmentSetting_ReferencedThroughSubstitution_IsReported()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateYamlFile("""
            file_format: "1.0"
            disabled: ${OTEL_SDK_DISABLED:-false}
            """);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["OTEL_SDK_DISABLED"] = "true" })
            .AddOpenTelemetryDeclarativeConfiguration(yamlFile.Path)
            .Build();

        using var listener = CreateListener(EventLevel.Warning);

        StrictModeDiagnostics.Report(config);

        var evt = Assert.Single(listener.Messages, e => e.EventId == SettingsIgnoredEventId);
        Assert.Equal("OTEL_SDK_DISABLED", evt.Payload![1]);
    }

    [Fact]
    public void EnvironmentReference_UsesProcessEnvironmentNameCasing()
    {
        using var environmentScope = EnvironmentVariableScope.Create("OTEL_SDK_DISABLED", "true");
        using var yamlFile = DeclarativeYamlTestFile.CreateYamlFile("""
            file_format: "1.0"
            disabled: ${otel_sdk_disabled:-false}
            """);

        var config = new ConfigurationBuilder()
            .Add(new TestEnvironmentVariablesSource(new Dictionary<string, string?> { ["OTEL_SDK_DISABLED"] = "true" }))
            .AddOpenTelemetryDeclarativeConfiguration(yamlFile.Path)
            .Build();

        using var listener = CreateListener(EventLevel.Warning);

        StrictModeDiagnostics.Report(config);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.DoesNotContain(listener.Messages, e => e.EventId == SettingsIgnoredEventId);
        }
        else
        {
            var evt = Assert.Single(listener.Messages, e => e.EventId == SettingsIgnoredEventId);
            Assert.Equal("OTEL_SDK_DISABLED", evt.Payload![1]);
        }
    }

    [Fact]
    public void EnvironmentReference_DoesNotExemptPrefixedEnvironmentProvider()
    {
        using var referencedScope = EnvironmentVariableScope.Create("OTEL_SERVICE_NAME", "referenced-value");
        using var prefixedScope = EnvironmentVariableScope.Create("APP_OTEL_SERVICE_NAME", "prefixed-value");
        using var yamlFile = DeclarativeYamlTestFile.CreateYamlFile("""
            file_format: "1.0"
            custom_section:
              service_name: ${OTEL_SERVICE_NAME}
            """);

        var config = new ConfigurationBuilder()
            .AddEnvironmentVariables("APP_")
            .AddOpenTelemetryDeclarativeConfiguration(yamlFile.Path)
            .Build();

        using var listener = CreateListener(EventLevel.Warning);

        StrictModeDiagnostics.Report(config);

        var evt = Assert.Single(listener.Messages, e => e.EventId == SettingsIgnoredEventId);
        Assert.Equal("OTEL_SERVICE_NAME", evt.Payload![1]);
    }

    [Fact]
    public void EnvironmentReference_DoesNotExemptPrefixedEnvironmentProvider_UnprefixedVariableUnset()
    {
        using var referencedScope = EnvironmentVariableScope.Create("OTEL_SERVICE_NAME", null);
        using var prefixedScope = EnvironmentVariableScope.Create("APP_OTEL_SERVICE_NAME", "prefixed-value");
        using var yamlFile = DeclarativeYamlTestFile.CreateYamlFile("""
            file_format: "1.0"
            custom_section:
              service_name: ${OTEL_SERVICE_NAME}
            """);

        var config = new ConfigurationBuilder()
            .AddEnvironmentVariables("APP_")
            .AddOpenTelemetryDeclarativeConfiguration(yamlFile.Path)
            .Build();

        using var listener = CreateListener(EventLevel.Warning);

        StrictModeDiagnostics.Report(config);

        var evt = Assert.Single(listener.Messages, e => e.EventId == SettingsIgnoredEventId);
        Assert.Equal("OTEL_SERVICE_NAME", evt.Payload![1]);
    }

    [Fact]
    public void EnvironmentReference_ExemptsPrefixedEnvironmentProvider_WhenValueMatchesReferencedVariable()
    {
        using var referencedScope = EnvironmentVariableScope.Create("OTEL_SERVICE_NAME", "same-value");
        using var prefixedScope = EnvironmentVariableScope.Create("APP_OTEL_SERVICE_NAME", "same-value");
        using var yamlFile = DeclarativeYamlTestFile.CreateYamlFile("""
            file_format: "1.0"
            custom_section:
              service_name: ${OTEL_SERVICE_NAME}
            """);

        var config = new ConfigurationBuilder()
            .AddEnvironmentVariables("APP_")
            .AddOpenTelemetryDeclarativeConfiguration(yamlFile.Path)
            .Build();

        using var listener = CreateListener(EventLevel.Warning);

        StrictModeDiagnostics.Report(config);

        // Both variables resolve to the same value, so the document already uses it and nothing is masked.
        Assert.DoesNotContain(listener.CurrentMessages, e => e.EventId == SettingsIgnoredEventId);
    }

    [Theory]
    [InlineData("loaded-value", false)]
    [InlineData("changed-value", true)]
    public void EnvironmentReference_UsesValueCapturedWhenDocumentWasLoaded(
        string providerValue,
        bool expectIgnoredSetting)
    {
        using var referencedScope = EnvironmentVariableScope.Create("OTEL_SERVICE_NAME", "loaded-value");
        using var prefixedScope = EnvironmentVariableScope.Create("APP_OTEL_SERVICE_NAME", providerValue);
        using var yamlFile = DeclarativeYamlTestFile.CreateYamlFile("""
            file_format: "1.0"
            custom_section:
              service_name: ${OTEL_SERVICE_NAME}
            """);

        var config = new ConfigurationBuilder()
            .AddEnvironmentVariables("APP_")
            .AddOpenTelemetryDeclarativeConfiguration(yamlFile.Path)
            .Build();

        using var changedScope = EnvironmentVariableScope.Create("OTEL_SERVICE_NAME", "changed-value");
        using var listener = CreateListener(EventLevel.Warning);

        StrictModeDiagnostics.Report(config);

        if (expectIgnoredSetting)
        {
            var evt = Assert.Single(listener.Messages, e => e.EventId == SettingsIgnoredEventId);
            Assert.Equal("OTEL_SERVICE_NAME", evt.Payload![1]);
        }
        else
        {
            Assert.DoesNotContain(listener.CurrentMessages, e => e.EventId == SettingsIgnoredEventId);
        }
    }

    [Fact]
    public void EarlierSettings_OnlyExemptOrUnrelatedKeys_NothingReported()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: false);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [OtelEnvironmentVariables.ConfigFile] = "/some/path.yaml",
                ["OTEL_DOTNET_EXPERIMENTAL_OTLP_RETRY"] = "true",
                ["OTEL_EMPTY"] = string.Empty,
                ["OpenTelemetry:Otlp:Endpoint"] = "http://collector:4318",
                ["some-app-key"] = "app-value",
            })
            .Build();

        using var listener = CreateListener(EventLevel.Warning);

        BuildAllSignals(config, yamlFile.Path);

        Assert.DoesNotContain(listener.CurrentMessages, e => e.EventId is SettingsIgnoredEventId or LaterSourceEventId or LaterEnvironmentVariablesEventId);
    }

    [Fact]
    public void LaterSources_ReportedByProviderKind()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: false);

        using var config = new ConfigurationManager();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddOpenTelemetry()
            .UseDeclarativeConfiguration(yamlFile.Path)
            .WithTracing();

        // Added after registration, so they are later than the declarative source.
        config.AddInMemoryCollection(new Dictionary<string, string?> { ["OTEL_TRACES_SAMPLER"] = "always_off" });
        ((IConfigurationBuilder)config).Add(new TestEnvironmentVariablesSource(new Dictionary<string, string?> { ["OTEL_SERVICE_NAME"] = "later" }));

        using var listener = CreateListener(EventLevel.Warning);

        using var serviceProvider = services.BuildServiceProvider();
        _ = serviceProvider.GetRequiredService<TracerProvider>();

        var laterSource = Assert.Single(listener.Messages, e => e.EventId == LaterSourceEventId);
        Assert.Equal(
            "Microsoft.Extensions.Configuration.Memory.MemoryConfigurationProvider",
            laterSource.Payload![1]);
        Assert.Equal("OTEL_TRACES_SAMPLER", laterSource.Payload[2]);

        var laterEnvironment = Assert.Single(listener.Messages, e => e.EventId == LaterEnvironmentVariablesEventId);
        Assert.Equal("OTEL_SERVICE_NAME", laterEnvironment.Payload![1]);

        Assert.DoesNotContain(listener.Messages, e => e.EventId == SettingsIgnoredEventId);
    }

    [Fact]
    public void ChainedConfiguration_ClassifiesProvidersByWhetherTheMaskReachesThem()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: false);

        var inner = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["OTEL_TRACES_SAMPLER"] = "always_off" })
            .AddOpenTelemetryDeclarativeConfiguration(yamlFile.Path)
            .Build();

        var outer = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["OTEL_SERVICE_NAME"] = "outer-earlier" })
            .AddConfiguration(inner)
            .AddInMemoryCollection(new Dictionary<string, string?> { ["OTEL_BSP_SCHEDULE_DELAY"] = "1" })
            .Build();

        // A chained configuration reports a masked key as not found, so the enclosing root falls
        // through to its earlier providers. The diagnostics must report what is actually applied.
        Assert.Null(outer["OTEL_TRACES_SAMPLER"]);
        Assert.Equal("outer-earlier", outer["OTEL_SERVICE_NAME"]);

        using var listener = CreateListener(EventLevel.Warning);

        StrictModeDiagnostics.Report(outer);

        var ignored = Assert.Single(listener.Messages, e => e.EventId == SettingsIgnoredEventId);
        Assert.Equal("OTEL_TRACES_SAMPLER", ignored.Payload![1]);

        var unmasked = listener.Messages.Where(e => e.EventId == LaterSourceEventId).Select(e => (string)e.Payload![2]!).ToList();
        Assert.Equal(["OTEL_SERVICE_NAME", "OTEL_BSP_SCHEDULE_DELAY"], unmasked);
    }

    [Fact]
    public void ChainedConfiguration_DocumentValuePreventsEnclosingEarlierOverride()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: true);

        var inner = new ConfigurationBuilder()
            .AddOpenTelemetryDeclarativeConfiguration(yamlFile.Path)
            .Build();

        var outer = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["OTEL_SDK_DISABLED"] = "false" })
            .AddConfiguration(inner)
            .Build();

        Assert.Equal("true", outer["OTEL_SDK_DISABLED"]);

        using var listener = CreateListener(EventLevel.Warning);

        StrictModeDiagnostics.Report(outer);

        Assert.DoesNotContain(listener.Messages, e => e.EventId == LaterSourceEventId);
    }

    [Fact]
    public void ChainedConfiguration_EmptyEffectiveValueFallsThroughToEnclosingEarlierProvider()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: true);

        var inner = new ConfigurationBuilder()
            .AddOpenTelemetryDeclarativeConfiguration(yamlFile.Path)
            .AddInMemoryCollection(new Dictionary<string, string?> { ["OTEL_SDK_DISABLED"] = string.Empty })
            .Build();

        var outer = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["OTEL_SDK_DISABLED"] = "false" })
            .AddConfiguration(inner)
            .Build();

        // Starting with .NET 11, ChainedConfigurationProvider.TryGet treats an empty string as present
        // (previously only non-empty values counted), so the inner configuration's empty override now
        // wins over the outer, earlier provider instead of falling through to it.
        // See: https://github.com/dotnet/runtime/pull/131480 and https://github.com/dotnet/docs/issues/55653
#if NET11_0_OR_GREATER || NETFRAMEWORK
        Assert.Equal(string.Empty, outer["OTEL_SDK_DISABLED"]);
#else
        Assert.Equal("false", outer["OTEL_SDK_DISABLED"]);
#endif

        using var listener = CreateListener(EventLevel.Warning);

        StrictModeDiagnostics.Report(outer);

        var evt = Assert.Single(listener.Messages, e => e.EventId == LaterSourceEventId);
        Assert.Equal("OTEL_SDK_DISABLED", evt.Payload![2]);
    }

    [Fact]
    public void ChainedConfiguration_NullEffectiveValueDoesNotOverrideDocument()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: false);

        var later = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["OTEL_SERVICE_NAME"] = "hidden" })
            .AddInMemoryCollection(new Dictionary<string, string?> { ["OTEL_SERVICE_NAME"] = null })
            .Build();

        var config = new ConfigurationBuilder()
            .AddOpenTelemetryDeclarativeConfiguration(yamlFile.Path)
            .AddConfiguration(later)
            .Build();

        Assert.Null(config["OTEL_SERVICE_NAME"]);

        using var listener = CreateListener(EventLevel.Warning);

        StrictModeDiagnostics.Report(config);

        Assert.DoesNotContain(
            listener.Messages,
            e => e.EventId is LaterSourceEventId or LaterEnvironmentVariablesEventId);
    }

    [Fact]
    public void ChainedConfiguration_EarlierSettingHiddenByChain_IsNotReportedAsIgnored()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: false);

        var earlier = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["OTEL_SERVICE_NAME"] = "hidden" })
            .AddInMemoryCollection(new Dictionary<string, string?> { ["OTEL_SERVICE_NAME"] = null })
            .Build();

        var outer = new ConfigurationBuilder()
            .AddConfiguration(earlier)
            .AddOpenTelemetryDeclarativeConfiguration(yamlFile.Path)
            .Build();

        Assert.Null(outer["OTEL_SERVICE_NAME"]);

        using var listener = CreateListener(EventLevel.Warning);

        StrictModeDiagnostics.Report(outer);

        Assert.DoesNotContain(listener.Messages, e => e.EventId == SettingsIgnoredEventId);
    }

    [Fact]
    public void ChainedConfiguration_SameProviderBeforeAndAfterDocument_ReportsOverride()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: false);

        var shared = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["OTEL_SERVICE_NAME"] = "outside" })
            .Build();

        var outer = new ConfigurationBuilder()
            .AddConfiguration(shared)
            .AddOpenTelemetryDeclarativeConfiguration(yamlFile.Path)
            .AddConfiguration(shared)
            .Build();

        Assert.Equal("outside", outer["OTEL_SERVICE_NAME"]);

        using var listener = CreateListener(EventLevel.Warning);

        StrictModeDiagnostics.Report(outer);

        var evt = Assert.Single(listener.Messages, e => e.EventId == LaterSourceEventId);
        Assert.Equal("OTEL_SERVICE_NAME", evt.Payload![2]);
    }

    [Fact]
    public void ChainedConfiguration_SameRootTwice_UsesSelectedRouteForMasking()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: false);

        var inner = new ConfigurationBuilder()
            .AddOpenTelemetryDeclarativeConfiguration(yamlFile.Path)
            .AddInMemoryCollection(new Dictionary<string, string?> { ["OTEL_SERVICE_NAME"] = "outside" })
            .Build();

        var outer = new ConfigurationBuilder()
            .AddConfiguration(inner)
            .AddConfiguration(inner)
            .Build();

        Assert.Equal("outside", outer["OTEL_SERVICE_NAME"]);

        using var listener = CreateListener(EventLevel.Warning);

        StrictModeDiagnostics.Report(outer);

        Assert.DoesNotContain(listener.Messages, e => e.EventId == SettingsIgnoredEventId);
        var evt = Assert.Single(listener.Messages, e => e.EventId == LaterSourceEventId);
        Assert.Equal("OTEL_SERVICE_NAME", evt.Payload![2]);
    }

    [Fact]
    public void LaterEmptyValue_OverridesDocumentAndIsReported()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: true);

        var config = new ConfigurationBuilder()
            .AddOpenTelemetryDeclarativeConfiguration(yamlFile.Path)
            .AddInMemoryCollection(new Dictionary<string, string?> { ["OTEL_SDK_DISABLED"] = string.Empty })
            .Build();

        Assert.Equal(string.Empty, config["OTEL_SDK_DISABLED"]);

        using var listener = CreateListener(EventLevel.Warning);

        StrictModeDiagnostics.Report(config);

        var evt = Assert.Single(listener.Messages, e => e.EventId == LaterSourceEventId);
        Assert.Equal("OTEL_SDK_DISABLED", evt.Payload![2]);
    }

    [Fact]
    public void LegacyHostBuilder_EarlierSettingIsMaskedAndReported()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: false);

        using var listener = CreateListener(EventLevel.Warning);

        using var host = new HostBuilder()
            .ConfigureAppConfiguration(c => c.AddInMemoryCollection(new Dictionary<string, string?> { ["OTEL_TRACES_SAMPLER"] = "always_off" }))
            .ConfigureServices(s => s.AddOpenTelemetry().UseDeclarativeConfiguration(yamlFile.Path).WithTracing())
            .Build();

        _ = host.Services.GetRequiredService<TracerProvider>();

        Assert.Null(host.Services.GetRequiredService<IConfiguration>()["OTEL_TRACES_SAMPLER"]);
        var evt = Assert.Single(listener.Messages, e => e.EventId == SettingsIgnoredEventId);
        Assert.Equal("OTEL_TRACES_SAMPLER", evt.Payload![1]);
    }

    [Fact]
    public void ExplicitPath_DiffersFromConfigFile_Warns()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: false);
        using var otherFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: false);
        using var envScope = EnvironmentVariableScope.Create(OtelEnvironmentVariables.ConfigFile, otherFile.Path);
        using var listener = CreateListener(EventLevel.Warning);

        new ServiceCollection().AddOpenTelemetry().UseDeclarativeConfiguration(yamlFile.Path);

        var evt = Assert.Single(listener.Messages, e => e.EventId == ExplicitFilePathEventId);
        Assert.Equal(yamlFile.Path, evt.Payload![0]);
        Assert.Equal(otherFile.Path, evt.Payload[1]);
    }

    [Fact]
    public void ExplicitPath_ConfigFileNotAPath_WarnsWithRawValue()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: false);
        using var envScope = EnvironmentVariableScope.Create(OtelEnvironmentVariables.ConfigFile, "not-yaml.txt");
        using var listener = CreateListener(EventLevel.Warning);

        new ServiceCollection().AddOpenTelemetry().UseDeclarativeConfiguration(yamlFile.Path);

        var evt = Assert.Single(listener.Messages, e => e.EventId == ExplicitFilePathEventId);
        Assert.Equal("not-yaml.txt", evt.Payload![1]);
    }

    [Fact]
    public void ExplicitPath_SameFileAsConfigFile_DoesNotWarn()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: false);
        using var envScope = EnvironmentVariableScope.Create(OtelEnvironmentVariables.ConfigFile, yamlFile.Path);
        using var listener = CreateListener(EventLevel.Warning);

        new ServiceCollection().AddOpenTelemetry().UseDeclarativeConfiguration(yamlFile.Path);

        Assert.DoesNotContain(listener.CurrentMessages, e => e.EventId == ExplicitFilePathEventId);
    }

    [Fact]
    public void ParameterlessOverload_DoesNotWarn()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: false);
        using var envScope = EnvironmentVariableScope.Create(OtelEnvironmentVariables.ConfigFile, yamlFile.Path);
        using var listener = CreateListener(EventLevel.Warning);

        new ServiceCollection().AddOpenTelemetry().UseDeclarativeConfiguration();

        Assert.DoesNotContain(listener.CurrentMessages, e => e.EventId == ExplicitFilePathEventId);
    }

    [Fact]
    public void NonRootConfiguration_ReportsUnavailable()
    {
        using var listener = CreateListener(EventLevel.Verbose);

        StrictModeDiagnostics.Report(new NonRootConfiguration());

        Assert.Single(listener.Messages, e => e.EventId == UnavailableEventId);
    }

    [Fact]
    public void DetachedSource_ReportsUnavailable()
    {
        using var listener = CreateListener(EventLevel.Verbose);

        StrictModeDiagnostics.Report(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["OTEL_TRACES_SAMPLER"] = "always_off" })
            .Build());

        Assert.Single(listener.Messages, e => e.EventId == UnavailableEventId);
        Assert.DoesNotContain(listener.CurrentMessages, e => e.EventId == SettingsIgnoredEventId);
    }

    [Fact]
    public void DetachedRegisteredSource_ReportsWarning()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: false);
        var services = new ServiceCollection();
        services.AddOpenTelemetry()
            .UseDeclarativeConfiguration(yamlFile.Path)
            .WithTracing();

        // A later IConfiguration registration wins in DI and detaches the declarative source.
        using var replacement = new ConfigurationManager();
        services.AddSingleton<IConfiguration>(replacement);

        using var listener = CreateListener(EventLevel.Warning);
        using var serviceProvider = services.BuildServiceProvider();

        _ = serviceProvider.GetRequiredService<TracerProvider>();

        var evt = Assert.Single(listener.Messages, e => e.EventId == SourceUnreachableEventId);
        Assert.Equal(yamlFile.Path, evt.Payload![0]);
    }

    [Fact]
    public void ReportOnce_NoConfigurationRegistered_ReportsUnavailableOnce()
    {
        using var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var diagnostics = new StrictModeDiagnostics();
        using var listener = CreateListener(EventLevel.Verbose);

        diagnostics.ReportOnce(serviceProvider);
        diagnostics.ReportOnce(serviceProvider);

        Assert.Single(listener.Messages, e => e.EventId == UnavailableEventId);
    }

    [Fact]
    public void Reader_RecordsReferencedVariables()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateYamlFile("""
            file_format: "1.0"
            disabled: ${OTEL_SDK_DISABLED:-false}
            resource:
              attributes_list: "${ATTRS}"
              attributes:
                - name: a
                  value: "${ATTRS}-${env:SUFFIX}"
            custom_section:
              key: "$${NOT_A_REFERENCE}"
            """);

        var document = DeclarativeConfigurationReader.Read(new FilePath(yamlFile.Path), _ => null);

        Assert.True(document.ReferencesEnvironmentVariable("ATTRS"), "Expected ATTRS to be referenced.");
        Assert.True(document.ReferencesEnvironmentVariable("OTEL_SDK_DISABLED"), "Expected OTEL_SDK_DISABLED to be referenced.");
        Assert.True(document.ReferencesEnvironmentVariable("SUFFIX"), "Expected SUFFIX to be referenced.");
        Assert.False(document.ReferencesEnvironmentVariable("NOT_A_REFERENCE"), "Expected the escaped variable not to be referenced.");
    }

    [Fact]
    public void Reader_EmptyFile_HasNoReferencedVariables()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateYamlFile(string.Empty);

        var document = DeclarativeConfigurationReader.Read(new FilePath(yamlFile.Path), _ => null);

        Assert.False(document.ReferencesEnvironmentVariable("OTEL_SDK_DISABLED"), "Expected no environment variables to be referenced.");
    }

    [Fact]
    public void EventMessages_ContainNoLiteralBraces()
    {
        var messages = typeof(OpenTelemetryDeclarativeConfigurationEventSource)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Select(m => m.GetCustomAttribute<EventAttribute>()?.Message)
            .OfType<string>()
            .ToList();

        // Any brace that is not part of a {N} format placeholder.
        Assert.NotEmpty(messages);
        Assert.All(messages, m => Assert.DoesNotMatch(@"\{(?!\d+\})|(?<!\{\d+)\}", m));
    }

    private static void BuildAllSignals(IConfiguration configuration, string yamlPath)
    {
        var services = new ServiceCollection();
        services.AddSingleton(configuration);
        services.AddOpenTelemetry()
            .UseDeclarativeConfiguration(yamlPath)
            .WithTracing()
            .WithMetrics()
            .WithLogging();

        using var serviceProvider = services.BuildServiceProvider();
        _ = serviceProvider.GetRequiredService<TracerProvider>();
        _ = serviceProvider.GetRequiredService<MeterProvider>();
        _ = serviceProvider.GetRequiredService<LoggerProvider>();
    }

    private static TestEventListener CreateListener(EventLevel level)
    {
        var listener = new TestEventListener();
        listener.EnableEvents(OpenTelemetryDeclarativeConfigurationEventSource.Log, level, EventKeywords.All);
        return listener;
    }

    private sealed class TestEnvironmentVariablesSource(IDictionary<string, string?> data) : IConfigurationSource
    {
        public IConfigurationProvider Build(IConfigurationBuilder builder) => new TestEnvironmentVariablesProvider(data);
    }

    // Stands in for the process environment, so the test does not have to set real variables.
    private sealed class TestEnvironmentVariablesProvider(IDictionary<string, string?> data) : EnvironmentVariablesConfigurationProvider
    {
        public override void Load() => this.Data = new Dictionary<string, string?>(data, StringComparer.OrdinalIgnoreCase);
    }

    private sealed class NonRootConfiguration : IConfiguration
    {
        private readonly IConfiguration inner = new ConfigurationBuilder().Build();

        public string? this[string key]
        {
            get => this.inner[key];
            set => this.inner[key] = value;
        }

        public IEnumerable<IConfigurationSection> GetChildren() => this.inner.GetChildren();

        public Microsoft.Extensions.Primitives.IChangeToken GetReloadToken() => this.inner.GetReloadToken();

        public IConfigurationSection GetSection(string key) => this.inner.GetSection(key);
    }
}

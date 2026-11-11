// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Tracing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Exporter;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Configuration.Declarative.Tests;

[Collection(EventSourceCollectionDefinition.Name)]
public sealed class DeclarativeTracerProviderTests
{
    private const string MemoryExporter = "test_memory";
    private const string RecordingProcessor = "test_recording";
    private const int DifferentSourceAlreadyRegisteredEventId = 29;
    private const int ScheduleDelayZeroReplacedEventId = 46;
    private const int TracerExclusionNotAppliedEventId = 47;
    private const int ComponentCleanupFailedEventId = 48;

    private static readonly string SimpleMemoryProcessor = SimpleProcessor(MemoryExporter);

    public enum RegistrationPath
    {
        /// <summary>
        /// <c>builder.AddOpenTelemetry()</c> on a host application builder.
        /// </summary>
        HostApplicationBuilder,

        /// <summary>
        /// <c>builder.Services.AddOpenTelemetry()</c> on a host application builder.
        /// </summary>
        ServicesOnHostApplicationBuilder,

        /// <summary>
        /// <c>services.AddOpenTelemetry()</c> inside <c>HostBuilder.ConfigureServices</c>.
        /// </summary>
        HostBuilder,

        /// <summary>
        /// <c>services.AddOpenTelemetry()</c> on a plain service collection.
        /// </summary>
        ServiceCollection,
    }

    public static TheoryData<RegistrationPath> RegistrationPaths =>
    [
        RegistrationPath.HostApplicationBuilder,
        RegistrationPath.ServicesOnHostApplicationBuilder,
        RegistrationPath.HostBuilder,
        RegistrationPath.ServiceCollection,
    ];

    public static IEnumerable<TheoryDataRow<string>> BatchDocumentsWithoutSettings =>
    [
        new(BatchProcessor("console")) { TestDisplayName = "omitted" },
        new(BatchProcessor(
            "console",
            "schedule_delay:",
            "export_timeout:",
            "max_queue_size:",
            "max_export_batch_size:")) { TestDisplayName = "explicit nulls" },
    ];

    // Each row is a scenario name, a tracer_provider document and the fragment of the error message
    // the user must see. The document follows a "file_format" line, so "tracer_provider:" is line 2
    // and the reported line and column numbers can be read straight from the YAML below.
    public static IEnumerable<TheoryDataRow<string, string>> InvalidSections =>
    [
        // tracer_provider section shape
        Invalid(
            "tracer_provider not a mapping",
            "tracer_provider: []",
            "'tracer_provider' (line 2, column 18) must be a mapping."),

        // processors list
        Invalid(
            "processors missing",
            "tracer_provider: {}",
            "'tracer_provider' (line 2, column 1) requires 'processors'"),
        Invalid(
            "processors null",
            """
            tracer_provider:
              processors:
            """,
            "'tracer_provider.processors'"),
        Invalid(
            "processors empty",
            """
            tracer_provider:
              processors: []
            """,
            "'tracer_provider.processors'"),
        Invalid(
            "processor names none",
            """
            tracer_provider:
              processors:
                - {}
            """,
            "'tracer_provider.processors[0]' (line 4, column 7) must name exactly one span processor"),
        Invalid(
            "processor names two",
            """
            tracer_provider:
              processors:
                - simple: {exporter: {console: }}
                  batch: {exporter: {console: }}
            """,
            "'tracer_provider.processors[0]'"),

        // simple processor and its exporter
        Invalid(
            "simple without exporter",
            """
            tracer_provider:
              processors:
                - simple:
            """,
            "'tracer_provider.processors[0].simple' (line 4, column 7) requires 'exporter'"),
        Invalid(
            "simple with null exporter",
            """
            tracer_provider:
              processors:
                - simple:
                    exporter:
            """,
            "'tracer_provider.processors[0].simple' (line 4, column 7) requires 'exporter'"),
        Invalid(
            "exporter names none",
            """
            tracer_provider:
              processors:
                - simple:
                    exporter: {}
            """,
            "'tracer_provider.processors[0].simple.exporter' (line 5, column 19) must name exactly one span exporter"),
        Invalid(
            "exporter names two",
            """
            tracer_provider:
              processors:
                - batch:
                    exporter: {console: , otlp_http: }
            """,
            "'tracer_provider.processors[0].batch.exporter'"),
        Invalid(
            "exporter properties not a mapping",
            """
            tracer_provider:
              processors:
                - simple:
                    exporter:
                      console: not-a-mapping
            """,
            "'tracer_provider.processors[0].simple.exporter.console'"),

        // batch processor settings
        Invalid(
            "negative schedule_delay",
            """
            tracer_provider:
              processors:
                - batch:
                    schedule_delay: -1
                    exporter: {console: }
            """,
            "'tracer_provider.processors[0].batch.schedule_delay' (line 5, column 25) must be a non-negative integer"),
        Invalid(
            "non-numeric export_timeout",
            """
            tracer_provider:
              processors:
                - batch:
                    export_timeout: soon
                    exporter: {console: }
            """,
            "'tracer_provider.processors[0].batch.export_timeout' (line 5"),
        Invalid(
            "zero max_queue_size",
            """
            tracer_provider:
              processors:
                - batch:
                    max_queue_size: 0
                    exporter: {console: }
            """,
            "'tracer_provider.processors[0].batch.max_queue_size' (line 5, column 25) must be a positive integer"),
        Invalid(
            "zero max_export_batch_size",
            """
            tracer_provider:
              processors:
                - batch:
                    max_export_batch_size: 0
                    exporter: {console: }
            """,
            "'tracer_provider.processors[0].batch.max_export_batch_size' (line 5"),
        Invalid(
            "max_export_batch_size larger than max_queue_size",
            """
            tracer_provider:
              processors:
                - batch:
                    max_queue_size: 10
                    max_export_batch_size: 11
                    exporter: {console: }
            """,
            "'tracer_provider.processors[0].batch.max_export_batch_size' (line 6, column 32) is 11, which is larger than 'max_queue_size' (10)"),
        Invalid(
            "max_queue_size smaller than default max_export_batch_size",
            """
            tracer_provider:
              processors:
                - batch:
                    max_queue_size: 100
                    exporter: {console: }
            """,
            "'tracer_provider.processors[0].batch.max_queue_size' (line 5, column 25) is 100, which is smaller than the default 'max_export_batch_size' (512)"),

        // tracer configurator
        Invalid(
            "empty tracer name",
            """
            tracer_provider:
              processors: [{simple: {exporter: {console: }}}]
              tracer_configurator/development:
                tracers:
                  - name: ""
                    config: {}
            """,
            "'tracer_provider.tracer_configurator/development.tracers[0].name' (line 6, column 15) must not be empty or whitespace."),

        // Core's AddSource rejects a whitespace name, so this must fail at the call, not at provider build.
        Invalid(
            "whitespace tracer name",
            """
            tracer_provider:
              processors: [{simple: {exporter: {console: }}}]
              tracer_configurator/development:
                default_config:
                  enabled: false
                tracers:
                  - name: " "
                    config: {}
            """,
            "'tracer_provider.tracer_configurator/development.tracers[0].name' (line 8, column 15) must not be empty or whitespace."),
        Invalid(
            "tracer entry not a mapping",
            """
            tracer_provider:
              processors: [{simple: {exporter: {console: }}}]
              tracer_configurator/development:
                tracers:
                  - not-a-mapping
            """,
            "'tracer_provider.tracer_configurator/development.tracers[0]'"),
        Invalid(
            "tracer without name",
            """
            tracer_provider:
              processors: [{simple: {exporter: {console: }}}]
              tracer_configurator/development:
                tracers:
                  - config: {}
            """,
            "'tracer_provider.tracer_configurator/development.tracers[0]' (line 6, column 9) requires 'name'"),
        Invalid(
            "tracer without config",
            """
            tracer_provider:
              processors: [{simple: {exporter: {console: }}}]
              tracer_configurator/development:
                tracers:
                  - name: a
            """,
            "'tracer_provider.tracer_configurator/development.tracers[0]' (line 6, column 9) requires 'config'"),
        Invalid(
            "non-boolean tracer enabled",
            """
            tracer_provider:
              processors: [{simple: {exporter: {console: }}}]
              tracer_configurator/development:
                tracers:
                  - name: a
                    config: {enabled: "yes"}
            """,
            "'tracer_provider.tracer_configurator/development.tracers[0].config.enabled' (line 7"),
        Invalid(
            "non-boolean default_config enabled",
            """
            tracer_provider:
              processors: [{simple: {exporter: {console: }}}]
              tracer_configurator/development:
                default_config: {enabled: 1}
            """,
            "'tracer_provider.tracer_configurator/development.default_config.enabled' (line 5"),
    ];

    [Theory]
    [MemberData(nameof(RegistrationPaths))]
    public void Document_WithTracerProvider_ExportsSpanWithoutWithTracing(RegistrationPath path)
    {
        var sourceName = NewSourceName();
        var exported = new List<Activity>();

        using var yamlFile = CreateDocument(SimpleMemoryProcessor);

        // The provider is registered after UseDeclarativeConfiguration, which the build-time lookup allows.
        using var services = Build(path, yamlFile.Path, s => s.AddPluginComponentProvider(MemoryExporter, new MemoryExporterProvider(exported)));
        _ = services.Provider.GetRequiredService<TracerProvider>();

        StartAndStop(sourceName);

        Assert.Single(exported, a => a.Source.Name == sourceName);
    }

    [Fact]
    public void Document_FromOtelConfigFile_ExportsSpanWithoutWithTracing()
    {
        var sourceName = NewSourceName();
        var exported = new List<Activity>();

        using var yamlFile = CreateDocument(SimpleMemoryProcessor);
        using var environment = EnvironmentVariableScope.Create("OTEL_CONFIG_FILE", yamlFile.Path);

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddPluginComponentProvider(MemoryExporter, new MemoryExporterProvider(exported));
        serviceCollection.AddOpenTelemetry().UseDeclarativeConfiguration();

        using var provider = serviceCollection.BuildServiceProvider();
        _ = provider.GetRequiredService<TracerProvider>();

        StartAndStop(sourceName);

        Assert.Single(exported, a => a.Source.Name == sourceName);
    }

    [Fact]
    public void Document_WithoutTracerProvider_TurnsNothingOn()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(fileFormat: "1.2");

        using var services = Build(RegistrationPath.ServiceCollection, yamlFile.Path);

        Assert.Null(services.Provider.GetService<TracerProvider>());
    }

    [Fact]
    public void Document_WithoutTracerProvider_TracingFromCodeStillRuns()
    {
        var sourceName = NewSourceName();
        var exported = new List<Activity>();

        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(fileFormat: "1.2");

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddOpenTelemetry()
            .UseDeclarativeConfiguration(yamlFile.Path)
            .WithTracing(b => b.AddSource(sourceName).AddInMemoryExporter(exported));

        using var provider = serviceCollection.BuildServiceProvider();
        _ = provider.GetRequiredService<TracerProvider>();

        StartAndStop(sourceName);

        Assert.Single(exported, a => a.Source.Name == sourceName);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Document_AndWithTracingInCode_BuildOneProviderWithBothProcessors(bool codeFirst)
    {
        var sourceName = NewSourceName();
        var fromDocument = new List<Activity>();
        var fromCode = new List<Activity>();

        using var yamlFile = CreateDocument(SimpleMemoryProcessor);

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddPluginComponentProvider(MemoryExporter, new MemoryExporterProvider(fromDocument));
        var builder = serviceCollection.AddOpenTelemetry();

        if (codeFirst)
        {
            builder.WithTracing(b => b.AddInMemoryExporter(fromCode)).UseDeclarativeConfiguration(yamlFile.Path);
        }
        else
        {
            builder.UseDeclarativeConfiguration(yamlFile.Path).WithTracing(b => b.AddInMemoryExporter(fromCode));
        }

        using var provider = serviceCollection.BuildServiceProvider();

        Assert.Single(provider.GetServices<TracerProvider>());

        StartAndStop(sourceName);

        Assert.Single(fromDocument, a => a.Source.Name == sourceName);
        Assert.Single(fromCode, a => a.Source.Name == sourceName);
    }

    [Theory]
    [MemberData(nameof(RegistrationPaths))]
    public void InvalidDocument_ThrowsFromRegistrationCall(RegistrationPath path)
    {
        using var yamlFile = CreateDocument("""
            tracer_provider:
              processors: []
            """);

        // On the HostBuilder path the call runs inside ConfigureServices, during Build.
        var exception = Assert.Throws<DeclarativeConfigurationException>(() => Build(path, yamlFile.Path).Dispose());

        Assert.Contains("'tracer_provider.processors' (line 3, column", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidSection_OnLiveConfiguration_LeavesNoSourceSoALaterCallCanRegister()
    {
        var sourceName = NewSourceName();
        var exported = new List<Activity>();

        using var invalidFile = CreateDocument("""
            tracer_provider:
              processors: []
            """);
        using var validFile = CreateDocument(SimpleMemoryProcessor);

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddPluginComponentProvider(MemoryExporter, new MemoryExporterProvider(exported));

        // The file parses, so the source is added before binding fails.
        Assert.Throws<DeclarativeConfigurationException>(() => builder.AddOpenTelemetry().UseDeclarativeConfiguration(invalidFile.Path));
        Assert.Empty(((IConfigurationBuilder)builder.Configuration).Sources.OfType<DeclarativeConfigurationSource>());

        builder.AddOpenTelemetry().UseDeclarativeConfiguration(validFile.Path);

        using var host = builder.Build();
        _ = host.Services.GetRequiredService<TracerProvider>();

        StartAndStop(sourceName);

        Assert.Single(exported, a => a.Source.Name == sourceName);
    }

    [Fact]
    public void SharedConfiguration_WithDifferentCallFile_WarnsAndBuildsPipelineFromSharedConfiguration()
    {
        var sourceName = NewSourceName();
        var fromShared = new List<Activity>();
        var fromCall = new List<Activity>();

        using var sharedFile = CreateDocument(SimpleProcessor("test_shared"));
        using var callFile = CreateDocument(SimpleProcessor("test_call"));

        var shared = new ConfigurationBuilder().AddOpenTelemetryDeclarativeConfiguration(sharedFile.Path).Build();
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddConfiguration(shared).Build());
        serviceCollection.AddPluginComponentProvider("test_shared", new MemoryExporterProvider(fromShared));
        serviceCollection.AddPluginComponentProvider("test_call", new MemoryExporterProvider(fromCall));

        using var listener = CreateListener();

        serviceCollection.AddOpenTelemetry().UseDeclarativeConfiguration(callFile.Path);

        using var provider = serviceCollection.BuildServiceProvider();
        _ = provider.GetRequiredService<TracerProvider>();

        StartAndStop(sourceName);

        // Activation followed the call's file; the pipeline follows the shared configuration's document.
        Assert.Contains(listener.Messages, e => e.EventId == DifferentSourceAlreadyRegisteredEventId);
        Assert.Single(fromShared, a => a.Source.Name == sourceName);
        Assert.DoesNotContain(fromCall, a => a.Source.Name == sourceName);
    }

    [Fact]
    public void NoConfigurator_ListensToEverySource()
    {
        var exported = new List<Activity>();

        using var provider = BuildWithMemoryExporter(SimpleMemoryProcessor, exported);

        var first = NewSourceName();
        var second = NewSourceName();
        StartAndStop(first);
        StartAndStop(second);

        Assert.Single(exported, a => a.Source.Name == first);
        Assert.Single(exported, a => a.Source.Name == second);
    }

    [Fact]
    public void ConfiguratorWithoutTracers_DefaultDisabled_ListensToNoSources()
    {
        // Omitting tracers is valid and means no per-tracer overrides; with default_config.enabled
        // false that leaves the provider listening to nothing from the document.
        var tracerProvider = SimpleMemoryProcessor + """

              tracer_configurator/development:
                default_config:
                  enabled: false
            """;

        Assert.Empty(Bind(tracerProvider).Configurator!.Tracers);

        var exported = new List<Activity>();
        using var provider = BuildWithMemoryExporter(tracerProvider, exported);

        StartAndStop(NewSourceName());

        Assert.Empty(exported);
    }

    [Fact]
    public void DefaultDisabled_ListensOnlyToEnabledEntries()
    {
        var exported = new List<Activity>();
        using var provider = BuildWithMemoryExporter(
            SimpleMemoryProcessor + """

              tracer_configurator/development:
                default_config:
                  enabled: false
                tracers:
                  - name: DeclarativeTracing.Selected.*
                    config:
                      enabled: true
                  - name: DeclarativeTracing.Q?
                    config: {}
                  - name: DeclarativeTracing.Off
                    config:
                      enabled: false
            """,
            exported);

        StartAndStop("DeclarativeTracing.Selected.Alpha");
        StartAndStop("DeclarativeTracing.Q1");
        StartAndStop("DeclarativeTracing.Q12");
        StartAndStop("DeclarativeTracing.Off");

        Assert.Equal(
            ["DeclarativeTracing.Selected.Alpha", "DeclarativeTracing.Q1"],
            exported.Select(a => a.Source.Name).Where(n => n.StartsWith("DeclarativeTracing.", StringComparison.Ordinal)));
    }

    [Fact]
    public void DisabledEntry_WithDefaultEnabled_IsStillListenedToAndWarns()
    {
        var sourceName = NewSourceName();
        var exported = new List<Activity>();

        using var listener = CreateListener();
        using var provider = BuildWithMemoryExporter(
            SimpleMemoryProcessor + $"""

              tracer_configurator/development:
                tracers:
                  - name: {sourceName}
                    config:
                      enabled: false
            """,
            exported);

        StartAndStop(sourceName);

        Assert.Single(exported, a => a.Source.Name == sourceName);
        var warning = Assert.Single(listener.Messages, e => e.EventId == TracerExclusionNotAppliedEventId);
        Assert.Equal(EventLevel.Warning, warning.Level);
        Assert.Equal("tracer_provider.tracer_configurator/development.tracers[0]", warning.Payload![1]);
        Assert.Equal(sourceName, warning.Payload[4]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PartiallyOverlappingEntries_KeepInclusionAndWarnOnlyWhenExclusionIsFirst(bool exclusionFirst)
    {
        var exclusion = new TracerConfiguratorEntry("MyApp.Internal", Enabled: false, "tracers[x]", default);
        var inclusion = new TracerConfiguratorEntry("MyApp.*", Enabled: true, "tracers[y]", default);
        var configurator = new TracerConfiguratorSection(
            DefaultEnabled: false,
            exclusionFirst ? [exclusion, inclusion] : [inclusion, exclusion]);

        using var listener = CreateListener();
        var sources = TracerActivitySourcePatternResolver.Resolve(configurator, "otel.yaml");

        Assert.Equal(["MyApp.*"], sources);

        var warnings = listener.Messages.Where(e => e.EventId == TracerExclusionNotAppliedEventId).ToList();

        if (exclusionFirst)
        {
            var warning = Assert.Single(warnings);
            Assert.Equal("tracers[x]", warning.Payload![1]);
            Assert.Contains("'tracers[y]'", (string)warning.Payload[5]!, StringComparison.Ordinal);
        }
        else
        {
            Assert.Empty(warnings);
        }
    }

    [Theory]
    [InlineData(".Internal", ".Internal", ".Internal")]
    [InlineData(".?", ".?", ".X")]
    public void DuplicateNameInclusion_IsNotListenedToAndDoesNotWarn(
        string exclusionSuffix,
        string inclusionSuffix,
        string sourceSuffix)
    {
        var prefix = NewSourceName();
        var exclusion = prefix + exclusionSuffix;
        var inclusion = prefix + inclusionSuffix;
        var sourceName = prefix + sourceSuffix;
        var exported = new List<Activity>();

        using var listener = CreateListener();
        using var provider = BuildWithMemoryExporter(DefaultDisabledConfigurator(exclusion, inclusion), exported);

        StartAndStop(sourceName);

        Assert.DoesNotContain(exported, a => a.Source.Name == sourceName);
        Assert.DoesNotContain(listener.Messages, e => e.EventId == TracerExclusionNotAppliedEventId);
    }

    [Theory]
    [InlineData("?")]
    [InlineData("*")]
    public void UnicodeOverlap_KeepsRegexInclusionAndWarns(string exclusionWildcard)
    {
        var prefix = NewSourceName();
        var sourceName = prefix + ".\u212Ax";
        var exported = new List<Activity>();

        using var listener = CreateListener();
        using var provider = BuildWithMemoryExporter(
            DefaultDisabledConfigurator($"{prefix}.K{exclusionWildcard}", $"{prefix}.\\u212A?"),
            exported);

        StartAndStop(sourceName);

        Assert.Single(exported, a => a.Source.Name == sourceName);
        Assert.Single(listener.Messages, e => e.EventId == TracerExclusionNotAppliedEventId);
    }

    [Fact]
    public void PrefixCoverage_DoesNotRemoveRegexInclusion()
    {
        var prefix = NewSourceName();
        var sourceName = prefix + ".Kx";
        var exported = new List<Activity>();

        using var listener = CreateListener();
        using var provider = BuildWithMemoryExporter(DefaultDisabledConfigurator($"{prefix}.K*", $"{prefix}.K?"), exported);

        StartAndStop(sourceName);

        Assert.Single(exported, a => a.Source.Name == sourceName);
#if NET
        // Modern .NET regex case folding also includes the Kelvin sign, unlike ordinal matching.
        var unicodeSourceName = prefix + ".\u212Ax";
        StartAndStop(unicodeSourceName);
        Assert.Single(exported, a => a.Source.Name == unicodeSourceName);
#endif
        Assert.Single(listener.Messages, e => e.EventId == TracerExclusionNotAppliedEventId);
    }

    [Fact]
    public void AddSourceInCode_AddsToTheDocumentSelection()
    {
        var exported = new List<Activity>();

        using var listener = CreateListener();
        using var yamlFile = CreateDocument(SimpleMemoryProcessor + """

              tracer_configurator/development:
                default_config:
                  enabled: false
                tracers:
                  - name: DeclarativeTracing.CodeAndDocument.Document
                    config: {}
                  - name: DeclarativeTracing.CodeAndDocument.Code
                    config:
                      enabled: false
            """);

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddPluginComponentProvider(MemoryExporter, new MemoryExporterProvider(exported));
        serviceCollection.AddOpenTelemetry()
            .UseDeclarativeConfiguration(yamlFile.Path)
            .WithTracing(b => b.AddSource("DeclarativeTracing.CodeAndDocument.Code"));

        using var provider = serviceCollection.BuildServiceProvider();
        _ = provider.GetRequiredService<TracerProvider>();

        StartAndStop("DeclarativeTracing.CodeAndDocument.Document");
        StartAndStop("DeclarativeTracing.CodeAndDocument.Code");

        Assert.Equal(
            ["DeclarativeTracing.CodeAndDocument.Document", "DeclarativeTracing.CodeAndDocument.Code"],
            exported.Select(a => a.Source.Name).Where(n => n.StartsWith("DeclarativeTracing.CodeAndDocument.", StringComparison.Ordinal)));
        Assert.DoesNotContain(listener.Messages, e => e.EventId == TracerExclusionNotAppliedEventId);
    }

    [Fact]
    public void Processors_AreCreatedInDocumentOrder()
    {
        var processors = CreateProcessors("""
            tracer_provider:
              processors:
                - batch:
                    exporter:
                      console:
                - simple:
                    exporter:
                      console:
            """);

        Assert.Collection(
            processors,
            p => Assert.IsType<BatchActivityExportProcessor>(p),
            p => Assert.IsType<SimpleActivityExportProcessor>(p));
    }

    [Fact]
    public void ConsoleExporter_IsCreatedThroughTheRegistry()
    {
        using var yamlFile = CreateDocument(SimpleProcessor("console"));
        using var services = CreateServices(yamlFile.Path);
        var registry = services.GetRequiredService<PluginComponentProviderRegistry>();
        using var exporter = registry.Create<BaseExporter<Activity>>("console", ConfigProperties.Empty);

        Assert.IsType<ConsoleActivityExporter>(exporter);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConsoleExporter_ConflictingRegisteredProvider_Throws(bool registerBefore)
    {
        using var yamlFile = CreateDocument(SimpleProcessor("console"));
        var services = new ServiceCollection();
        var builder = services.AddOpenTelemetry();
        var provider = new MemoryExporterProvider([]);

        InvalidOperationException exception;
        if (registerBefore)
        {
            services.AddPluginComponentProvider("console", provider);
            exception = Assert.Throws<InvalidOperationException>(() => builder.UseDeclarativeConfiguration(yamlFile.Path));
        }
        else
        {
            builder.UseDeclarativeConfiguration(yamlFile.Path);
            exception = Assert.Throws<InvalidOperationException>(() => services.AddPluginComponentProvider("console", provider));
        }

        Assert.Contains("console", exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(ConsoleSpanExporterProvider), exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(MemoryExporterProvider), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateProcessors_WithoutDocumentAccessor_ReturnsEmpty()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();

        Assert.Empty(TracerProviderConfigurationApplier.CreateProcessors(provider));
    }

    [Fact]
    public void GetEffectiveBatchSettings_NonBatchEntry_Throws()
    {
        var entry = Assert.Single(Bind(SimpleProcessor("console")).Processors);

        var exception = Assert.Throws<ArgumentException>(
            () => TracerProviderConfigurationApplier.GetEffectiveBatchSettings(new FilePath("otel.yaml"), entry));

        Assert.Equal("entry", exception.ParamName);
        Assert.Contains("not a batch processor", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BatchSettings_ReachTheConstructor() =>
        AssertBatchSettings(
            BatchProcessor(
                "console",
                "schedule_delay: 100",
                "export_timeout: 200",
                "max_queue_size: 300",
                "max_export_batch_size: 40"),
            scheduleDelay: 100,
            exportTimeout: 200,
            maxQueueSize: 300,
            maxExportBatchSize: 40);

    [Fact]
    public void MaxExportBatchSize_LimitsEachExport()
    {
        var sourceName = NewSourceName();
        var batchSizes = new List<long>();

        using var yamlFile = CreateDocument(
            BatchProcessor("test_batches", "schedule_delay: 60000", "max_export_batch_size: 2") + EnableOnly(sourceName));

        using var provider = CreateServices(
            yamlFile.Path,
            s => s.AddPluginComponentProvider("test_batches", new BatchRecordingExporterProvider(batchSizes)));

        var tracerProvider = provider.GetRequiredService<TracerProvider>();

        for (var i = 0; i < 5; i++)
        {
            StartAndStop(sourceName);
        }

        Assert.True(tracerProvider.ForceFlush(), "ForceFlush should complete before the default timeout.");

        // The export thread can run between spans, so how the five spans are split varies. Only the
        // cap on each export is fixed.
        lock (batchSizes)
        {
            Assert.Equal(5, batchSizes.Sum());
            Assert.All(batchSizes, size => Assert.InRange(size, 1, 2));
        }
    }

    [Theory]
    [MemberData(nameof(BatchDocumentsWithoutSettings))]
    public void BatchSettings_NullOrOmitted_UseTheDefaults(string tracerProvider)
    {
        AssertBatchSettings(
            tracerProvider,
            scheduleDelay: 5000,
            exportTimeout: 30000,
            maxQueueSize: 2048,
            maxExportBatchSize: 512);
    }

    [Fact]
    public void ScheduleDelayZero_BecomesOneMillisecond_AndWarns()
    {
        using var listener = CreateListener();

        // Core rejects a delay of 0, so building the processor shows the replacement reached it.
        var processor = Assert.Single(CreateProcessors(BatchProcessor("console", "schedule_delay: 0")));
        Assert.IsType<BatchActivityExportProcessor>(processor);

        var warning = Assert.Single(listener.Messages, e => e.EventId == ScheduleDelayZeroReplacedEventId);
        Assert.Equal(EventLevel.Warning, warning.Level);
        Assert.Equal("tracer_provider.processors[0].batch.schedule_delay", warning.Payload![1]);
        Assert.Equal(5L, warning.Payload[2]);

        Assert.Equal(1, GetEffectiveBatchSettings(BatchProcessor("console", "schedule_delay: 0")).ScheduleDelayMilliseconds);
    }

    [Fact]
    public void ExportTimeoutZero_IsPassedAsNoLimit() =>
        AssertBatchSettings(
            BatchProcessor("console", "export_timeout: 0"),
            scheduleDelay: 5000,
            exportTimeout: 0,
            maxQueueSize: 2048,
            maxExportBatchSize: 512);

    [Fact]
    public void ProcessorsFromDocumentAndCode_RunInCallOrder()
    {
        var sourceName = NewSourceName();
        var log = new List<string>();

        using var yamlFile = CreateDocument($"""
            tracer_provider:
              processors:
                - {RecordingProcessor}:
                    label: document
            """);

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddPluginComponentProvider(RecordingProcessor, new RecordingProcessorProvider(log));
        serviceCollection.AddOpenTelemetry()
            .WithTracing(b => b.AddProcessor(new RecordingActivityProcessor("before", log)))
            .UseDeclarativeConfiguration(yamlFile.Path)
            .WithTracing(b => b.AddProcessor(new RecordingActivityProcessor("after", log)));

        using var provider = serviceCollection.BuildServiceProvider();
        _ = provider.GetRequiredService<TracerProvider>();

        StartAndStop(sourceName);

        Assert.Equal(["before", "document", "after"], log);
    }

    [Fact]
    public void StrictMode_BatchEnvironmentVariablesHaveNoEffect()
    {
        var sourceName = NewSourceName();
        var batchSizes = new List<long>();

        // If these were read, the queue would hold two spans and each export would carry one.
        using var environment = EnvironmentVariableScope.Create(
            ("OTEL_BSP_SCHEDULE_DELAY", "1"),
            ("OTEL_BSP_EXPORT_TIMEOUT", "456"),
            ("OTEL_BSP_MAX_QUEUE_SIZE", "2"),
            ("OTEL_BSP_MAX_EXPORT_BATCH_SIZE", "1"));

        // A long delay keeps the spans queued until the flush, so they leave in one export.
        using var yamlFile = CreateDocument(BatchProcessor("test_batches", "schedule_delay: 60000") + EnableOnly(sourceName));

        using var provider = CreateServices(
            yamlFile.Path,
            s => s.AddPluginComponentProvider("test_batches", new BatchRecordingExporterProvider(batchSizes)));

        var tracerProvider = provider.GetRequiredService<TracerProvider>();

        for (var i = 0; i < 5; i++)
        {
            StartAndStop(sourceName);
        }

        Assert.True(tracerProvider.ForceFlush(), "ForceFlush should complete before the default timeout.");

        lock (batchSizes)
        {
            Assert.Equal([5L], batchSizes);
        }

        AssertBatchSettings(
            BatchProcessor("console"),
            scheduleDelay: 5000,
            exportTimeout: 30000,
            maxQueueSize: 2048,
            maxExportBatchSize: 512);
    }

    [Fact]
    public void ConsoleExporter_PrintsASpan()
    {
        var sourceName = NewSourceName();
        using var yamlFile = CreateDocument(SimpleProcessor("console") + EnableOnly(sourceName));

        var originalOut = Console.Out;
        using var output = new StringWriter();
        try
        {
            Console.SetOut(output);

            using var provider = CreateServices(yamlFile.Path);
            _ = provider.GetRequiredService<TracerProvider>();

            StartAndStop(sourceName, "console-smoke-test");
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        Assert.Contains("console-smoke-test", output.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(InvalidSections))]
    public void InvalidSection_ThrowsFromTheCall_WithThePathAndPosition(string tracerProvider, string expected)
    {
        using var yamlFile = CreateDocument(tracerProvider);

        var builder = new ServiceCollection().AddOpenTelemetry();
        var exception = Assert.Throws<DeclarativeConfigurationException>(() => builder.UseDeclarativeConfiguration(yamlFile.Path));

        Assert.Contains(expected, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("otlp_http")]
    [InlineData("otlp_grpc")]
    [InlineData("otlp_file/development")]
    [InlineData("x_vendor_exporter")]
    public void UnsupportedExporter_FailsAtProviderBuild(string exporter)
    {
        var exception = GetProviderBuildFailure($"""
            tracer_provider:
              processors:
                - batch:
                    exporter:
                      {exporter}:
            """);

        Assert.Contains($"'tracer_provider.processors[0].batch.exporter.{exporter}' (line 6, column 11) is a span exporter", exception.Message, StringComparison.Ordinal);
        Assert.Contains("does not support", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("register", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("registration", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnsupportedProcessor_FailsAtProviderBuild()
    {
        var exception = GetProviderBuildFailure("""
            tracer_provider:
              processors:
                - x_vendor_processor:
            """);

        Assert.Contains("'tracer_provider.processors[0].x_vendor_processor' (line 4, column 7) is a span processor", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnsupportedSettings_FailWithOneExceptionListingEveryPath()
    {
        var exception = GetProviderBuildFailure("""
            tracer_provider:
              processors:
                - batch:
                    exporter:
                      otlp_http:
              sampler:
                x_vendor_sampler:
              limits:
                attribute_count_limit: 10
            """);

        var exporter = exception.Message.IndexOf("'tracer_provider.processors[0].batch.exporter.otlp_http'", StringComparison.Ordinal);
        var sampler = exception.Message.IndexOf("'tracer_provider.sampler.x_vendor_sampler' (line 8, column 5) is a sampler", StringComparison.Ordinal);
        var limits = exception.Message.IndexOf("'tracer_provider.limits' (line 9, column 3) is a Stable setting", StringComparison.Ordinal);

        Assert.True(
            exporter >= 0 && sampler > exporter && limits > sampler,
            $"Expected the exporter, sampler and limits paths in that order, but the message was: {exception.Message}");
    }

    [Fact]
    public void RandomIdGenerator_BuildsProviderAndExportsSpan()
    {
        var sourceName = NewSourceName();
        var exported = new List<Activity>();
        using var provider = BuildWithMemoryExporter(
            SimpleMemoryProcessor + """

              id_generator:
                random:
            """,
            exported);

        StartAndStop(sourceName);

        Assert.Single(exported, a => a.Source.Name == sourceName);
    }

    [Fact]
    public void UnsupportedIdGenerator_FailsAtProviderBuild()
    {
        var exception = GetProviderBuildFailure("""
            tracer_provider:
              processors:
                - simple:
                    exporter:
                      console:
              id_generator:
                x_vendor_id_generator:
            """);

        Assert.Contains("'tracer_provider.id_generator.x_vendor_id_generator' (line 8, column 5) is an ID generator", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RegisteredProvider_ForAnUnknownName_IsUsed()
    {
        var sourceName = NewSourceName();
        var log = new List<string>();

        using var yamlFile = CreateDocument($"""
            tracer_provider:
              processors:
                - {RecordingProcessor}:
                    label: from-provider
            """);

        using var provider = CreateServices(yamlFile.Path, s => s.AddPluginComponentProvider(RecordingProcessor, new RecordingProcessorProvider(log)));
        _ = provider.GetRequiredService<TracerProvider>();

        StartAndStop(sourceName);

        Assert.Equal(["from-provider"], log);
    }

    [Fact]
    public void ProviderException_PropagatesUnchanged()
    {
        using var yamlFile = CreateDocument($"""
            tracer_provider:
              processors:
                - {RecordingProcessor}:
            """);

        var thrown = new InvalidOperationException("provider failure");
        using var provider = CreateServices(yamlFile.Path, s => s.AddPluginComponentProvider(RecordingProcessor, new ThrowingProcessorProvider(thrown)));

        var exception = Assert.Throws<InvalidOperationException>(provider.GetRequiredService<TracerProvider>);

        Assert.Same(thrown, exception);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProcessorCreationFailure_CleansUpEveryProcessor_AndPreservesTheOriginalException(bool throwOnDispose)
    {
        using var yamlFile = CreateDocument("""
            tracer_provider:
              processors:
                - test_lifecycle:
                    label: first
                - test_lifecycle:
                    label: second
                - test_throwing:
            """);

        var log = new List<string>();
        var thrown = new InvalidOperationException("provider failure");
        using var provider = CreateServices(
            yamlFile.Path,
            s =>
            {
                s.AddPluginComponentProvider("test_lifecycle", new LifecycleProcessorProvider(log, throwOnDispose));
                s.AddPluginComponentProvider("test_throwing", new ThrowingProcessorProvider(thrown));
            });
        using var listener = CreateListener();

        var exception = Assert.Throws<InvalidOperationException>(provider.GetRequiredService<TracerProvider>);

        Assert.Same(thrown, exception);
        Assert.Equal(
            ["first.Shutdown(0)", "first.Dispose", "second.Shutdown(0)", "second.Dispose"],
            log);

        var cleanupFailures = listener.Messages.Where(e => e.EventId == ComponentCleanupFailedEventId).ToArray();

        if (throwOnDispose)
        {
            var failure = Assert.Single(cleanupFailures);
            Assert.Equal(EventLevel.Error, failure.Level);
            Assert.Equal(typeof(LifecycleProcessor).ToString(), failure.Payload![0]);
            Assert.Contains("dispose failure", Assert.IsType<string>(failure.Payload[1]), StringComparison.Ordinal);
        }
        else
        {
            Assert.Empty(cleanupFailures);
        }
    }

    [Theory]
    [InlineData("simple")]
    [InlineData("batch")]
    public void ProcessorCreationFailure_ShutsDownAndDisposesEarlierExporter(string processor)
    {
        using var yamlFile = CreateDocument($"""
            tracer_provider:
              processors:
                - {processor}:
                    exporter:
                      test_lifecycle:
                - test_throwing:
            """);

        var log = new List<string>();
        var thrown = new InvalidOperationException("provider failure");
        using var provider = CreateServices(
            yamlFile.Path,
            s =>
            {
                s.AddPluginComponentProvider("test_lifecycle", new LifecycleExporterProvider(log));
                s.AddPluginComponentProvider("test_throwing", new ThrowingProcessorProvider(thrown));
            });

        var exception = Assert.Throws<InvalidOperationException>(provider.GetRequiredService<TracerProvider>);

        Assert.Same(thrown, exception);
        Assert.Equal(["Shutdown(0)", "Dispose"], log);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BatchProcessorConstructionFailure_DisposesTheExporter_AndPreservesTheOriginalException(bool throwOnDispose)
    {
        using var yamlFile = CreateDocument("""
            tracer_provider:
              processors:
                - batch:
                    exporter:
                      test_lifecycle:
            """);

        var log = new List<string>();
        using var provider = CreateServices(
            yamlFile.Path,
            s => s.AddPluginComponentProvider("test_lifecycle", new LifecycleExporterProvider(log, throwOnDispose)));

        var registry = provider.GetRequiredService<PluginComponentProviderRegistry>();
        var entry = Assert.Single(Bind(BatchProcessor("console")).Processors);

        // Validation of the document prevents this combination, so build the entry directly to make
        // core reject the settings after the exporter exists.
        var invalid = entry with
        {
            Exporter = Assert.Single(Bind(BatchProcessor("test_lifecycle")).Processors).Exporter,
            BatchSection = new BatchSpanProcessorSection(5000, 30000, MaxQueueSize: 10, MaxExportBatchSize: 11),
        };

        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => TracerProviderConfigurationApplier.CreateProcessor(new FilePath("otel.yaml"), invalid, registry));

        Assert.Equal("maxExportBatchSize", exception.ParamName);
        Assert.Equal(11, exception.ActualValue);
        Assert.Equal(["Dispose"], log);
    }

    private static string SimpleProcessor(string exporter) => $"""
        tracer_provider:
          processors:
            - simple:
                exporter:
                  {exporter}:
        """;

    private static DeclarativeYamlTestFile CreateDocument(string body) =>
        DeclarativeYamlTestFile.CreateYamlFile("file_format: \"1.2\"\n" + body + "\n");

    private static TheoryDataRow<string, string> Invalid(string scenario, string tracerProvider, string expected) =>
        new(tracerProvider, expected) { TestDisplayName = scenario };

    private static string NewSourceName() => "DeclarativeTracing." + Guid.NewGuid().ToString("N");

    private static string BatchProcessor(string exporter, params string[] settings)
    {
        var settingLines = string.Concat(settings.Select(s => $"        {s}\n"));

        return $"""
            tracer_provider:
              processors:
                - batch:
            {settingLines}        exporter:
                      {exporter}:
            """;
    }

    // Enables only the named source, with every other source disabled by default.
    private static string EnableOnly(string sourceName) => $"""

          tracer_configurator/development:
            default_config:
              enabled: false
            tracers:
              - name: {sourceName}
                config:
                  enabled: true
        """;

    private static string DefaultDisabledConfigurator(string excludedName, string includedName) => SimpleMemoryProcessor + $"""

          tracer_configurator/development:
            default_config:
              enabled: false
            tracers:
              - name: "{excludedName}"
                config:
                  enabled: false
              - name: "{includedName}"
                config:
                  enabled: true
        """;

    private static ServiceProvider CreateServices(string yamlPath, Action<IServiceCollection>? configureServices = null)
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddOpenTelemetry().UseDeclarativeConfiguration(yamlPath);
        configureServices?.Invoke(serviceCollection);
        return serviceCollection.BuildServiceProvider();
    }

    private static DeclarativeConfigurationException GetProviderBuildFailure(string tracerProvider)
    {
        using var yamlFile = CreateDocument(tracerProvider);
        using var provider = CreateServices(yamlFile.Path);

        return Assert.Throws<DeclarativeConfigurationException>(provider.GetRequiredService<TracerProvider>);
    }

    private static void StartAndStop(string sourceName, string activityName = "work")
    {
        using var source = new ActivitySource(sourceName);
        using var activity = source.StartActivity(activityName);
    }

    private static TestEventListener CreateListener() =>
        new(OpenTelemetryDeclarativeConfigurationEventSource.Log, EventLevel.Verbose);

    private static ServiceProvider BuildWithMemoryExporter(string tracerProvider, List<Activity> exported)
    {
        using var yamlFile = CreateDocument(tracerProvider);

        var provider = CreateServices(yamlFile.Path, s => s.AddPluginComponentProvider(MemoryExporter, new MemoryExporterProvider(exported)));
        _ = provider.GetRequiredService<TracerProvider>();
        return provider;
    }

    private static IReadOnlyList<BaseProcessor<Activity>> CreateProcessors(string tracerProvider)
    {
        using var yamlFile = CreateDocument(tracerProvider);

        using var provider = CreateServices(yamlFile.Path);

        var processors = TracerProviderConfigurationApplier.CreateProcessors(provider);
        foreach (var processor in processors)
        {
            processor.Shutdown(0);
            processor.Dispose();
        }

        return processors;
    }

    private static void AssertBatchSettings(string tracerProvider, int scheduleDelay, int exportTimeout, int maxQueueSize, int maxExportBatchSize)
    {
        // The processor is built, so core accepted the settings.
        Assert.IsType<BatchActivityExportProcessor>(Assert.Single(CreateProcessors(tracerProvider)));

        // Core does not expose the settings, so the test checks the values the applier passes to
        // the constructor. MaxExportBatchSize_LimitsEachExport and
        // StrictMode_BatchEnvironmentVariablesHaveNoEffect check the effect on exports.
        var batch = GetEffectiveBatchSettings(tracerProvider);
        Assert.Equal(new BatchSpanProcessorSection(scheduleDelay, exportTimeout, maxQueueSize, maxExportBatchSize), batch);
    }

    private static BatchSpanProcessorSection GetEffectiveBatchSettings(string tracerProvider) =>
        TracerProviderConfigurationApplier.GetEffectiveBatchSettings(
            new FilePath("otel.yaml"),
            Assert.Single(Bind(tracerProvider).Processors));

    private static TracerProviderSection Bind(string tracerProvider)
    {
        using var yamlFile = CreateDocument(tracerProvider);
        var document = DeclarativeConfigurationReader.Read(new FilePath(yamlFile.Path));
        var configuration = TracerProviderConfigurationBinder.Bind(document.Properties);
        Assert.NotNull(configuration);
        return configuration;
    }

    private static ServiceProviderOwner Build(RegistrationPath path, string yamlPath, Action<IServiceCollection>? configureServices = null)
    {
        switch (path)
        {
            case RegistrationPath.HostApplicationBuilder:
                {
                    var builder = Host.CreateApplicationBuilder();
                    builder.AddOpenTelemetry().UseDeclarativeConfiguration(yamlPath);
                    configureServices?.Invoke(builder.Services);
                    var host = builder.Build();
                    return new(host.Services, host);
                }

            case RegistrationPath.ServicesOnHostApplicationBuilder:
                {
                    var builder = Host.CreateApplicationBuilder();
                    builder.Services.AddOpenTelemetry().UseDeclarativeConfiguration(yamlPath);
                    configureServices?.Invoke(builder.Services);
                    var host = builder.Build();
                    return new(host.Services, host);
                }

            case RegistrationPath.HostBuilder:
                {
                    var host = new HostBuilder()
                        .ConfigureServices(services =>
                        {
                            services.AddOpenTelemetry().UseDeclarativeConfiguration(yamlPath);
                            configureServices?.Invoke(services);
                        })
                        .Build();
                    return new(host.Services, host);
                }

            case RegistrationPath.ServiceCollection:
            default:
                {
                    var services = new ServiceCollection();
                    services.AddOpenTelemetry().UseDeclarativeConfiguration(yamlPath);
                    configureServices?.Invoke(services);
                    var provider = services.BuildServiceProvider();
                    return new(provider, provider);
                }
        }
    }

    private sealed class ServiceProviderOwner(IServiceProvider provider, IDisposable owner) : IDisposable
    {
        public IServiceProvider Provider { get; } = provider;

        public void Dispose() => owner.Dispose();
    }

    private sealed class MemoryExporterProvider(ICollection<Activity> exported) : PluginComponentProvider<BaseExporter<Activity>>
    {
        public override BaseExporter<Activity> Create(ConfigProperties properties, IServiceProvider serviceProvider) =>
            new InMemoryExporter<Activity>(exported);
    }

    private sealed class BatchRecordingExporterProvider(List<long> batchSizes) : PluginComponentProvider<BaseExporter<Activity>>
    {
        public override BaseExporter<Activity> Create(ConfigProperties properties, IServiceProvider serviceProvider) =>
            new BatchRecordingExporter(batchSizes);
    }

    private sealed class BatchRecordingExporter(List<long> batchSizes) : BaseExporter<Activity>
    {
        public override ExportResult Export(in Batch<Activity> batch)
        {
            lock (batchSizes)
            {
                batchSizes.Add(batch.Count);
            }

            return ExportResult.Success;
        }
    }

    private sealed class RecordingProcessorProvider(List<string> log) : PluginComponentProvider<BaseProcessor<Activity>>
    {
        public override BaseProcessor<Activity> Create(ConfigProperties properties, IServiceProvider serviceProvider) =>
            new RecordingActivityProcessor(properties.GetString("label").Value ?? "unlabelled", log);
    }

    private sealed class ThrowingProcessorProvider(Exception exception) : PluginComponentProvider<BaseProcessor<Activity>>
    {
        public override BaseProcessor<Activity> Create(ConfigProperties properties, IServiceProvider serviceProvider) =>
            throw exception;
    }

    private sealed class LifecycleProcessorProvider(List<string> log, bool throwOnDispose) : PluginComponentProvider<BaseProcessor<Activity>>
    {
        public override BaseProcessor<Activity> Create(ConfigProperties properties, IServiceProvider serviceProvider) =>
            new LifecycleProcessor(properties.GetString("label").Value!, log, throwOnDispose);
    }

    private sealed class LifecycleProcessor(string label, List<string> log, bool throwOnDispose) : BaseProcessor<Activity>
    {
        protected override bool OnShutdown(int timeoutMilliseconds)
        {
            log.Add($"{label}.Shutdown({timeoutMilliseconds})");
            return true;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                log.Add($"{label}.Dispose");
                if (throwOnDispose && label == "first")
                {
                    throw new InvalidOperationException("dispose failure");
                }
            }
        }
    }

    private sealed class LifecycleExporterProvider(List<string> log, bool throwOnDispose = false) : PluginComponentProvider<BaseExporter<Activity>>
    {
        public override BaseExporter<Activity> Create(ConfigProperties properties, IServiceProvider serviceProvider) =>
            new LifecycleExporter(log, throwOnDispose);
    }

    private sealed class LifecycleExporter(List<string> log, bool throwOnDispose) : BaseExporter<Activity>
    {
        public override ExportResult Export(in Batch<Activity> batch) => ExportResult.Success;

        protected override bool OnShutdown(int timeoutMilliseconds)
        {
            log.Add($"Shutdown({timeoutMilliseconds})");
            return true;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                log.Add("Dispose");
                if (throwOnDispose)
                {
                    throw new InvalidOperationException("dispose failure");
                }
            }
        }
    }

    private sealed class RecordingActivityProcessor(string label, List<string> log) : BaseProcessor<Activity>
    {
        public override void OnEnd(Activity data)
        {
            lock (log)
            {
                log.Add(label);
            }
        }
    }
}

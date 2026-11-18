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
public sealed class DeclarativeSamplerTests
{
    private const string MemoryExporter = "test_memory";
    private const string TestSampler = "test_sampler";
    private const int SamplerReplacedByCodeEventId = 49;
    private const int ComponentCleanupFailedEventId = 48;

    private const string AllChildren =
        """
        parent_based:
          root:
            always_off:
          remote_parent_sampled:
            always_off:
          remote_parent_not_sampled:
            always_on:
          local_parent_sampled:
            always_off:
          local_parent_not_sampled:
            always_on:
        """;

    private const string Nested =
        """
        parent_based:
          root:
            parent_based:
              root:
                always_record:
                  root:
                    trace_id_ratio_based:
                      ratio: 0.1
        """;

    private static readonly string AllChildrenDescription = new ParentBasedSampler(
        new AlwaysOffSampler(),
        new AlwaysOffSampler(),
        new AlwaysOnSampler(),
        new AlwaysOffSampler(),
        new AlwaysOnSampler()).Description;

    private static readonly string NestedDescription =
        new ParentBasedSampler(new ParentBasedSampler(new AlwaysRecordSampler(new TraceIdRatioBasedSampler(0.1)))).Description;

    private static readonly string[] UnbuildableNames = ["jaeger_remote/development", "composite/development", "probability/development", "x_undefined"];
    private static readonly bool[] Nestings = [false, true];

    // Each row is a sampler written as YAML, and the description of the sampler that core builds
    // for the same settings.
    public static IEnumerable<TheoryDataRow<string, string>> Samplers =>
    [
        new("{always_on: }", new AlwaysOnSampler().Description) { TestDisplayName = "always_on" },
        new("{always_off: }", new AlwaysOffSampler().Description) { TestDisplayName = "always_off" },
        new("{trace_id_ratio_based: }", new TraceIdRatioBasedSampler(1).Description) { TestDisplayName = "ratio null value" },
        new("{trace_id_ratio_based: {}}", new TraceIdRatioBasedSampler(1).Description) { TestDisplayName = "ratio omitted" },
        new("{trace_id_ratio_based: {ratio: }}", new TraceIdRatioBasedSampler(1).Description) { TestDisplayName = "ratio null" },
        new("{trace_id_ratio_based: {ratio: 0}}", new TraceIdRatioBasedSampler(0).Description) { TestDisplayName = "ratio integer 0" },
        new("{trace_id_ratio_based: {ratio: 1}}", new TraceIdRatioBasedSampler(1).Description) { TestDisplayName = "ratio integer 1" },
        new("{trace_id_ratio_based: {ratio: 0.25}}", new TraceIdRatioBasedSampler(0.25).Description) { TestDisplayName = "ratio 0.25" },
        new("{always_record: {root: {always_off: }}}", new AlwaysRecordSampler(new AlwaysOffSampler()).Description) { TestDisplayName = "always_record" },
        new("{parent_based: }", new ParentBasedSampler(new AlwaysOnSampler()).Description) { TestDisplayName = "parent_based null value" },
        new("{parent_based: {}}", new ParentBasedSampler(new AlwaysOnSampler()).Description) { TestDisplayName = "parent_based defaults" },
        new("{parent_based: {root: {trace_id_ratio_based: {ratio: 0.5}}}}", new ParentBasedSampler(new TraceIdRatioBasedSampler(0.5)).Description) { TestDisplayName = "parent_based root only" },
        new(AllChildren, AllChildrenDescription) { TestDisplayName = "parent_based all children" },
        new(Nested, NestedDescription) { TestDisplayName = "nested" },
    ];

    public static IEnumerable<TheoryDataRow<string, bool>> UnbuildableSamplers =>
        from name in UnbuildableNames
        from nested in Nestings
        select new TheoryDataRow<string, bool>(name, nested) { TestDisplayName = $"{name} {(nested ? "nested" : "at the root")}" };

    [Theory]
    [MemberData(nameof(Samplers))]
    public void Sampler_IsBuiltFromTheDocument(string sampler, string expectedDescription)
    {
        Sampler? chosen = null;
        using var yamlFile = CreateDocument(sampler);
        using var provider = CreateServices(yamlFile.Path, s => s.ConfigureOpenTelemetryTracerProvider(b => b.ConfigureSampler((_, current) => chosen = current)));

        _ = provider.GetRequiredService<TracerProvider>();

        Assert.NotNull(chosen);
        Assert.Equal(expectedDescription, chosen.Description);
    }

    [Theory]
    [MemberData(nameof(UnbuildableSamplers))]
    public void UnbuildableSampler_FailsAtProviderBuild(string name, bool nested)
    {
        var sampler = nested
            ? $"""
              parent_based:
                root:
                  {name}:
              """
            : $"{{{name}: }}";
        var path = nested ? $"parent_based.root.{name}" : $"tracer_provider.sampler.{name}";
        using var yamlFile = CreateDocument(sampler);
        using var provider = CreateServices(yamlFile.Path);

        var exception = Assert.Throws<DeclarativeConfigurationException>(provider.GetRequiredService<TracerProvider>);

        Assert.Contains($"'{path}' (line {(nested ? 10 : 7)}, column", exception.Message, StringComparison.Ordinal);
        Assert.Contains("is a sampler", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("register", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnbuildableSampler_AtTheRoot_AppearsWithTheOtherUnsupportedSettings()
    {
        using var yamlFile = CreateDocument("{x_one: }");
        using var provider = CreateServices(yamlFile.Path);

        var exception = Assert.Throws<DeclarativeConfigurationException>(provider.GetRequiredService<TracerProvider>);

        Assert.Contains("x_one", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RegisteredProvider_BuildsAnUndefinedSamplerAtTheRootAndNested()
    {
        var log = new List<string>();
        using var yamlFile = CreateDocument(
            """
            parent_based:
              root:
                test_sampler:
                  label: root
              local_parent_sampled:
                test_sampler:
                  label: local
            """);
        using var provider = CreateServices(yamlFile.Path, s => s.AddPluginComponentProvider(TestSampler, new LifecycleSamplerProvider(log, throwOnDispose: false)));

        provider.GetRequiredService<TracerProvider>().Dispose();

        // The parent-based sampler shuts down every child it was given.
        Assert.Contains(log, entry => entry.StartsWith("root.Shutdown", StringComparison.Ordinal));
        Assert.Contains(log, entry => entry.StartsWith("local.Shutdown", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false, false, true, SamplingDecision.RecordAndSample)]
    [InlineData(true, true, false, SamplingDecision.Drop)]
    [InlineData(true, false, false, SamplingDecision.RecordAndSample)]
    [InlineData(false, true, false, SamplingDecision.Drop)]
    [InlineData(false, false, false, SamplingDecision.RecordAndSample)]
    public void ParentBased_RoutesEachParentKindToItsOwnChild(bool isRemote, bool sampled, bool noParent, SamplingDecision expected)
    {
        Sampler? chosen = null;
        using var yamlFile = CreateDocument(
            """
            parent_based:
              root:
                always_on:
              remote_parent_sampled:
                always_off:
              remote_parent_not_sampled:
                always_on:
              local_parent_sampled:
                always_off:
              local_parent_not_sampled:
                always_on:
            """);
        using var provider = CreateServices(yamlFile.Path, s => s.ConfigureOpenTelemetryTracerProvider(b => b.ConfigureSampler((_, current) => chosen = current)));
        _ = provider.GetRequiredService<TracerProvider>();

        var decision = Decide(chosen!, parent: !noParent, isRemote, sampled);

        Assert.Equal(expected, decision);
    }

    [Fact]
    public void ParentBased_OmittedChildren_UseTheSchemaDefaults()
    {
        Sampler? chosen = null;
        using var yamlFile = CreateDocument("{parent_based: {local_parent_sampled: {always_off: }}}");
        using var provider = CreateServices(yamlFile.Path, s => s.ConfigureOpenTelemetryTracerProvider(b => b.ConfigureSampler((_, current) => chosen = current)));
        _ = provider.GetRequiredService<TracerProvider>();

        // Only the local sampled parent was configured. The other four follow the schema.
        Assert.Equal(SamplingDecision.RecordAndSample, Decide(chosen!, parent: false, remote: false, sampled: false));
        Assert.Equal(SamplingDecision.RecordAndSample, Decide(chosen!, parent: true, remote: true, sampled: true));
        Assert.Equal(SamplingDecision.Drop, Decide(chosen!, parent: true, remote: true, sampled: false));
        Assert.Equal(SamplingDecision.Drop, Decide(chosen!, parent: true, remote: false, sampled: true));
        Assert.Equal(SamplingDecision.Drop, Decide(chosen!, parent: true, remote: false, sampled: false));
    }

    [Fact]
    public void RegisteredProvider_Exception_PropagatesUnchanged()
    {
        var thrown = new InvalidOperationException("provider failure");
        using var yamlFile = CreateDocument("{parent_based: {root: {test_sampler: }}}");
        using var provider = CreateServices(yamlFile.Path, s => s.AddPluginComponentProvider(TestSampler, new ThrowingSamplerProvider(thrown)));

        var exception = Assert.Throws<InvalidOperationException>(provider.GetRequiredService<TracerProvider>);

        Assert.Same(thrown, exception);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RegisteredProvider_ConflictsWithABuiltInSampler_Throws(bool registerBefore)
    {
        using var yamlFile = CreateDocument("{always_off: }");
        var services = new ServiceCollection();
        var builder = services.AddOpenTelemetry();
        var provider = new LifecycleSamplerProvider([], throwOnDispose: false);

        InvalidOperationException exception;
        if (registerBefore)
        {
            services.AddPluginComponentProvider("always_off", provider);
            exception = Assert.Throws<InvalidOperationException>(() => builder.UseDeclarativeConfiguration(yamlFile.Path));
        }
        else
        {
            builder.UseDeclarativeConfiguration(yamlFile.Path);
            exception = Assert.Throws<InvalidOperationException>(() => services.AddPluginComponentProvider("always_off", provider));
        }

        Assert.Contains("always_off", exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(AlwaysOffSamplerProvider), exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(LifecycleSamplerProvider), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void YamlAnchor_UsedTwice_BuildsTwoInstances()
    {
        var log = new List<string>();
        using var yamlFile = CreateDocument(
            """
            parent_based:
              root: &s
                test_sampler:
                  label: shared
              local_parent_sampled: *s
            """);
        using var provider = CreateServices(yamlFile.Path, s => s.AddPluginComponentProvider(TestSampler, new LifecycleSamplerProvider(log, throwOnDispose: false)));

        using var tracerProvider = provider.GetRequiredService<TracerProvider>();
        tracerProvider.Dispose();

        Assert.Equal(2, log.Count(entry => entry.StartsWith("shared.Shutdown", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void FailureBuildingALaterChild_ShutsDownEarlierSamplers(bool throwOnDispose, bool throwOnShutdown)
    {
        var log = new List<string>();
        var thrown = new InvalidOperationException("provider failure");
        using var yamlFile = CreateDocument(
            """
            parent_based:
              root:
                test_sampler:
                  label: first
              remote_parent_sampled:
                test_sampler:
                  label: second
              local_parent_sampled:
                test_failure:
            """);
        using var provider = CreateServices(
            yamlFile.Path,
            s =>
            {
                s.AddPluginComponentProvider(TestSampler, new LifecycleSamplerProvider(log, throwOnDispose, throwOnShutdown: throwOnShutdown));
                s.AddPluginComponentProvider("test_failure", new ThrowingSamplerProvider(thrown));
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
            Assert.Equal(typeof(LifecycleSampler).ToString(), failure.Payload![0]);
            Assert.Contains("dispose failure", Assert.IsType<string>(failure.Payload[1]), StringComparison.Ordinal);
        }
        else
        {
            Assert.Empty(cleanupFailures);
        }
    }

    [Fact]
    public void FailureBuildingAlwaysRecordRoot_LeavesNothingToClean()
    {
        var log = new List<string>();
        var thrown = new InvalidOperationException("provider failure");
        using var yamlFile = CreateDocument("{always_record: {root: {test_failure: }}}");
        using var provider = CreateServices(
            yamlFile.Path,
            s =>
            {
                s.AddPluginComponentProvider(TestSampler, new LifecycleSamplerProvider(log, throwOnDispose: false));
                s.AddPluginComponentProvider("test_failure", new ThrowingSamplerProvider(thrown));
            });

        Assert.Same(thrown, Assert.Throws<InvalidOperationException>(provider.GetRequiredService<TracerProvider>));
        Assert.Empty(log);
    }

    [Fact]
    public void AlwaysOff_ExportsNothing()
    {
        var source = NewSourceName();
        var exported = new List<Activity>();
        using var yamlFile = CreateDocument("{always_off: }", source);
        using var provider = CreateServices(yamlFile.Path, exported: exported);
        _ = provider.GetRequiredService<TracerProvider>();

        Assert.False(StartAndStop(source), "The always_off sampler should drop the span.");
        Assert.Empty(exported);
    }

    [Fact]
    public void AlwaysOn_ExportsSpans()
    {
        var source = NewSourceName();
        var exported = new List<Activity>();
        using var yamlFile = CreateDocument("{always_on: }", source);
        using var provider = CreateServices(yamlFile.Path, exported: exported);
        _ = provider.GetRequiredService<TracerProvider>();

        StartAndStop(source);

        Assert.Single(exported, a => a.Source.Name == source);
    }

    [Fact]
    public void AlwaysRecord_WithAlwaysOffRoot_RecordsSpansWithoutExportingThem()
    {
        var source = NewSourceName();
        var exported = new List<Activity>();
        using var seen = new SeenProcessor();
        using var yamlFile = CreateDocument("{always_record: {root: {always_off: }}}", source);
        using var provider = CreateServices(
            yamlFile.Path,
            s => s.ConfigureOpenTelemetryTracerProvider(b => b.AddProcessor(seen)),
            exported);
        _ = provider.GetRequiredService<TracerProvider>();

        Assert.True(StartAndStop(source), "The always_record sampler should record the span even when its always_off root drops it.");

        Assert.Equal([true], seen.IsAllDataRequested);
        Assert.Empty(exported);
    }

    [Theory]
    [MemberData(nameof(DeclarativeTracerProviderTests.RegistrationPaths), MemberType = typeof(DeclarativeTracerProviderTests))]
    public void CodeSampler_SetBeforeTheCall_WinsAndIsReported(DeclarativeTracerProviderTests.RegistrationPath path)
    {
        var source = NewSourceName();
        var exported = new List<Activity>();
        using var yamlFile = CreateDocument("{trace_id_ratio_based: {ratio: 0.25}}", source);
        using var listener = CreateListener();
        using var built = Build(
            path,
            yamlFile.Path,
            before: b => b.WithTracing(t => t.SetSampler(new AlwaysOffSampler())),
            after: b => b.Services.AddPluginComponentProvider(MemoryExporter, new MemoryExporterProvider(exported)));

        _ = built.Provider.GetRequiredService<TracerProvider>();

        Assert.False(StartAndStop(source), "The always_off sampler set in code before declarative configuration should take precedence and drop the span.");
        AssertReplaced(listener, typeof(AlwaysOffSampler));
    }

    [Theory]
    [MemberData(nameof(DeclarativeTracerProviderTests.RegistrationPaths), MemberType = typeof(DeclarativeTracerProviderTests))]
    public void CodeSampler_SetAfterTheCall_WinsAndIsReported(DeclarativeTracerProviderTests.RegistrationPath path)
    {
        var source = NewSourceName();
        var exported = new List<Activity>();
        using var yamlFile = CreateDocument("{always_on: }", source);
        using var listener = CreateListener();
        using var built = Build(
            path,
            yamlFile.Path,
            before: null,
            after: b =>
            {
                b.Services.AddPluginComponentProvider(MemoryExporter, new MemoryExporterProvider(exported));
                b.WithTracing(t => t.SetSampler(new AlwaysOffSampler()));
            });

        _ = built.Provider.GetRequiredService<TracerProvider>();

        Assert.False(StartAndStop(source), "The always_off sampler set in code after declarative configuration should take precedence and drop the span.");
        AssertReplaced(listener, typeof(AlwaysOffSampler));
    }

    [Fact]
    public void CodeSamplerFactory_SetAfterTheCall_WinsAndIsReported()
    {
        using var yamlFile = CreateDocument("{always_on: }");
        using var listener = CreateListener();
        using var provider = CreateServices(
            yamlFile.Path,
            s => s.ConfigureOpenTelemetryTracerProvider(b => b.SetSampler(_ => new AlwaysOffSampler())));

        _ = provider.GetRequiredService<TracerProvider>();

        AssertReplaced(listener, typeof(AlwaysOffSampler));
    }

    [Fact]
    public void ConfigureSampler_InCode_ReceivesTheDocumentSamplerAndCanWrapIt()
    {
        var source = NewSourceName();
        var exported = new List<Activity>();
        Sampler? received = null;
        using var yamlFile = CreateDocument("{trace_id_ratio_based: {ratio: 0.25}}", source);
        using var listener = CreateListener();
        using var provider = CreateServices(
            yamlFile.Path,
            s => s.ConfigureOpenTelemetryTracerProvider(b => b.ConfigureSampler((_, current) =>
            {
                received = current;
                return new AlwaysRecordSampler(current);
            })),
            exported);

        _ = provider.GetRequiredService<TracerProvider>();

        Assert.Equal(new TraceIdRatioBasedSampler(0.25).Description, received?.Description);
        Assert.DoesNotContain(listener.Messages, e => e.EventId == SamplerReplacedByCodeEventId);
    }

    [Fact]
    public void CallbackRegisteredBeforeTheCall_ReceivesTheDocumentSampler()
    {
        Sampler? received = null;
        var services = new ServiceCollection();
        services.ConfigureOpenTelemetryTracerProvider(b => b.ConfigureSampler((_, current) => received = current));
        using var yamlFile = CreateDocument("{always_off: }");
        services.AddOpenTelemetry().UseDeclarativeConfiguration(yamlFile.Path);
        services.AddPluginComponentProvider(MemoryExporter, new MemoryExporterProvider([]));
        using var provider = services.BuildServiceProvider();

        _ = provider.GetRequiredService<TracerProvider>();

        // The document's callback runs first, so a callback registered earlier already sees its sampler.
        Assert.IsType<AlwaysOffSampler>(received);
    }

    [Fact]
    public void DocumentSampler_AloneOrOmitted_IsNotReported()
    {
        var source = NewSourceName();
        var exported = new List<Activity>();
        using var listener = CreateListener();

        using (var yamlFile = CreateDocument("{always_on: }", source))
        using (var provider = CreateServices(yamlFile.Path, exported: exported))
        {
            _ = provider.GetRequiredService<TracerProvider>();
            StartAndStop(source);
        }

        Assert.Single(exported, a => a.Source.Name == source);

        using (var yamlFile = CreateDocument(null))
        using (var provider = CreateServices(yamlFile.Path, s => s.ConfigureOpenTelemetryTracerProvider(b => b.SetSampler(new AlwaysOffSampler()))))
        {
            _ = provider.GetRequiredService<TracerProvider>();
        }

        Assert.DoesNotContain(listener.Messages, e => e.EventId == SamplerReplacedByCodeEventId);
    }

    [Fact]
    public void ConfigurationApplier_WithoutADocumentAccessor_DoesNothing()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var builder = Sdk.CreateTracerProviderBuilder();

        new DeclarativeSamplerConfigurationApplier().ConfigureBuilder(services, builder);
    }

    [Fact]
    public void ConfigurationApplier_ForADocumentWithoutTracerProvider_DoesNothing()
    {
        Sampler? chosen = null;
        using var yamlFile = DeclarativeYamlTestFile.CreateYamlFile("""
            file_format: "1.2"

            """);
        using var provider = CreateServices(
            yamlFile.Path,
            s =>
            {
                s.AddOpenTelemetry().WithTracing();
                s.ConfigureOpenTelemetryTracerProvider(b => b.ConfigureSampler((_, current) => chosen = current));
            });

        _ = provider.GetRequiredService<TracerProvider>();

        Assert.Equal(new ParentBasedSampler(new AlwaysOnSampler()).Description, chosen?.Description);
    }

    [Fact]
    public void OmittedSampler_LeavesCoreDefault()
    {
        Sampler? chosen = null;
        using var yamlFile = CreateDocument(null);
        using var provider = CreateServices(yamlFile.Path, s => s.ConfigureOpenTelemetryTracerProvider(b => b.ConfigureSampler((_, current) => chosen = current)));

        _ = provider.GetRequiredService<TracerProvider>();

        Assert.Equal(new ParentBasedSampler(new AlwaysOnSampler()).Description, chosen?.Description);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EarlierSamplerKeys_AreMasked(bool documentSetsSampler)
    {
        Sampler? chosen = null;
        using var yamlFile = CreateDocument(documentSetsSampler ? "{always_on: }" : null);
        using var configuration = new ConfigurationManager();
        configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["OTEL_TRACES_SAMPLER"] = "always_off" });
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddOpenTelemetry().UseDeclarativeConfiguration(yamlFile.Path);
        services.AddPluginComponentProvider(MemoryExporter, new MemoryExporterProvider([]));
        services.ConfigureOpenTelemetryTracerProvider(b => b.ConfigureSampler((_, current) => chosen = current));
        using var provider = services.BuildServiceProvider();

        _ = provider.GetRequiredService<TracerProvider>();

        var expected = documentSetsSampler ? new AlwaysOnSampler().Description : new ParentBasedSampler(new AlwaysOnSampler()).Description;
        Assert.Equal(expected, chosen?.Description);
    }

    [Fact]
    public void ReplacedDocumentSampler_IsNeverCreated()
    {
        var log = new List<string>();
        var created = 0;
        using var yamlFile = CreateDocument("{test_sampler: {label: doc}}");
        using var provider = CreateServices(
            yamlFile.Path,
            s =>
            {
                s.AddPluginComponentProvider(TestSampler, new LifecycleSamplerProvider(log, throwOnDispose: false, () => created++));
                s.ConfigureOpenTelemetryTracerProvider(b => b.SetSampler(new AlwaysOffSampler()));
            });

        _ = provider.GetRequiredService<TracerProvider>();

        Assert.Equal(0, created);
        Assert.Empty(log);
    }

    [Fact]
    public void WrappedDocumentSampler_IsNotDisposedByThePackage()
    {
        var log = new List<string>();
        using var yamlFile = CreateDocument("{test_sampler: {label: doc}}");
        using var listener = CreateListener();
        using var provider = CreateServices(
            yamlFile.Path,
            s =>
            {
                s.AddPluginComponentProvider(TestSampler, new LifecycleSamplerProvider(log, throwOnDispose: false));
                s.ConfigureOpenTelemetryTracerProvider(b => b.ConfigureSampler((_, current) => new AlwaysRecordSampler(current)));
            });

        _ = provider.GetRequiredService<TracerProvider>();

        Assert.Empty(log);
        Assert.DoesNotContain(listener.Messages, e => e.EventId == SamplerReplacedByCodeEventId);
    }

    [Fact]
    public void DeclarativeSamplerPlaceholder_ThrowsIfUsed()
    {
        var placeholder = new DeclarativeSamplerPlaceholder();

        Assert.Throws<InvalidOperationException>(() => placeholder.ShouldSample(default));
    }

    [Fact]
    public void UsedDocumentSampler_IsReleasedByTheProvider_NotByThePackage()
    {
        var log = new List<string>();
        using var yamlFile = CreateDocument("{test_sampler: {label: doc}}");
        using var provider = CreateServices(yamlFile.Path, s => s.AddPluginComponentProvider(TestSampler, new LifecycleSamplerProvider(log, throwOnDispose: false)));

        var tracerProvider = provider.GetRequiredService<TracerProvider>();
        Assert.Empty(log);

        tracerProvider.Dispose();

        Assert.Equal(1, log.Count(entry => entry.StartsWith("doc.Shutdown", StringComparison.Ordinal)));
        Assert.Contains("doc.Dispose", log);
    }

    private static SamplingDecision Decide(Sampler sampler, bool parent, bool remote, bool sampled)
    {
        var traceId = ActivityTraceId.CreateRandom();
        var parentContext = parent
            ? new ActivityContext(traceId, ActivitySpanId.CreateRandom(), sampled ? ActivityTraceFlags.Recorded : ActivityTraceFlags.None, isRemote: remote)
            : default;

        return sampler.ShouldSample(new SamplingParameters(parentContext, traceId, "work", ActivityKind.Internal)).Decision;
    }

    private static void AssertReplaced(TestEventListener listener, Type replacingType)
    {
        var replaced = Assert.Single(listener.Messages, e => e.EventId == SamplerReplacedByCodeEventId);
        Assert.Equal(EventLevel.Warning, replaced.Level);
        Assert.Equal(replacingType.ToString(), replaced.Payload![4]);
        Assert.StartsWith("tracer_provider.sampler.", Assert.IsType<string>(replaced.Payload[1]), StringComparison.Ordinal);
        Assert.Equal(7L, replaced.Payload[2]);
    }

    private static DeclarativeYamlTestFile CreateDocument(string? sampler, string? source = null)
    {
        var samplerLines = sampler?.Split('\n');
        var document =
            $"""
            file_format: "1.2"
            tracer_provider:
              processors:
                - simple:
                    exporter:
                      {MemoryExporter}:

            """
            + (samplerLines is null
                ? string.Empty
                : samplerLines.Length > 1
                    ? $"  sampler:\n    {string.Join("\n    ", samplerLines)}\n"
                    : $"  sampler: {sampler}\n");

        if (source is not null)
        {
            document +=
                $"""
                  tracer_configurator/development:
                    default_config:
                      enabled: false
                    tracers:
                      - name: {source}
                        config:
                          enabled: true

                """;
        }

        return DeclarativeYamlTestFile.CreateYamlFile(document);
    }

    private static string NewSourceName() => "DeclarativeSampler." + Guid.NewGuid().ToString("N");

    // A dropped span still gets an Activity, to carry the trace context, so ask whether it records.
    private static bool StartAndStop(string sourceName)
    {
        using var source = new ActivitySource(sourceName);
        using var activity = source.StartActivity("work");
        return activity?.IsAllDataRequested == true;
    }

    private static TestEventListener CreateListener() =>
        new(OpenTelemetryDeclarativeConfigurationEventSource.Log, EventLevel.Verbose);

    private static ServiceProvider CreateServices(string yamlPath, Action<IServiceCollection>? configureServices = null, List<Activity>? exported = null)
    {
        var services = new ServiceCollection();
        services.AddOpenTelemetry().UseDeclarativeConfiguration(yamlPath);
        services.AddPluginComponentProvider(MemoryExporter, new MemoryExporterProvider(exported ?? []));
        configureServices?.Invoke(services);
        return services.BuildServiceProvider();
    }

    private static BuiltServices Build(
        DeclarativeTracerProviderTests.RegistrationPath path,
        string yamlPath,
        Action<IOpenTelemetryBuilder>? before,
        Action<IOpenTelemetryBuilder>? after)
    {
        switch (path)
        {
            case DeclarativeTracerProviderTests.RegistrationPath.HostApplicationBuilder:
                {
                    var builder = Host.CreateApplicationBuilder();
                    var openTelemetry = builder.AddOpenTelemetry();
                    before?.Invoke(openTelemetry);
                    openTelemetry.UseDeclarativeConfiguration(yamlPath);
                    after?.Invoke(openTelemetry);
                    var host = builder.Build();
                    return new(host.Services, host);
                }

            case DeclarativeTracerProviderTests.RegistrationPath.ServicesOnHostApplicationBuilder:
                {
                    var builder = Host.CreateApplicationBuilder();
                    var openTelemetry = builder.Services.AddOpenTelemetry();
                    before?.Invoke(openTelemetry);
                    openTelemetry.UseDeclarativeConfiguration(yamlPath);
                    after?.Invoke(openTelemetry);
                    var host = builder.Build();
                    return new(host.Services, host);
                }

            case DeclarativeTracerProviderTests.RegistrationPath.HostBuilder:
                {
                    var host = new HostBuilder()
                        .ConfigureServices(services =>
                        {
                            var openTelemetry = services.AddOpenTelemetry();
                            before?.Invoke(openTelemetry);
                            openTelemetry.UseDeclarativeConfiguration(yamlPath);
                            after?.Invoke(openTelemetry);
                        })
                        .Build();
                    return new(host.Services, host);
                }

            case DeclarativeTracerProviderTests.RegistrationPath.ServiceCollection:
            default:
                {
                    var services = new ServiceCollection();
                    var openTelemetry = services.AddOpenTelemetry();
                    before?.Invoke(openTelemetry);
                    openTelemetry.UseDeclarativeConfiguration(yamlPath);
                    after?.Invoke(openTelemetry);
                    var provider = services.BuildServiceProvider();
                    return new(provider, provider);
                }
        }
    }

    private sealed class BuiltServices(IServiceProvider provider, IDisposable owner) : IDisposable
    {
        public IServiceProvider Provider { get; } = provider;

        public void Dispose() => owner.Dispose();
    }

    private sealed class MemoryExporterProvider(ICollection<Activity> exported) : PluginComponentProvider<BaseExporter<Activity>>
    {
        public override BaseExporter<Activity> Create(ConfigProperties properties, IServiceProvider serviceProvider) =>
            new InMemoryExporter<Activity>(exported);
    }

    private sealed class LifecycleSamplerProvider(List<string> log, bool throwOnDispose, Action? onCreate = null, bool throwOnShutdown = false) : PluginComponentProvider<Sampler>
    {
        public override Sampler Create(ConfigProperties properties, IServiceProvider serviceProvider)
        {
            onCreate?.Invoke();
            return new LifecycleSampler(properties.GetString("label").Value ?? "unlabelled", log, throwOnDispose, throwOnShutdown);
        }
    }

    private sealed class ThrowingSamplerProvider(Exception exception) : PluginComponentProvider<Sampler>
    {
        public override Sampler Create(ConfigProperties properties, IServiceProvider serviceProvider) =>
            throw exception;
    }

    private sealed class LifecycleSampler : Sampler, IDisposable
    {
        private readonly string label;
        private readonly List<string> log;
        private readonly bool throwOnDispose;
        private readonly bool throwOnShutdown;

        public LifecycleSampler(string label, List<string> log, bool throwOnDispose, bool throwOnShutdown)
        {
            this.label = label;
            this.log = log;
            this.throwOnDispose = throwOnDispose;
            this.throwOnShutdown = throwOnShutdown;
            this.Description = label;
        }

        public override SamplingResult ShouldSample(in SamplingParameters samplingParameters) =>
            new(SamplingDecision.RecordAndSample);

        public void Dispose()
        {
            lock (this.log)
            {
                this.log.Add($"{this.label}.Dispose");
            }

            if (this.throwOnDispose && this.label == "first")
            {
                throw new InvalidOperationException("dispose failure");
            }
        }

        protected override bool OnShutdown(int timeoutMilliseconds)
        {
            lock (this.log)
            {
                this.log.Add($"{this.label}.Shutdown({timeoutMilliseconds})");
            }

            if (this.throwOnShutdown && this.label == "first")
            {
                throw new InvalidOperationException("shutdown failure");
            }

            return true;
        }
    }

    private sealed class SeenProcessor : BaseProcessor<Activity>
    {
        public List<bool> IsAllDataRequested { get; } = [];

        public override void OnEnd(Activity data)
        {
            lock (this.IsAllDataRequested)
            {
                this.IsAllDataRequested.Add(data.IsAllDataRequested);
            }
        }
    }
}

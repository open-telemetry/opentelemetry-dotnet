// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Internal;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Trace.Tests;

public sealed class SamplerOptionsTests
{
    private const int TracerProviderSdkEventId = 46;
    private const int TracesSamplerConfigInvalidEventId = 54;
    private const int TracesSamplerArgConfigInvalidEventId = 55;

    private const string IgnoredSamplerMessageFragment = "has been ignored because a value";

    [Fact]
    public void SamplerTypeConstants_MatchSpecValues()
    {
        Assert.Equal("always_on", SamplerOptions.AlwaysOnType);
        Assert.Equal("always_off", SamplerOptions.AlwaysOffType);
        Assert.Equal("traceidratio", SamplerOptions.TraceIdRatioType);
        Assert.Equal("parentbased_always_on", SamplerOptions.ParentBasedAlwaysOnType);
        Assert.Equal("parentbased_always_off", SamplerOptions.ParentBasedAlwaysOffType);
        Assert.Equal("parentbased_traceidratio", SamplerOptions.ParentBasedTraceIdRatioType);
    }

    [Fact]
    public void NoConfigurationKeys_PropertiesAreNull()
    {
        var options = CreateOptions();

        Assert.Null(options.Type);
        Assert.Null(options.TraceIdRatio);
        Assert.Null(options.Argument);
    }

    [Fact]
    public void TracesSampler_ReadFromConfiguration()
    {
        var options = CreateOptions((SamplerOptions.TracesSamplerConfigKey, SamplerOptions.AlwaysOnType));

        Assert.Equal(SamplerOptions.AlwaysOnType, options.Type);
    }

    [Theory]
    [InlineData("0.5", 0.5)]
    [InlineData("1", 1.0)]
    [InlineData("0", 0.0)]
    [InlineData("2.0", 2.0)] // Out of range values are parsed here and rejected when read.
    public void TracesSamplerArg_WithRatioType_ParsedIntoTraceIdRatio(string argValue, double expected)
    {
        var options = CreateOptions(
            (SamplerOptions.TracesSamplerConfigKey, SamplerOptions.TraceIdRatioType),
            (SamplerOptions.TracesSamplerArgConfigKey, argValue));

        Assert.Equal(expected, options.TraceIdRatio);
        Assert.Equal(argValue, options.Argument);
    }

    [Fact]
    public void TracesSamplerArg_WithoutRatioType_TraceIdRatioNotParsed()
    {
        // OTEL_TRACES_SAMPLER_ARG is sampler-specific. The ratio interpretation only applies
        // to traceidratio and parentbased_traceidratio, so parsing is skipped for other types.
        var options = CreateOptions((SamplerOptions.TracesSamplerArgConfigKey, "0.5"));

        Assert.Null(options.TraceIdRatio);
        Assert.Equal("0.5", options.Argument);
    }

    [Fact]
    public void TracesSamplerArg_Unparsable_LeavesTraceIdRatioNullAndRetainsArgument()
    {
        var options = CreateOptions(
            (SamplerOptions.TracesSamplerConfigKey, SamplerOptions.TraceIdRatioType),
            (SamplerOptions.TracesSamplerArgConfigKey, "banana"));

        // TraceIdRatio is null because the value could not be parsed. Argument retains the
        // verbatim string so it can be reported if the configured sampler uses it.
        Assert.Null(options.TraceIdRatio);
        Assert.Equal("banana", options.Argument);
    }

    [Fact]
    public void ParameterlessConstructor_ReadsEnvironmentVariables()
    {
        using var environment = EnvironmentVariableScope.Create(
            [
                (SamplerOptions.TracesSamplerConfigKey, SamplerOptions.TraceIdRatioType),
                (SamplerOptions.TracesSamplerArgConfigKey, "0.25"),
            ]);

        var options = new SamplerOptions();

        Assert.Equal(SamplerOptions.TraceIdRatioType, options.Type);
        Assert.Equal(0.25, options.TraceIdRatio);
    }

    [Fact]
    public void ResolvedFromServiceCollectionWithoutTracing()
    {
        // The options type must be usable through the standard options pipeline, which
        // requires a public parameterless constructor.
        var services = new ServiceCollection();
        services.AddOptions();
        services.Configure<SamplerOptions>(o => o.Type = SamplerOptions.AlwaysOnType);

        using var serviceProvider = services.BuildServiceProvider();

        Assert.Equal(
            SamplerOptions.AlwaysOnType,
            serviceProvider.GetRequiredService<IOptions<SamplerOptions>>().Value.Type);
    }

    [Fact]
    public void Configure_OverridesEnvironmentVariableType()
    {
        using var environment = EnvironmentVariableScope.Create(
            SamplerOptions.TracesSamplerConfigKey, SamplerOptions.AlwaysOffType);

        using var tracerProvider = BuildTracerProvider(
            configureServices: s => s.Configure<SamplerOptions>(o => o.Type = SamplerOptions.AlwaysOnType));

        Assert.Equal("AlwaysOnSampler", tracerProvider.Sampler.Description);
    }

    [Fact]
    public void Configure_OverridesEnvironmentVariableTraceIdRatio()
    {
        using var environment = EnvironmentVariableScope.Create(
            [
                (SamplerOptions.TracesSamplerConfigKey, SamplerOptions.TraceIdRatioType),
                (SamplerOptions.TracesSamplerArgConfigKey, "0.1"),
            ]);

        using var tracerProvider = BuildTracerProvider(
            configureServices: s => s.Configure<SamplerOptions>(o => o.TraceIdRatio = 0.9));

        Assert.Equal("TraceIdRatioBasedSampler{0.900000}", tracerProvider.Sampler.Description);
    }

    [Fact]
    public void Configure_BindsFromConfigurationSection()
    {
        var configuration = BuildConfiguration(
            ("Sampler:Type", SamplerOptions.TraceIdRatioType),
            ("Sampler:TraceIdRatio", "0.4"));

        using var tracerProvider = BuildTracerProvider(
            configureServices: s => s.Configure<SamplerOptions>(configuration.GetSection("Sampler")));

        Assert.Equal("TraceIdRatioBasedSampler{0.400000}", tracerProvider.Sampler.Description);
    }

    [Fact]
    public void NoConfiguration_UsesDefaultSamplerWithoutDiagnostics()
    {
        using var eventListener = new TestEventListener(OpenTelemetrySdkEventSource.Log);

        using var tracerProvider = BuildTracerProvider(configuration: BuildConfiguration());

        Assert.Equal("ParentBased{AlwaysOnSampler}", tracerProvider.Sampler.Description);
        Assert.DoesNotContain(eventListener.Messages, IsSamplerIgnoredEvent);
    }

    [Fact]
    public void ProgrammaticSamplerWithoutConfiguration_NoIgnoredDiagnostic()
    {
        using var eventListener = new TestEventListener(OpenTelemetrySdkEventSource.Log);

        using var tracerProvider = BuildTracerProvider(
            configuration: BuildConfiguration(),
            configureBuilder: b => b.SetSampler(new AlwaysOnSampler()));

        Assert.Equal("AlwaysOnSampler", tracerProvider.Sampler.Description);
        Assert.DoesNotContain(eventListener.Messages, IsSamplerIgnoredEvent);
    }

    [Fact]
    public void ProgrammaticSamplerWithConfiguration_EmitsIgnoredDiagnostic()
    {
        using var eventListener = new TestEventListener(OpenTelemetrySdkEventSource.Log);

        using var tracerProvider = BuildTracerProvider(
            configuration: BuildConfiguration((SamplerOptions.TracesSamplerConfigKey, SamplerOptions.AlwaysOnType)),
            configureBuilder: b => b.SetSampler(new AlwaysOffSampler()));

        Assert.Equal("AlwaysOffSampler", tracerProvider.Sampler.Description);
        Assert.Contains(eventListener.Messages, IsSamplerIgnoredEvent);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankSamplerType_TreatedAsUnsetWithoutDiagnostics(string samplerType)
    {
        using var eventListener = new TestEventListener(OpenTelemetrySdkEventSource.Log);

        using var tracerProvider = BuildTracerProvider(
            configuration: BuildConfiguration((SamplerOptions.TracesSamplerConfigKey, samplerType)));

        Assert.Equal("ParentBased{AlwaysOnSampler}", tracerProvider.Sampler.Description);
        Assert.DoesNotContain(
            eventListener.Messages,
            e => e.EventId == TracesSamplerConfigInvalidEventId);
    }

    [Theory]
    [InlineData(" always_off ", "AlwaysOffSampler")]
    [InlineData("	parentbased_always_off	", "ParentBased{AlwaysOffSampler}")]
    public void PaddedSamplerType_TrimmedBeforeMatching(string samplerType, string expectedDescription)
    {
        using var eventListener = new TestEventListener(OpenTelemetrySdkEventSource.Log);

        using var tracerProvider = BuildTracerProvider(
            configuration: BuildConfiguration((SamplerOptions.TracesSamplerConfigKey, samplerType)));

        Assert.Equal(expectedDescription, tracerProvider.Sampler.Description);
        Assert.DoesNotContain(
            eventListener.Messages,
            e => e.EventId == TracesSamplerConfigInvalidEventId);
    }

    [Fact]
    public void PaddedRatioSamplerType_AppliesConfiguredArgument()
    {
        var configuration = BuildConfiguration(
            (SamplerOptions.TracesSamplerConfigKey, " traceidratio "),
            (SamplerOptions.TracesSamplerArgConfigKey, "0.25"));

        Assert.Equal(0.25, new SamplerOptions(configuration).TraceIdRatio);

        using var tracerProvider = BuildTracerProvider(configuration);

        Assert.Equal("TraceIdRatioBasedSampler{0.250000}", tracerProvider.Sampler.Description);
    }

    [Fact]
    public void UnknownSamplerType_LogsAndUsesDefault()
    {
        using var eventListener = new TestEventListener(OpenTelemetrySdkEventSource.Log);

        using var tracerProvider = BuildTracerProvider(
            configuration: BuildConfiguration((SamplerOptions.TracesSamplerConfigKey, "unknown_sampler")));

        Assert.Equal("ParentBased{AlwaysOnSampler}", tracerProvider.Sampler.Description);
        AssertPayload(eventListener, TracesSamplerConfigInvalidEventId, "unknown_sampler");
    }

    [Fact]
    public void InvalidArgWithNonRatioSampler_NoDiagnostic()
    {
        // OTEL_TRACES_SAMPLER_ARG is only evaluated for ratio based samplers. Per the
        // specification each sampler defines its own expected input, so an invalid arg
        // combined with a sampler which ignores it must not produce a diagnostic.
        using var eventListener = new TestEventListener(OpenTelemetrySdkEventSource.Log);

        using var tracerProvider = BuildTracerProvider(
            configuration: BuildConfiguration(
                (SamplerOptions.TracesSamplerConfigKey, SamplerOptions.AlwaysOnType),
                (SamplerOptions.TracesSamplerArgConfigKey, "banana")));

        Assert.Equal("AlwaysOnSampler", tracerProvider.Sampler.Description);
        Assert.DoesNotContain(
            eventListener.Messages,
            e => e.EventId == TracesSamplerArgConfigInvalidEventId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NoArgWithRatioSampler_UsesDefaultRatioWithoutDiagnostics(string? argValue)
    {
        // The specification defines the default ratio as 1.0 when OTEL_TRACES_SAMPLER_ARG is not
        // set, so there is no invalid value to report.
        using var eventListener = new TestEventListener(OpenTelemetrySdkEventSource.Log);

        using var tracerProvider = BuildTracerProvider(
            configuration: BuildConfiguration(
                (SamplerOptions.TracesSamplerConfigKey, SamplerOptions.TraceIdRatioType),
                (SamplerOptions.TracesSamplerArgConfigKey, argValue)));

        Assert.Equal("TraceIdRatioBasedSampler{1.000000}", tracerProvider.Sampler.Description);
        Assert.DoesNotContain(
            eventListener.Messages,
            e => e.EventId == TracesSamplerArgConfigInvalidEventId);
    }

    // The configured value is always reported exactly as it was written, including a trailing
    // zero or a thousands separator which would be lost by formatting the parsed value.
    [Theory]
    [InlineData("banana", "banana")] // Unparsable.
    [InlineData("1.5", "1.5")] // Parsable but out of range.
    [InlineData("2.0", "2.0")]
    [InlineData("1,5", "1,5")] // Thousands separators are permitted, so this parses to 15.
    [InlineData("-0.1", "-0.1")]
    [InlineData("NaN", "NaN")]
    public void InvalidArgWithRatioSampler_LogsValueAndUsesDefaultRatio(string argValue, string expectedPayload)
    {
        using var eventListener = new TestEventListener(OpenTelemetrySdkEventSource.Log);

        using var tracerProvider = BuildTracerProvider(
            configuration: BuildConfiguration(
                (SamplerOptions.TracesSamplerConfigKey, SamplerOptions.TraceIdRatioType),
                (SamplerOptions.TracesSamplerArgConfigKey, argValue)));

        Assert.Equal("TraceIdRatioBasedSampler{1.000000}", tracerProvider.Sampler.Description);
        Assert.Single(eventListener.Messages, e => e.EventId == TracesSamplerArgConfigInvalidEventId);
        AssertPayload(eventListener, TracesSamplerArgConfigInvalidEventId, expectedPayload);
    }

    // A configured value is present in every case, to prove the diagnostic reports the
    // programmatic value which actually caused the fallback rather than the ignored one.
    [Theory]
    [InlineData("0.5", 2.0, "2")] // Configured value is usable, so the override is at fault.
    [InlineData("0.5", -0.1, "-0.1")]
    [InlineData("0.5", double.NaN, "NaN")]
    [InlineData("2.0", 3.0, "3")] // Both are unusable, but only the override is in effect.
    [InlineData("banana", 3.0, "3")]
    public void ProgrammaticInvalidTraceIdRatio_LogsEffectiveValueAndUsesDefaultRatio(
        string argValue,
        double traceIdRatio,
        string expectedPayload)
    {
        using var eventListener = new TestEventListener(OpenTelemetrySdkEventSource.Log);

        using var tracerProvider = BuildTracerProvider(
            configuration: BuildConfiguration((SamplerOptions.TracesSamplerArgConfigKey, argValue)),
            configureServices: s => s.Configure<SamplerOptions>(o =>
            {
                o.Type = SamplerOptions.TraceIdRatioType;
                o.TraceIdRatio = traceIdRatio;
            }));

        Assert.Equal("TraceIdRatioBasedSampler{1.000000}", tracerProvider.Sampler.Description);
        AssertPayload(eventListener, TracesSamplerArgConfigInvalidEventId, expectedPayload);
    }

    [Fact]
    public void ProgrammaticValidTraceIdRatio_OverridesUnusableConfiguredValueWithoutDiagnostics()
    {
        using var eventListener = new TestEventListener(OpenTelemetrySdkEventSource.Log);

        using var tracerProvider = BuildTracerProvider(
            configuration: BuildConfiguration((SamplerOptions.TracesSamplerArgConfigKey, "banana")),
            configureServices: s => s.Configure<SamplerOptions>(o =>
            {
                o.Type = SamplerOptions.TraceIdRatioType;
                o.TraceIdRatio = 0.25;
            }));

        Assert.Equal("TraceIdRatioBasedSampler{0.250000}", tracerProvider.Sampler.Description);
        Assert.DoesNotContain(
            eventListener.Messages,
            e => e.EventId == TracesSamplerArgConfigInvalidEventId);
    }

    [Fact]
    public void ProgrammaticTypeWithConfiguredArg_FallsBackToArgument()
    {
        // When Type is set programmatically and OTEL_TRACES_SAMPLER_ARG is in config but
        // OTEL_TRACES_SAMPLER is not, the constructor guard skips parsing. ReadTraceIdRatio
        // falls back to Argument so the ratio still takes effect.
        using var tracerProvider = BuildTracerProvider(
            configuration: BuildConfiguration((SamplerOptions.TracesSamplerArgConfigKey, "0.5")),
            configureServices: s => s.Configure<SamplerOptions>(o => o.Type = SamplerOptions.TraceIdRatioType));

        Assert.Equal("TraceIdRatioBasedSampler{0.500000}", tracerProvider.Sampler.Description);
    }

    [Fact]
    public void ProgrammaticTypeWithInvalidConfiguredArg_LogsAndUsesDefaultRatio()
    {
        // The fallback parse in ReadTraceIdRatio correctly reports the value that was
        // tried and rejected, not a "could not parse" message.
        using var eventListener = new TestEventListener(OpenTelemetrySdkEventSource.Log);

        using var tracerProvider = BuildTracerProvider(
            configuration: BuildConfiguration((SamplerOptions.TracesSamplerArgConfigKey, "1.5")),
            configureServices: s => s.Configure<SamplerOptions>(o => o.Type = SamplerOptions.TraceIdRatioType));

        Assert.Equal("TraceIdRatioBasedSampler{1.000000}", tracerProvider.Sampler.Description);
        AssertPayload(eventListener, TracesSamplerArgConfigInvalidEventId, "1.5");
    }

    private static SamplerOptions CreateOptions(params (string Key, string? Value)[] configuration)
        => new(BuildConfiguration(configuration));

    private static IConfiguration BuildConfiguration(params (string Key, string? Value)[] values)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(v => v.Key, v => v.Value))
            .Build();

    private static TracerProviderSdk BuildTracerProvider(
        IConfiguration? configuration = null,
        Action<IServiceCollection>? configureServices = null,
        Action<TracerProviderBuilder>? configureBuilder = null)
    {
        var builder = Sdk.CreateTracerProviderBuilder();

        builder.ConfigureServices(services =>
        {
            // Registered last so it replaces the environment variable only configuration
            // added by the SDK, which uses TryAddSingleton.
            if (configuration != null)
            {
                services.AddSingleton(configuration);
            }

            configureServices?.Invoke(services);
        });

        configureBuilder?.Invoke(builder);

        var tracerProvider = builder.Build() as TracerProviderSdk;

        Assert.NotNull(tracerProvider);

        return tracerProvider;
    }

    private static bool IsSamplerIgnoredEvent(EventWrittenEventArgs e)
        => e.EventId == TracerProviderSdkEventId
            && e.Payload != null
            && ((string)e.Payload[0]!).Contains(IgnoredSamplerMessageFragment, StringComparison.Ordinal);

    private static void AssertPayload(TestEventListener eventListener, int eventId, string expectedPayload)
        => Assert.Contains(
            eventListener.Messages,
            e => e.EventId == eventId
                && e.Payload != null
                && (string)e.Payload[0]! == expectedPayload);
}

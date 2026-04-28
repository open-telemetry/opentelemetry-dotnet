// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Internal;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Trace.Tests;

public sealed class SamplerOptionsTests : IDisposable
{
    public SamplerOptionsTests()
    {
        ClearSamplerEnvVars();
    }

    [Fact]
    public void SamplerOptions_NoConfigurationKeys_Defaults()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection([]).Build();
        var options = new SamplerOptions(configuration);

        Assert.Null(options.SamplerType);
        Assert.Null(options.SamplerArg);
    }

    [Fact]
    public void SamplerOptions_TracesSampler_FromConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [SamplerOptions.TracesSamplerConfigKey] = "always_on",
            })
            .Build();

        var options = new SamplerOptions(configuration);

        Assert.Equal("always_on", options.SamplerType);
    }

    [Fact]
    public void SamplerOptions_TracesSamplerArg_Parse()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [SamplerOptions.TracesSamplerArgConfigKey] = "0.5",
            })
            .Build();

        var options = new SamplerOptions(configuration);

        Assert.Equal(0.5, options.SamplerArg);
    }

    [Fact]
    public void SamplerOptions_TracesSamplerArg_Invalid_LeavesNullAndPreservesRaw()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [SamplerOptions.TracesSamplerArgConfigKey] = "banana",
            })
            .Build();

        var options = new SamplerOptions(configuration);

        // SamplerArg is null because the string couldn't be parsed.
        Assert.Null(options.SamplerArg);

        // SamplerArgRaw preserves the original string for ReadTraceIdRatio to use in its diagnostic.
        Assert.Equal("banana", options.SamplerArgRaw);
    }

    [Fact]
    public void SamplerOptions_InvalidArgWithNonRatioSampler_NoEvent55()
    {
        // OTEL_TRACES_SAMPLER_ARG is only evaluated for ratio-based samplers. A bad arg value
        // combined with a sampler type that ignores the arg must not produce a diagnostic.
        // NOTE: This specifically preserves the existing behavior before introducing SamplerOptions.
        using var eventListener = new TestEventListener(OpenTelemetrySdkEventSource.Log);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [SamplerOptions.TracesSamplerConfigKey] = "always_on",
                [SamplerOptions.TracesSamplerArgConfigKey] = "banana",
            })
            .Build();

        var builder = Sdk.CreateTracerProviderBuilder();
        builder.ConfigureServices(s => ReplaceRootConfiguration(s, configuration));

        using var tracerProvider = builder.Build() as TracerProviderSdk;

        Assert.NotNull(tracerProvider);
        Assert.Equal("AlwaysOnSampler", tracerProvider.Sampler.Description);
        Assert.DoesNotContain(eventListener.Messages, e => e.EventId == 55);
    }

    [Fact]
    public void SamplerOptions_ConfigureOverridesEnvironmentVariable()
    {
        Environment.SetEnvironmentVariable(SamplerOptions.TracesSamplerConfigKey, "always_off");

        var builder = Sdk.CreateTracerProviderBuilder();
        builder.ConfigureServices(s => s.Configure<SamplerOptions>(o => o.SamplerType = "always_on"));

        using var tracerProvider = builder.Build() as TracerProviderSdk;

        Assert.NotNull(tracerProvider);
        Assert.Equal("AlwaysOnSampler", tracerProvider.Sampler.Description);
    }

    [Fact]
    public void SamplerOptions_ConfigureOverridesSamplerArg()
    {
        Environment.SetEnvironmentVariable(SamplerOptions.TracesSamplerConfigKey, "traceidratio");
        Environment.SetEnvironmentVariable(SamplerOptions.TracesSamplerArgConfigKey, "0.1");

        var builder = Sdk.CreateTracerProviderBuilder();
        builder.ConfigureServices(s => s.Configure<SamplerOptions>(o => o.SamplerArg = 0.9));

        using var tracerProvider = builder.Build() as TracerProviderSdk;

        Assert.NotNull(tracerProvider);
        Assert.Equal("TraceIdRatioBasedSampler{0.900000}", tracerProvider.Sampler.Description);
    }

    [Fact]
    public void SamplerOptions_ConfigureFromSection()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Sampler:SamplerType"] = "traceidratio",
                ["Sampler:SamplerArg"] = "0.4",
            })
            .Build();

        var builder = Sdk.CreateTracerProviderBuilder();
        builder.ConfigureServices(s => s.Configure<SamplerOptions>(configuration.GetSection("Sampler")));

        using var tracerProvider = builder.Build() as TracerProviderSdk;

        Assert.NotNull(tracerProvider);
        Assert.Equal("TraceIdRatioBasedSampler{0.400000}", tracerProvider.Sampler.Description);
    }

    [Fact]
    public void TracerProvider_NoSamplerConfiguration_UsesDefaultSampler_NoDiagnostics()
    {
        using var eventListener = new TestEventListener(OpenTelemetrySdkEventSource.Log);

        var configuration = EmptySamplerConfiguration();
        var builder = Sdk.CreateTracerProviderBuilder();
        builder.ConfigureServices(s => ReplaceRootConfiguration(s, configuration));

        using var tracerProvider = builder.Build() as TracerProviderSdk;

        Assert.NotNull(tracerProvider);
        Assert.Equal("ParentBased{AlwaysOnSampler}", tracerProvider.Sampler.Description);
        Assert.DoesNotContain(
            eventListener.Messages,
            e => e.EventId == 46 && e.Payload != null && ((string)e.Payload[0]!).Contains("has been ignored because a value", StringComparison.Ordinal));
    }

    [Fact]
    public void TracerProvider_ProgrammaticSamplerWithoutConfiguration_NoIgnoredDiagnostic()
    {
        using var eventListener = new TestEventListener(OpenTelemetrySdkEventSource.Log);

        var configuration = EmptySamplerConfiguration();
        var builder = Sdk.CreateTracerProviderBuilder();
        builder.ConfigureServices(s => ReplaceRootConfiguration(s, configuration));
        builder.SetSampler(new AlwaysOnSampler());

        using var tracerProvider = builder.Build() as TracerProviderSdk;

        Assert.NotNull(tracerProvider);
        Assert.Equal("AlwaysOnSampler", tracerProvider.Sampler.Description);
        Assert.DoesNotContain(
            eventListener.Messages,
            e => e.EventId == 46 && e.Payload != null && ((string)e.Payload[0]!).Contains("has been ignored because a value", StringComparison.Ordinal));
    }

    [Fact]
    public void TracerProvider_ConfigurationWithProgrammaticSampler_EmitsIgnoredDiagnostic()
    {
        using var eventListener = new TestEventListener(OpenTelemetrySdkEventSource.Log);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [SamplerOptions.TracesSamplerConfigKey] = "always_on",
            })
            .Build();

        var builder = Sdk.CreateTracerProviderBuilder();
        builder.ConfigureServices(s => ReplaceRootConfiguration(s, configuration));
        builder.SetSampler(new AlwaysOffSampler());

        using var tracerProvider = builder.Build() as TracerProviderSdk;

        Assert.NotNull(tracerProvider);
        Assert.Equal("AlwaysOffSampler", tracerProvider.Sampler.Description);
        Assert.Contains(
            eventListener.Messages,
            e => e.EventId == 46
                && e.Payload != null
                && ((string)e.Payload[0]!).Contains("has been ignored because a value", StringComparison.Ordinal));
    }

    [Fact]
    public void TracerProvider_AlwaysOn_FromConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [SamplerOptions.TracesSamplerConfigKey] = "always_on",
            })
            .Build();

        var builder = Sdk.CreateTracerProviderBuilder();
        builder.ConfigureServices(s => ReplaceRootConfiguration(s, configuration));

        using var tracerProvider = builder.Build() as TracerProviderSdk;

        Assert.NotNull(tracerProvider);
        Assert.Equal("AlwaysOnSampler", tracerProvider.Sampler.Description);
    }

    [Fact]
    public void TracerProvider_AlwaysOff_FromConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [SamplerOptions.TracesSamplerConfigKey] = "always_off",
            })
            .Build();

        var builder = Sdk.CreateTracerProviderBuilder();
        builder.ConfigureServices(s => ReplaceRootConfiguration(s, configuration));

        using var tracerProvider = builder.Build() as TracerProviderSdk;

        Assert.NotNull(tracerProvider);
        Assert.Equal("AlwaysOffSampler", tracerProvider.Sampler.Description);
    }

    [Fact]
    public void TracerProvider_ParentBasedAlwaysOn_FromConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [SamplerOptions.TracesSamplerConfigKey] = "parentbased_always_on",
            })
            .Build();

        var builder = Sdk.CreateTracerProviderBuilder();
        builder.ConfigureServices(s => ReplaceRootConfiguration(s, configuration));

        using var tracerProvider = builder.Build() as TracerProviderSdk;

        Assert.NotNull(tracerProvider);
        Assert.Equal("ParentBased{AlwaysOnSampler}", tracerProvider.Sampler.Description);
    }

    [Fact]
    public void TracerProvider_ParentBasedAlwaysOff_FromConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [SamplerOptions.TracesSamplerConfigKey] = "parentbased_always_off",
            })
            .Build();

        var builder = Sdk.CreateTracerProviderBuilder();
        builder.ConfigureServices(s => ReplaceRootConfiguration(s, configuration));

        using var tracerProvider = builder.Build() as TracerProviderSdk;

        Assert.NotNull(tracerProvider);
        Assert.Equal("ParentBased{AlwaysOffSampler}", tracerProvider.Sampler.Description);
    }

    [Fact]
    public void TracerProvider_TraceIdRatio_FromConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [SamplerOptions.TracesSamplerConfigKey] = "traceidratio",
                [SamplerOptions.TracesSamplerArgConfigKey] = "0.3",
            })
            .Build();

        var builder = Sdk.CreateTracerProviderBuilder();
        builder.ConfigureServices(s => ReplaceRootConfiguration(s, configuration));

        using var tracerProvider = builder.Build() as TracerProviderSdk;

        Assert.NotNull(tracerProvider);
        Assert.IsType<TraceIdRatioBasedSampler>(tracerProvider.Sampler);
        Assert.Equal("TraceIdRatioBasedSampler{0.300000}", tracerProvider.Sampler.Description);
    }

    [Fact]
    public void TracerProvider_ParentBasedTraceIdRatio_FromConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [SamplerOptions.TracesSamplerConfigKey] = "parentbased_traceidratio",
                [SamplerOptions.TracesSamplerArgConfigKey] = "0.3",
            })
            .Build();

        var builder = Sdk.CreateTracerProviderBuilder();
        builder.ConfigureServices(s => ReplaceRootConfiguration(s, configuration));

        using var tracerProvider = builder.Build() as TracerProviderSdk;

        Assert.NotNull(tracerProvider);
        Assert.Equal("ParentBased{TraceIdRatioBasedSampler{0.300000}}", tracerProvider.Sampler.Description);
    }

    [Fact]
    public void TracerProvider_TraceIdRatio_NoArg_DefaultsToOne_LogsEmptyArg()
    {
        // Matches pre-options behaviour

        using var eventListener = new TestEventListener(OpenTelemetrySdkEventSource.Log);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [SamplerOptions.TracesSamplerConfigKey] = "traceidratio",
            })
            .Build();

        var builder = Sdk.CreateTracerProviderBuilder();
        builder.ConfigureServices(s => ReplaceRootConfiguration(s, configuration));

        using var tracerProvider = builder.Build() as TracerProviderSdk;

        Assert.NotNull(tracerProvider);
        Assert.Equal("TraceIdRatioBasedSampler{1.000000}", tracerProvider.Sampler.Description);
        Assert.Contains(
            eventListener.Messages,
            e => e.EventId == 55 && e.Payload != null && string.IsNullOrEmpty((string)e.Payload[0]!));
    }

    [Fact]
    public void TracerProvider_TraceIdRatio_OutOfRange_LogsAndUsesDefaultRatio()
    {
        using var eventListener = new TestEventListener(OpenTelemetrySdkEventSource.Log);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [SamplerOptions.TracesSamplerConfigKey] = "traceidratio",
                [SamplerOptions.TracesSamplerArgConfigKey] = "1.5",
            })
            .Build();

        var builder = Sdk.CreateTracerProviderBuilder();
        builder.ConfigureServices(s => ReplaceRootConfiguration(s, configuration));

        using var tracerProvider = builder.Build() as TracerProviderSdk;

        Assert.NotNull(tracerProvider);
        Assert.Equal("TraceIdRatioBasedSampler{1.000000}", tracerProvider.Sampler.Description);
        Assert.Contains(
            eventListener.Messages,
            e => e.EventId == 55 && e.Payload != null && (string)e.Payload[0]! == "1.5");
    }

    [Fact]
    public void TracerProvider_TraceIdRatio_OutOfRange_LogsOriginalConfigString()
    {
        using var eventListener = new TestEventListener(OpenTelemetrySdkEventSource.Log);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [SamplerOptions.TracesSamplerConfigKey] = "traceidratio",
                [SamplerOptions.TracesSamplerArgConfigKey] = "2.0",
            })
            .Build();

        var builder = Sdk.CreateTracerProviderBuilder();
        builder.ConfigureServices(s => ReplaceRootConfiguration(s, configuration));

        using var tracerProvider = builder.Build() as TracerProviderSdk;

        Assert.NotNull(tracerProvider);
        Assert.Equal("TraceIdRatioBasedSampler{1.000000}", tracerProvider.Sampler.Description);
        Assert.Contains(
            eventListener.Messages,
            e => e.EventId == 55 && e.Payload != null && (string)e.Payload[0]! == "2.0");
    }

    [Fact]
    public void TracerProvider_TraceIdRatio_UnparsableArg_LogsFromReadTraceIdRatio()
    {
        using var eventListener = new TestEventListener(OpenTelemetrySdkEventSource.Log);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [SamplerOptions.TracesSamplerConfigKey] = "traceidratio",
                [SamplerOptions.TracesSamplerArgConfigKey] = "not_a_double",
            })
            .Build();

        var builder = Sdk.CreateTracerProviderBuilder();
        builder.ConfigureServices(s => ReplaceRootConfiguration(s, configuration));

        using var tracerProvider = builder.Build() as TracerProviderSdk;

        Assert.NotNull(tracerProvider);
        Assert.Equal("TraceIdRatioBasedSampler{1.000000}", tracerProvider.Sampler.Description);
        Assert.Single(eventListener.Messages, e => e.EventId == 55);
        Assert.Contains(
            eventListener.Messages,
            e => e.EventId == 55 && e.Payload != null && (string)e.Payload[0]! == "not_a_double");
    }

    [Fact]
    public void TracerProvider_UnknownSampler_LogsAndUsesDefault()
    {
        using var eventListener = new TestEventListener(OpenTelemetrySdkEventSource.Log);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [SamplerOptions.TracesSamplerConfigKey] = "unknown_sampler",
            })
            .Build();

        var builder = Sdk.CreateTracerProviderBuilder();
        builder.ConfigureServices(s => ReplaceRootConfiguration(s, configuration));

        using var tracerProvider = builder.Build() as TracerProviderSdk;

        Assert.NotNull(tracerProvider);
        Assert.Equal("ParentBased{AlwaysOnSampler}", tracerProvider.Sampler.Description);
        Assert.Contains(
            eventListener.Messages,
            e => e.EventId == 54 && e.Payload != null && (string)e.Payload[0]! == "unknown_sampler");
    }

    [Fact]
    public void TracerProvider_EmptySamplerTypeKey_TreatedAsUnset()
    {
        using var eventListener = new TestEventListener(OpenTelemetrySdkEventSource.Log);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [SamplerOptions.TracesSamplerConfigKey] = string.Empty,
            })
            .Build();

        var builder = Sdk.CreateTracerProviderBuilder();
        builder.ConfigureServices(s => ReplaceRootConfiguration(s, configuration));

        using var tracerProvider = builder.Build() as TracerProviderSdk;

        Assert.NotNull(tracerProvider);
        Assert.Equal("ParentBased{AlwaysOnSampler}", tracerProvider.Sampler.Description);
        Assert.DoesNotContain(eventListener.Messages, e => e.EventId == 54);
    }

    [Fact]
    public void TracerProvider_TraceIdRatio_ProgrammaticOutOfRangeArg_FallsBackToDefaultAndLogs()
    {
        using var eventListener = new TestEventListener(OpenTelemetrySdkEventSource.Log);

        var builder = Sdk.CreateTracerProviderBuilder();
        builder.ConfigureServices(s => s.Configure<SamplerOptions>(o =>
        {
            o.SamplerType = "traceidratio";
            o.SamplerArg = 2.0; // out of [0.0, 1.0]; SamplerArgRaw is null (no config string)
        }));

        using var tracerProvider = builder.Build() as TracerProviderSdk;

        Assert.NotNull(tracerProvider);
        Assert.Equal("TraceIdRatioBasedSampler{1.000000}", tracerProvider.Sampler.Description);
        Assert.Contains(eventListener.Messages, e => e.EventId == 55);
    }

    [Fact]
    public void TracerProvider_TraceIdRatio_ProgrammaticNaNArg_FallsBackToDefaultAndLogs()
    {
        using var eventListener = new TestEventListener(OpenTelemetrySdkEventSource.Log);

        var builder = Sdk.CreateTracerProviderBuilder();
        builder.ConfigureServices(s => s.Configure<SamplerOptions>(o =>
        {
            o.SamplerType = "traceidratio";
            o.SamplerArg = double.NaN;
        }));

        using var tracerProvider = builder.Build() as TracerProviderSdk;

        Assert.NotNull(tracerProvider);
        Assert.Equal("TraceIdRatioBasedSampler{1.000000}", tracerProvider.Sampler.Description);
        Assert.Contains(eventListener.Messages, e => e.EventId == 55);
    }

    [Fact]
    public void TracerProvider_WhitespaceSamplerType_TreatedAsUnset()
    {
        using var eventListener = new TestEventListener(OpenTelemetrySdkEventSource.Log);

        var builder = Sdk.CreateTracerProviderBuilder();
        builder.ConfigureServices(s => s.Configure<SamplerOptions>(o => o.SamplerType = "   "));

        using var tracerProvider = builder.Build() as TracerProviderSdk;

        Assert.NotNull(tracerProvider);
        Assert.Equal("ParentBased{AlwaysOnSampler}", tracerProvider.Sampler.Description);
        Assert.DoesNotContain(eventListener.Messages, e => e.EventId == 54);
    }

    public void Dispose()
    {
        ClearSamplerEnvVars();
        GC.SuppressFinalize(this);
    }

    private static IConfiguration EmptySamplerConfiguration()
        => new ConfigurationBuilder().AddInMemoryCollection([]).Build();

    private static void ReplaceRootConfiguration(IServiceCollection services, IConfiguration configuration)
    {
        for (var i = services.Count - 1; i >= 0; i--)
        {
            if (services[i].ServiceType == typeof(IConfiguration))
            {
                services.RemoveAt(i);
            }
        }

        services.AddSingleton(configuration);
    }

    private static void ClearSamplerEnvVars()
    {
        Environment.SetEnvironmentVariable(SamplerOptions.TracesSamplerConfigKey, null);
        Environment.SetEnvironmentVariable(SamplerOptions.TracesSamplerArgConfigKey, null);
    }
}

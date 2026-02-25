// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Internal;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Metrics;

/// <summary>
/// Stores state used to build a <see cref="MeterProvider"/>.
/// </summary>
internal sealed partial class MeterProviderBuilderSdk : MeterProviderBuilder, IMeterProviderBuilder
{
    public const int DefaultMetricLimit = 1000;
    public const int DefaultCardinalityLimit = 2000;
    private const string DefaultInstrumentationVersion = "1.0.0.0";
    private const string InstrumentNameRegexPattern = @"^[a-z][a-z0-9-._/]{0,254}$";

    private readonly IServiceProvider serviceProvider;
    private MeterProviderSdk? meterProvider;

    public MeterProviderBuilderSdk(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
    }

    // Note: We don't use static readonly here because some customers
    // replace this using reflection which is not allowed on initonly static
    // fields. See: https://github.com/dotnet/runtime/issues/11571.
    // Customers: This is not guaranteed to work forever. We may change this
    // mechanism in the future do this at your own risk.
    public static Regex InstrumentNameRegex { get; set; } = GetInstrumentNameRegex();

    public List<InstrumentationRegistration> Instrumentation { get; } = [];

    public ResourceBuilder? ResourceBuilder { get; private set; }

    public ExemplarFilterType? ExemplarFilter { get; private set; }

    public MeterProvider? Provider => this.meterProvider;

    public List<MetricReader> Readers { get; } = [];

    public List<string> MeterSources { get; } = [];

    public List<Func<Instrument, MetricStreamConfiguration?>> ViewConfigs { get; } = [];

    public int MetricLimit { get; private set; } = DefaultMetricLimit;

    public int CardinalityLimit { get; private set; } = DefaultCardinalityLimit;

    /// <summary>
    /// Returns whether the given instrument name is valid according to the specification.
    /// </summary>
    /// <remarks>See specification: <see href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/api.md#instrument"/>.</remarks>
    /// <param name="instrumentName">The instrument name.</param>
    /// <returns>Boolean indicating if the instrument is valid.</returns>
    public static bool IsValidInstrumentName(string instrumentName)
        => !string.IsNullOrWhiteSpace(instrumentName) && InstrumentNameRegex.IsMatch(instrumentName);

    /// <summary>
    /// Returns whether the given custom view name is valid according to the specification.
    /// </summary>
    /// <remarks>See specification: <see href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/api.md#instrument"/>.</remarks>
    /// <param name="customViewName">The view name.</param>
    /// <returns>Boolean indicating if the instrument is valid.</returns>
    public static bool IsValidViewName(string customViewName) =>
        /* Only validate the view name in case it's not null. In case it's null, the view name will be the instrument name as per the spec. */
        customViewName == null || InstrumentNameRegex.IsMatch(customViewName);

    public void RegisterProvider(MeterProviderSdk meterProvider)
    {
        Debug.Assert(meterProvider != null, "meterProvider was null");

        if (this.meterProvider != null)
        {
            throw new NotSupportedException("MeterProvider cannot be accessed while build is executing.");
        }

        this.meterProvider = meterProvider;
    }

    public override MeterProviderBuilder AddInstrumentation<TInstrumentation>(Func<TInstrumentation> instrumentationFactory)
    {
        Debug.Assert(instrumentationFactory != null, "instrumentationFactory was null");

        return this.AddInstrumentation(
            typeof(TInstrumentation).Name,
            typeof(TInstrumentation).Assembly.GetName().Version?.ToString() ?? DefaultInstrumentationVersion,
            instrumentationFactory!());
    }

    public MeterProviderBuilder AddInstrumentation(
        string instrumentationName,
        string instrumentationVersion,
        object? instrumentation)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(instrumentationName), "instrumentationName was null or whitespace");
        Debug.Assert(!string.IsNullOrWhiteSpace(instrumentationVersion), "instrumentationVersion was null or whitespace");

        this.Instrumentation.Add(
            new InstrumentationRegistration(
                instrumentationName,
                instrumentationVersion,
                instrumentation));

        return this;
    }

    public MeterProviderBuilder ConfigureResource(Action<ResourceBuilder> configure)
    {
        Debug.Assert(configure != null, "configure was null");

        var resourceBuilder = this.ResourceBuilder ??= ResourceBuilder.CreateDefault();

        configure!(resourceBuilder);

        return this;
    }

    public MeterProviderBuilder SetResourceBuilder(ResourceBuilder resourceBuilder)
    {
        Debug.Assert(resourceBuilder != null, "resourceBuilder was null");

        this.ResourceBuilder = resourceBuilder;

        return this;
    }

    public MeterProviderBuilder SetExemplarFilter(ExemplarFilterType exemplarFilter)
    {
        this.ExemplarFilter = exemplarFilter;

        return this;
    }

    public override MeterProviderBuilder AddMeter(params string[] names)
    {
        Debug.Assert(names != null, "names was null");

        foreach (var name in names!)
        {
            Guard.ThrowIfNullOrWhitespace(name);

            this.MeterSources.Add(name);
        }

        return this;
    }

    public MeterProviderBuilder AddReader(MetricReader reader)
    {
        Debug.Assert(reader != null, "reader was null");

        this.Readers.Add(reader!);

        return this;
    }

    public MeterProviderBuilder AddView(Func<Instrument, MetricStreamConfiguration?> viewConfig)
    {
        Debug.Assert(viewConfig != null, "viewConfig was null");

        this.ViewConfigs.Add(viewConfig!);

        return this;
    }

    public MeterProviderBuilder SetMetricLimit(int metricLimit)
    {
        this.MetricLimit = metricLimit;

        return this;
    }

    public MeterProviderBuilder SetDefaultCardinalityLimit(int cardinalityLimit)
    {
        this.CardinalityLimit = cardinalityLimit;

        return this;
    }

    public MeterProviderBuilder ConfigureBuilder(Action<IServiceProvider, MeterProviderBuilder> configure)
    {
        Debug.Assert(configure != null, "configure was null");

        configure!(this.serviceProvider, this);

        return this;
    }

    public MeterProviderBuilder ConfigureServices(Action<IServiceCollection> configure)
        => throw new NotSupportedException("Services cannot be configured after ServiceProvider has been created.");

    MeterProviderBuilder IDeferredMeterProviderBuilder.Configure(Action<IServiceProvider, MeterProviderBuilder> configure)
        => this.ConfigureBuilder(configure);

#if NET
    [GeneratedRegex(InstrumentNameRegexPattern, RegexOptions.IgnoreCase)]
    private static partial Regex GetInstrumentNameRegex();
#else
    private static Regex GetInstrumentNameRegex() => new(InstrumentNameRegexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
#endif

    internal readonly struct InstrumentationRegistration
    {
        public readonly string Name;
        public readonly string Version;
        public readonly object? Instance;

        internal InstrumentationRegistration(string name, string version, object? instance)
        {
            this.Name = name;
            this.Version = version;
            this.Instance = instance;
        }
    }
}

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
internal sealed class MeterProviderBuilderSdk : MeterProviderBuilder, IMeterProviderBuilder
{
    public const int DefaultMetricLimit = 1000;
    public const int DefaultCardinalityLimit = 2000;
    private const string DefaultInstrumentationVersion = "1.0.0.0";

    private readonly IServiceProvider serviceProvider;
    private MeterProviderSdk? meterProvider;

    public MeterProviderBuilderSdk(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
    }

    public List<InstrumentationRegistration> Instrumentation { get; } = new();

    public ResourceBuilder? ResourceBuilder { get; private set; }

    public ExemplarFilterType? ExemplarFilter { get; private set; }

    public MeterProvider? Provider => this.meterProvider;

    public List<MetricReader> Readers { get; } = new();

    public List<string> MeterSources { get; } = new();

    public List<Func<Instrument, MetricStreamConfiguration?>> ViewConfigs { get; } = new();

    public int MetricLimit { get; private set; } = DefaultMetricLimit;

    public int CardinalityLimit { get; private set; } = DefaultCardinalityLimit;

    /// <summary>
    /// Returns whether the given instrument name is valid according to the specification.
    /// </summary>
    /// <remarks>See specification: <see href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/api.md#instrument"/>.</remarks>
    /// <param name="instrumentName">The instrument name.</param>
    /// <returns>Boolean indicating if the instrument is valid.</returns>
    public static bool IsValidInstrumentName(string instrumentName)
    {
        if (string.IsNullOrWhiteSpace(instrumentName))
        {
            return false;
        }

        return IsValidName(instrumentName);
    }

    /// <summary>
    /// Returns whether the given custom view name is valid according to the specification.
    /// </summary>
    /// <remarks>See specification: <see href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/api.md#instrument"/>.</remarks>
    /// <param name="customViewName">The view name.</param>
    /// <returns>Boolean indicating if the instrument is valid.</returns>
    public static bool IsValidViewName(string customViewName)
    {
        // Only validate the view name in case it's not null. In case it's null, the view name will be the instrument name as per the spec.
        if (customViewName == null)
        {
            return true;
        }

        return IsValidName(customViewName);
    }

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
    {
        throw new NotSupportedException("Services cannot be configured after ServiceProvider has been created.");
    }

    MeterProviderBuilder IDeferredMeterProviderBuilder.Configure(Action<IServiceProvider, MeterProviderBuilder> configure)
        => this.ConfigureBuilder(configure);

    private static bool IsValidName(string name)
    {
        if (name.Length > 254)
        {
            return false;
        }

        for (int i = 0; i < name.Length; i++)
        {
            if ((name[i] >= '-' && name[i] <= '9' && i > 0) ||
                (name[i] >= 'A' && name[i] <= 'Z') ||
                (name[i] >= 'a' && name[i] <= 'z') ||
                (name[i] == '_' && i > 0))
            {
                continue;
            }

            return false;
        }

        return true;
    }

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

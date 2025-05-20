// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
using System.Diagnostics.CodeAnalysis;
#endif
using System.Diagnostics.Metrics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry.Internal;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Metrics;

/// <summary>
/// Contains extension methods for the <see cref="MeterProviderBuilder"/> class.
/// </summary>
public static class MeterProviderBuilderExtensions
{
    /// <summary>
    /// Adds a reader to the provider.
    /// </summary>
    /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>.</param>
    /// <param name="reader"><see cref="MetricReader"/>.</param>
    /// <returns>The supplied <see cref="MeterProviderBuilder"/> for chaining.</returns>
    public static MeterProviderBuilder AddReader(this MeterProviderBuilder meterProviderBuilder, MetricReader reader)
    {
        Guard.ThrowIfNull(reader);

        meterProviderBuilder.ConfigureBuilder((sp, builder) =>
        {
            if (builder is MeterProviderBuilderSdk meterProviderBuilderSdk)
            {
                meterProviderBuilderSdk.AddReader(reader);
            }
        });

        return meterProviderBuilder;
    }

    /// <summary>
    /// Adds a reader to the provider.
    /// </summary>
    /// <remarks>
    /// Note: The type specified by <typeparamref name="T"/> will be
    /// registered as a singleton service into application services.
    /// </remarks>
    /// <typeparam name="T">Reader type.</typeparam>
    /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>.</param>
    /// <returns>The supplied <see cref="MeterProviderBuilder"/> for chaining.</returns>
    public static MeterProviderBuilder AddReader<
#if NET
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
#endif
    T>(this MeterProviderBuilder meterProviderBuilder)
        where T : MetricReader
    {
        meterProviderBuilder.ConfigureServices(services => services.TryAddSingleton<T>());

        meterProviderBuilder.ConfigureBuilder((sp, builder) =>
        {
            if (builder is MeterProviderBuilderSdk meterProviderBuilderSdk)
            {
                meterProviderBuilderSdk.AddReader(sp.GetRequiredService<T>());
            }
        });

        return meterProviderBuilder;
    }

    /// <summary>
    /// Adds a reader to the provider.
    /// </summary>
    /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>.</param>
    /// <param name="implementationFactory">The factory that creates the service.</param>
    /// <returns>The supplied <see cref="MeterProviderBuilder"/> for chaining.</returns>
    public static MeterProviderBuilder AddReader(
        this MeterProviderBuilder meterProviderBuilder,
        Func<IServiceProvider, MetricReader> implementationFactory)
    {
        Guard.ThrowIfNull(implementationFactory);

        meterProviderBuilder.ConfigureBuilder((sp, builder) =>
        {
            if (builder is MeterProviderBuilderSdk meterProviderBuilderSdk)
            {
                meterProviderBuilderSdk.AddReader(implementationFactory(sp));
            }
        });

        return meterProviderBuilder;
    }

    /// <summary>
    /// Add metric view, which can be used to customize the Metrics outputted
    /// from the SDK. The views are applied in the order they are added.
    /// </summary>
    /// <remarks>See View specification here : https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk.md#view.</remarks>
    /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>.</param>
    /// <param name="instrumentName">Name of the instrument, to be used as part of Instrument selection criteria.</param>
    /// <param name="name">Name of the view. This will be used as name of resulting metrics stream.</param>
    /// <returns>The supplied <see cref="MeterProviderBuilder"/> for chaining.</returns>
    public static MeterProviderBuilder AddView(this MeterProviderBuilder meterProviderBuilder, string instrumentName, string name)
    {
        Guard.ThrowIfNull(instrumentName);

        if (!MeterProviderBuilderSdk.IsValidInstrumentName(name))
        {
            throw new ArgumentException($"Custom view name {name} is invalid.", nameof(name));
        }

#pragma warning disable CA1062 // Validate arguments of public methods - needed for netstandard2.1
#if NET || NETSTANDARD2_1_OR_GREATER
        if (instrumentName.Contains('*', StringComparison.Ordinal))
#else
        if (instrumentName.Contains('*'))
#endif
#pragma warning restore CA1062 // Validate arguments of public methods - needed for netstandard2.1
        {
            throw new ArgumentException(
                $"Instrument selection criteria is invalid. Instrument name '{instrumentName}' " +
                $"contains a wildcard character. This is not allowed when using a view to " +
                $"rename a metric stream as it would lead to conflicting metric stream names.",
                nameof(instrumentName));
        }

        meterProviderBuilder.AddView(instrumentName, new MetricStreamConfiguration { Name = name });

        return meterProviderBuilder;
    }

    /// <summary>
    /// Add metric view, which can be used to customize the Metrics outputted
    /// from the SDK. The views are applied in the order they are added.
    /// </summary>
    /// <remarks>See View specification here : https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk.md#view.</remarks>
    /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>.</param>
    /// <param name="instrumentName">Name of the instrument, to be used as part of Instrument selection criteria.</param>
    /// <param name="metricStreamConfiguration">Aggregation configuration used to produce metrics stream.</param>
    /// <returns>The supplied <see cref="MeterProviderBuilder"/> for chaining.</returns>
    public static MeterProviderBuilder AddView(this MeterProviderBuilder meterProviderBuilder, string instrumentName, MetricStreamConfiguration metricStreamConfiguration)
    {
        Guard.ThrowIfNullOrWhitespace(instrumentName);
        Guard.ThrowIfNull(metricStreamConfiguration);

#if NET || NETSTANDARD2_1_OR_GREATER
#pragma warning disable CA1062 // Validate arguments of public methods - needed for netstandard2.1
        if (metricStreamConfiguration.Name != null && instrumentName.Contains('*', StringComparison.Ordinal))
#pragma warning restore CA1062 // Validate arguments of public methods - needed for netstandard2.1
#else
        if (metricStreamConfiguration.Name != null && instrumentName.Contains('*'))
#endif
        {
            throw new ArgumentException(
                $"Instrument selection criteria is invalid. Instrument name '{instrumentName}' " +
                $"contains a wildcard character. This is not allowed when using a view to " +
                $"rename a metric stream as it would lead to conflicting metric stream names.",
                nameof(instrumentName));
        }

        meterProviderBuilder.ConfigureBuilder((sp, builder) =>
        {
            if (builder is MeterProviderBuilderSdk meterProviderBuilderSdk)
            {
#if NET || NETSTANDARD2_1_OR_GREATER
                if (instrumentName.Contains('*', StringComparison.Ordinal))
#else
                if (instrumentName.Contains('*'))
#endif
                {
#if NET || NETSTANDARD2_1_OR_GREATER
                    var pattern = '^' + Regex.Escape(instrumentName).Replace("\\*", ".*", StringComparison.Ordinal);
#else
                    var pattern = '^' + Regex.Escape(instrumentName).Replace("\\*", ".*");
#endif
                    var regex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
                    meterProviderBuilderSdk.AddView(instrument => regex.IsMatch(instrument.Name) ? metricStreamConfiguration : null);
                }
                else
                {
                    meterProviderBuilderSdk.AddView(instrument => instrument.Name.Equals(instrumentName, StringComparison.OrdinalIgnoreCase) ? metricStreamConfiguration : null);
                }
            }
        });

        return meterProviderBuilder;
    }

    /// <summary>
    /// Add metric view, which can be used to customize the Metrics outputted
    /// from the SDK. The views are applied in the order they are added.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Note: An invalid <see cref="MetricStreamConfiguration"/>
    /// returned from <paramref name="viewConfig"/> will cause the
    /// view to be ignored, no error will be
    /// thrown at runtime.</item>
    /// <item>See View specification here : https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk.md#view.</item>
    /// </list>
    /// </remarks>
    /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>.</param>
    /// <param name="viewConfig">Function to configure aggregation based on the instrument.</param>
    /// <returns>The supplied <see cref="MeterProviderBuilder"/> for chaining.</returns>
    public static MeterProviderBuilder AddView(this MeterProviderBuilder meterProviderBuilder, Func<Instrument, MetricStreamConfiguration?> viewConfig)
    {
        Guard.ThrowIfNull(viewConfig);

        meterProviderBuilder.ConfigureBuilder((sp, builder) =>
        {
            if (builder is MeterProviderBuilderSdk meterProviderBuilderSdk)
            {
                meterProviderBuilderSdk.AddView(viewConfig);
            }
        });

        return meterProviderBuilder;
    }

    /// <summary>
    /// Sets the maximum number of Metric streams supported by the MeterProvider.
    /// When no Views are configured, every instrument will result in one metric stream,
    /// so this control the numbers of instruments supported.
    /// When Views are configured, a single instrument can result in multiple metric streams,
    /// so this control the number of streams.
    /// </summary>
    /// <remarks>
    /// If an instrument is created, but disposed later, this will still be contributing to the limit.
    /// This may change in the future.
    /// </remarks>
    /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>.</param>
    /// <param name="maxMetricStreams">Maximum number of metric streams allowed.</param>
    /// <returns>The supplied <see cref="MeterProviderBuilder"/> for chaining.</returns>
    public static MeterProviderBuilder SetMaxMetricStreams(this MeterProviderBuilder meterProviderBuilder, int maxMetricStreams)
    {
        Guard.ThrowIfOutOfRange(maxMetricStreams, min: 1);

        meterProviderBuilder.ConfigureBuilder((sp, builder) =>
        {
            if (builder is MeterProviderBuilderSdk meterProviderBuilderSdk)
            {
                meterProviderBuilderSdk.SetMetricLimit(maxMetricStreams);
            }
        });

        return meterProviderBuilder;
    }

    /// <summary>
    /// Sets the maximum number of MetricPoints allowed per metric stream.
    /// This limits the number of unique combinations of key/value pairs used
    /// for reporting measurements.
    /// </summary>
    /// <remarks>
    /// If a particular key/value pair combination is used at least once,
    /// it will contribute to the limit for the life of the process.
    /// This may change in the future. See: https://github.com/open-telemetry/opentelemetry-dotnet/issues/2360.
    /// </remarks>
    /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>.</param>
    /// <param name="maxMetricPointsPerMetricStream">Maximum number of metric points allowed per metric stream.</param>
    /// <returns>The supplied <see cref="MeterProviderBuilder"/> for chaining.</returns>
    [Obsolete("Use MetricStreamConfiguration.CardinalityLimit via the AddView API instead. This method is marked as obsolete in version 1.10.0 and will be removed in a future version.")]
    public static MeterProviderBuilder SetMaxMetricPointsPerMetricStream(this MeterProviderBuilder meterProviderBuilder, int maxMetricPointsPerMetricStream)
    {
        Guard.ThrowIfOutOfRange(maxMetricPointsPerMetricStream, min: 1);

        meterProviderBuilder.ConfigureBuilder((sp, builder) =>
        {
            if (builder is MeterProviderBuilderSdk meterProviderBuilderSdk)
            {
                meterProviderBuilderSdk.SetDefaultCardinalityLimit(maxMetricPointsPerMetricStream);
            }
        });

        return meterProviderBuilder;
    }

    /// <summary>
    /// Sets the <see cref="ResourceBuilder"/> from which the Resource associated with
    /// this provider is built from. Overwrites currently set ResourceBuilder.
    /// You should usually use <see cref="ConfigureResource(MeterProviderBuilder, Action{ResourceBuilder})"/> instead
    /// (call <see cref="ResourceBuilder.Clear"/> if desired).
    /// </summary>
    /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>.</param>
    /// <param name="resourceBuilder"><see cref="ResourceBuilder"/> from which Resource will be built.</param>
    /// <returns>The supplied <see cref="MeterProviderBuilder"/> for chaining.</returns>
    public static MeterProviderBuilder SetResourceBuilder(this MeterProviderBuilder meterProviderBuilder, ResourceBuilder resourceBuilder)
    {
        Guard.ThrowIfNull(resourceBuilder);

        meterProviderBuilder.ConfigureBuilder((sp, builder) =>
        {
            if (builder is MeterProviderBuilderSdk meterProviderBuilderSdk)
            {
                meterProviderBuilderSdk.SetResourceBuilder(resourceBuilder);
            }
        });

        return meterProviderBuilder;
    }

    /// <summary>
    /// Modify the <see cref="ResourceBuilder"/> from which the Resource associated with
    /// this provider is built from in-place.
    /// </summary>
    /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>.</param>
    /// <param name="configure">An action which modifies the provided <see cref="ResourceBuilder"/> in-place.</param>
    /// <returns>The supplied <see cref="MeterProviderBuilder"/> for chaining.</returns>
    public static MeterProviderBuilder ConfigureResource(this MeterProviderBuilder meterProviderBuilder, Action<ResourceBuilder> configure)
    {
        Guard.ThrowIfNull(configure);

        meterProviderBuilder.ConfigureBuilder((sp, builder) =>
        {
            if (builder is MeterProviderBuilderSdk meterProviderBuilderSdk)
            {
                meterProviderBuilderSdk.ConfigureResource(configure);
            }
        });

        return meterProviderBuilder;
    }

    /// <summary>
    /// Run the given actions to initialize the <see cref="MeterProvider"/>.
    /// </summary>
    /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>.</param>
    /// <returns><see cref="MeterProvider"/>.</returns>
    public static MeterProvider Build(this MeterProviderBuilder meterProviderBuilder)
    {
        if (meterProviderBuilder is MeterProviderBuilderBase meterProviderBuilderBase)
        {
            return meterProviderBuilderBase.InvokeBuild();
        }

        throw new NotSupportedException($"Build is not supported on '{meterProviderBuilder?.GetType().FullName ?? "null"}' instances.");
    }

    /// <summary>
    /// Sets the default <see cref="ExemplarFilterType"/> for the provider.
    /// </summary>
    /// <remarks>
    /// <para>Notes:
    /// <list type="bullet">
    /// <item>The configured <see cref="ExemplarFilterType"/> controls how
    /// measurements will be offered to <see cref="ExemplarReservoir"/>s which
    /// are responsible for storing <see cref="Exemplar"/>s on metrics.</item>
    /// <item>The default provider configuration is <see
    /// cref="ExemplarFilterType.AlwaysOff"/>.</item>
    /// <item>Use <see cref="ExemplarFilterType.TraceBased"/> or <see
    /// cref="ExemplarFilterType.AlwaysOn"/> to enable <see cref="Exemplar"/>s
    /// for all metrics managed by the provider.</item>
    /// <item>If <see cref="Exemplar"/>s are enabled on the provider by the
    /// configured <see cref="ExemplarFilterType"/> then <see
    /// cref="ExemplarReservoir"/>s will be configured on metrics using the
    /// defaults described in the specification: <see
    /// href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk.md#exemplar-defaults"
    /// />. To change the <see cref="ExemplarReservoir"/> for a metric use the
    /// <c>AddView</c> API and <see
    /// cref="MetricStreamConfiguration.ExemplarReservoirFactory"/>.</item>
    /// </list>
    /// </para>
    /// <para>Specification: <see
    /// href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk.md#exemplarfilter"/>.</para>
    /// </remarks>
    /// <param name="meterProviderBuilder"><see
    /// cref="MeterProviderBuilder"/>.</param>
    /// <param name="exemplarFilter"><see cref="ExemplarFilterType"/> to
    /// use.</param>
    /// <returns>The supplied <see cref="MeterProviderBuilder"/> for
    /// chaining.</returns>
    public static MeterProviderBuilder SetExemplarFilter(
        this MeterProviderBuilder meterProviderBuilder,
        ExemplarFilterType exemplarFilter)
    {
        meterProviderBuilder.ConfigureBuilder((sp, builder) =>
        {
            if (builder is MeterProviderBuilderSdk meterProviderBuilderSdk)
            {
                switch (exemplarFilter)
                {
                    case ExemplarFilterType.AlwaysOn:
                    case ExemplarFilterType.AlwaysOff:
                    case ExemplarFilterType.TraceBased:
                        meterProviderBuilderSdk.SetExemplarFilter(exemplarFilter);
                        break;
                    default:
                        throw new NotSupportedException($"ExemplarFilterType '{exemplarFilter}' is not supported.");
                }
            }
        });

        return meterProviderBuilder;
    }
}

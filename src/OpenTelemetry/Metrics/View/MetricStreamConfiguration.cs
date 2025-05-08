// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if EXPOSE_EXPERIMENTAL_FEATURES && NET
using System.Diagnostics.CodeAnalysis;
#endif
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// Stores configuration for a MetricStream.
/// </summary>
public class MetricStreamConfiguration
{
    private string? name;

    private int? cardinalityLimit;

    /// <summary>
    /// Gets the drop configuration.
    /// </summary>
    /// <remarks>
    /// Note: All metrics for the given instrument will be dropped (not
    /// collected).
    /// </remarks>
    public static MetricStreamConfiguration Drop { get; } = new MetricStreamConfiguration { ViewId = -1 };

    /// <summary>
    /// Gets or sets the optional name of the metric stream.
    /// </summary>
    /// <remarks>
    /// Note: If not provided the instrument name will be used.
    /// </remarks>
    public string? Name
    {
        get => this.name;
        set
        {
            if (value != null && !MeterProviderBuilderSdk.IsValidViewName(value))
            {
                throw new ArgumentException($"Custom view name {value} is invalid.", nameof(value));
            }

            this.name = value;
        }
    }

    /// <summary>
    /// Gets or sets the optional description of the metric stream.
    /// </summary>
    /// <remarks>
    /// Note: If not provided the instrument description will be used.
    /// </remarks>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the optional tag keys to include in the metric stream.
    /// </summary>
    /// <remarks>
    /// Notes:
    /// <list type="bullet">
    /// <item>If not provided, all the tags provided by the instrument
    /// while reporting measurements will be used for aggregation.
    /// If provided, only those tags in this list will be used
    /// for aggregation. Providing an empty array will result
    /// in a metric stream without any tags.
    /// </item>
    /// <item>A copy is made of the provided array.</item>
    /// </list>
    /// </remarks>
#pragma warning disable CA1819 // Properties should not return arrays
    public string[]? TagKeys
#pragma warning restore CA1819 // Properties should not return arrays
    {
        get => this.CopiedTagKeys?.ToArray();
        set => this.CopiedTagKeys = value?.ToArray();
    }

    /// <summary>
    /// Gets or sets a positive integer value defining the maximum number of
    /// data points allowed for the metric managed by the view.
    /// </summary>
    /// <remarks>
    /// <para>Spec reference: <see
    /// href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk.md#cardinality-limits">Cardinality
    /// limits</see>.</para>
    /// Note: The cardinality limit determines the maximum number of unique
    /// dimension combinations for metrics.
    /// Metrics with zero dimensions and overflow metrics are treated specially
    /// and do not count against this limit.
    /// If not set the default
    /// MeterProvider cardinality limit of 2000 will apply.
    /// </remarks>
    public int? CardinalityLimit
    {
        get => this.cardinalityLimit;
        set
        {
            if (value != null)
            {
                Guard.ThrowIfOutOfRange(value.Value, min: 1, max: int.MaxValue);
            }

            this.cardinalityLimit = value;
        }
    }

#if EXPOSE_EXPERIMENTAL_FEATURES
    /// <summary>
    /// Gets or sets a factory function used to generate an <see
    /// cref="ExemplarReservoir"/> for the metric managed by the view to use
    /// when storing <see cref="Exemplar"/>s.
    /// </summary>
    /// <remarks>
    /// <inheritdoc cref="ExemplarReservoir" path="/remarks/para[@experimental-warning='true']"/>
    /// <para>Note: Returning <see langword="null"/> from the factory function will
    /// result in the default <see cref="ExemplarReservoir"/> being chosen by
    /// the SDK based on the type of metric.</para>
    /// Specification: <see
    /// href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk.md#stream-configuration"/>.
    /// </remarks>
#if NET
    [Experimental(DiagnosticDefinitions.ExemplarReservoirExperimentalApi, UrlFormat = DiagnosticDefinitions.ExperimentalApiUrlFormat)]
#endif
    public Func<ExemplarReservoir?>? ExemplarReservoirFactory { get; set; }
#else
    internal Func<ExemplarReservoir?>? ExemplarReservoirFactory { get; set; }
#endif

    internal string[]? CopiedTagKeys { get; private set; }

    internal int? ViewId { get; set; }
}

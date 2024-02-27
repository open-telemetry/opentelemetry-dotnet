// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if EXPOSE_EXPERIMENTAL_FEATURES && NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
using OpenTelemetry.Internal;
#endif

namespace OpenTelemetry.Metrics;

#if EXPOSE_EXPERIMENTAL_FEATURES
/// <summary>
/// Represents an Exemplar measurement.
/// </summary>
/// <remarks><inheritdoc cref="Exemplar" path="/remarks"/></remarks>
/// <typeparam name="T">Measurement type.</typeparam>
#if NET8_0_OR_GREATER
[Experimental(DiagnosticDefinitions.ExemplarExperimentalApi, UrlFormat = DiagnosticDefinitions.ExperimentalApiUrlFormat)]
#endif
public
#else
internal
#endif
    readonly ref struct ExemplarMeasurement<T>
    where T : struct
{
    internal ExemplarMeasurement(
        T value,
        ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        this.Value = value;
        this.Tags = tags;
        this.ExplicitBucketHistogramBucketIndex = -1;
    }

    internal ExemplarMeasurement(
        T value,
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        int explicitBucketHistogramIndex)
    {
        this.Value = value;
        this.Tags = tags;
        this.ExplicitBucketHistogramBucketIndex = explicitBucketHistogramIndex;
    }

    /// <summary>
    /// Gets the measurement value.
    /// </summary>
    public T Value { get; }

    /// <summary>
    /// Gets the measurement tags.
    /// </summary>
    /// <remarks>
    /// Note: <see cref="Tags"/> represents the full set of tags supplied at
    /// measurement regardless of any filtering configured by a view (<see
    /// cref="MetricStreamConfiguration.TagKeys"/>).
    /// </remarks>
    public ReadOnlySpan<KeyValuePair<string, object?>> Tags { get; }

    internal int ExplicitBucketHistogramBucketIndex { get; }
}

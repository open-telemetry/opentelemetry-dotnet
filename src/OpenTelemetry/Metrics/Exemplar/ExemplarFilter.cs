// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if EXPOSE_EXPERIMENTAL_FEATURES && NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
using OpenTelemetry.Internal;
#endif

namespace OpenTelemetry.Metrics;

#if EXPOSE_EXPERIMENTAL_FEATURES
/// <summary>
/// ExemplarFilter base implementation and contract.
/// </summary>
/// <remarks>
/// <inheritdoc cref="Exemplar" path="/remarks/para[@experimental-warning='true']"/>
/// Specification: <see
/// href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk.md#exemplarfilter"/>.
/// </remarks>
#if NET8_0_OR_GREATER
[Experimental(DiagnosticDefinitions.ExemplarExperimentalApi, UrlFormat = DiagnosticDefinitions.ExperimentalApiUrlFormat)]
#endif
public
#else
internal
#endif
    abstract class ExemplarFilter
{
    /// <summary>
    /// Determines if a given measurement is eligible for being
    /// considered for becoming Exemplar.
    /// </summary>
    /// <param name="value">The value of the measurement.</param>
    /// <param name="tags">The complete set of tags provided with the measurement.</param>
    /// <returns>
    /// Returns
    /// <c>true</c> to indicate this measurement is eligible to become Exemplar
    /// and will be given to an ExemplarReservoir.
    /// Reservoir may further sample, so a true here does not mean that this
    /// measurement will become an exemplar, it just means it'll be
    /// eligible for being Exemplar.
    /// <c>false</c> to indicate this measurement is not eligible to become Exemplar
    /// and will not be given to the ExemplarReservoir.
    /// </returns>
    public abstract bool ShouldSample(long value, ReadOnlySpan<KeyValuePair<string, object?>> tags);

    /// <summary>
    /// Determines if a given measurement is eligible for being
    /// considered for becoming Exemplar.
    /// </summary>
    /// <param name="value">The value of the measurement.</param>
    /// <param name="tags">The complete set of tags provided with the measurement.</param>
    /// <returns>
    /// Returns
    /// <c>true</c> to indicate this measurement is eligible to become Exemplar
    /// and will be given to an ExemplarReservoir.
    /// Reservoir may further sample, so a true here does not mean that this
    /// measurement will become an exemplar, it just means it'll be
    /// eligible for being Exemplar.
    /// <c>false</c> to indicate this measurement is not eligible to become Exemplar
    /// and will not be given to the ExemplarReservoir.
    /// </returns>
    public abstract bool ShouldSample(double value, ReadOnlySpan<KeyValuePair<string, object?>> tags);
}

// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if EXPOSE_EXPERIMENTAL_FEATURES && NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
using OpenTelemetry.Internal;
#endif

using System.Diagnostics;

namespace OpenTelemetry.Metrics;

#if EXPOSE_EXPERIMENTAL_FEATURES
/// <summary>
/// An ExemplarFilter which makes those measurements eligible for being an Exemplar,
/// which are recorded in the context of a sampled parent activity (span).
/// </summary>
/// <remarks><inheritdoc cref="Exemplar" path="/remarks"/></remarks>
#if NET8_0_OR_GREATER
[Experimental(DiagnosticDefinitions.ExemplarExperimentalApi, UrlFormat = DiagnosticDefinitions.ExperimentalApiUrlFormat)]
#endif
public
#else
/// <summary>
/// An ExemplarFilter which makes those measurements eligible for being an Exemplar,
/// which are recorded in the context of a sampled parent activity (span).
/// </summary>
internal
#endif
    sealed class TraceBasedExemplarFilter : ExemplarFilter
{
    /// <inheritdoc/>
    public override bool ShouldSample(long value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        return Activity.Current?.Recorded ?? false;
    }

    /// <inheritdoc/>
    public override bool ShouldSample(double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        return Activity.Current?.Recorded ?? false;
    }
}

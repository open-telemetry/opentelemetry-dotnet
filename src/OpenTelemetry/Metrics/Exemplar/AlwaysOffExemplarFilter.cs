// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if EXPOSE_EXPERIMENTAL_FEATURES && NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
using OpenTelemetry.Internal;
#endif

namespace OpenTelemetry.Metrics;

#if EXPOSE_EXPERIMENTAL_FEATURES
/// <summary>
/// An ExemplarFilter which makes no measurements eligible for being an Exemplar.
/// Using this ExemplarFilter is as good as disabling Exemplar feature.
/// </summary>
/// <remarks><inheritdoc cref="Exemplar" path="/remarks"/></remarks>
#if NET8_0_OR_GREATER
[Experimental(DiagnosticDefinitions.ExemplarExperimentalApi, UrlFormat = DiagnosticDefinitions.ExperimentalApiUrlFormat)]
#endif
public
#else
/// <summary>
/// An ExemplarFilter which makes no measurements eligible for being an Exemplar.
/// Using this ExemplarFilter is as good as disabling Exemplar feature.
/// </summary>
internal
#endif
sealed class AlwaysOffExemplarFilter : ExemplarFilter
{
    /// <inheritdoc/>
    public override bool ShouldSample(long value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        return false;
    }

    /// <inheritdoc/>
    public override bool ShouldSample(double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        return false;
    }
}

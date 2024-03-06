// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
#if EXPOSE_EXPERIMENTAL_FEATURES && NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
using OpenTelemetry.Internal;
#endif

namespace OpenTelemetry.Metrics;

#if EXPOSE_EXPERIMENTAL_FEATURES
/// <summary>
/// Defines the supported exemplar filters.
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
    enum ExemplarFilterType
{
    /// <summary>
    /// An exemplar filter which makes no measurements eligible for becoming an
    /// <see cref="Exemplar"/>.
    /// </summary>
    /// <remarks>
    /// <para>Note: Setting <see cref="AlwaysOff"/> on a meter provider
    /// effectively disables exemplars.</para>
    /// <para>Specification: <see
    /// href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk.md#alwaysoff"/>.</para>
    /// </remarks>
    AlwaysOff,

    /// <summary>
    /// An exemplar filter which makes all measurements eligible for becoming an
    /// <see cref="Exemplar"/>.
    /// </summary>
    /// <remarks>
    /// Specification: <see
    /// href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk.md#alwayson"/>.
    /// </remarks>
    AlwaysOn,

    /// <summary>
    /// An exemplar filter which makes measurements recorded in the context of a
    /// sampled <see cref="Activity"/> (span) eligible for becoming an <see
    /// cref="Exemplar"/>.
    /// </summary>
    /// <remarks>
    /// Specification: <see
    /// href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk.md#tracebased"/>.
    /// </remarks>
    TraceBased,
}

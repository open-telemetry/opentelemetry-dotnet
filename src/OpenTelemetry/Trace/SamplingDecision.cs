// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Trace;

/// <summary>
/// Enumeration to define sampling decision.
/// </summary>
public enum SamplingDecision
{
    /// <summary>
    /// The activity will be created but not recorded.
    /// Activity.IsAllDataRequested will return false.
    /// </summary>
    Drop,

    /// <summary>
    /// The activity will be created and recorded, but sampling flag will not be set.
    /// Activity.IsAllDataRequested will return true.
    /// Activity.Recorded will return false.
    /// </summary>
    RecordOnly,

    /// <summary>
    /// The activity will be created, recorded, and sampling flag will be set.
    /// Activity.IsAllDataRequested will return true.
    /// Activity.Recorded will return true.
    /// </summary>
    RecordAndSample,
}
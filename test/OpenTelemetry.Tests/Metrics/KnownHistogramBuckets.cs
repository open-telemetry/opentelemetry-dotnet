// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Metrics.Tests;

public enum KnownHistogramBuckets
{
    /// <summary>
    /// Default OpenTelemetry semantic convention buckets.
    /// </summary>
    Default,

    /// <summary>
    /// Buckets for up to 10 seconds.
    /// </summary>
    DefaultShortSeconds,

    /// <summary>
    /// Buckets for up to 300 seconds.
    /// </summary>
    DefaultLongSeconds,
}
// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Metrics;

internal enum MetricPointStatus
{
    /// <summary>
    /// This status is applied to <see cref="MetricPoint"/>s with status <see cref="CollectPending"/> after a Collect.
    /// If an update occurs, status will be moved to <see cref="CollectPending"/>.
    /// </summary>
    NoCollectPending,

    /// <summary>
    /// The <see cref="MetricPoint"/> has been updated since the previous Collect cycle.
    /// Collect will move it to <see cref="NoCollectPending"/>.
    /// </summary>
    CollectPending,
}
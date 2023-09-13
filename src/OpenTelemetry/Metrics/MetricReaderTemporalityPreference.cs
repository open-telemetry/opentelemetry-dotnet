// <copyright file="MetricReaderTemporalityPreference.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

namespace OpenTelemetry.Metrics;

/// <summary>
/// Defines the behavior of a <see cref="MetricReader" />
/// with respect to <see cref="AggregationTemporality" />.
/// </summary>
public enum MetricReaderTemporalityPreference
{
    /// <summary>
    /// All aggregations are performed using cumulative temporality.
    /// </summary>
    Cumulative = 1,

    /// <summary>
    /// All measurements that are monotonic in nature are aggregated using delta temporality.
    /// Aggregations of non-monotonic measurements use cumulative temporality.
    /// </summary>
    Delta = 2,
}

// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Metrics;

[AttributeUsage(AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
internal sealed class MetricAggregatorBehaviorsAttribute : Attribute
{
    public MetricAggregatorBehaviorsAttribute(MetricAggregatorBehaviors metricAggregatorBehaviors)
    {
        this.MetricAggregatorBehaviors = metricAggregatorBehaviors;
    }

    public MetricAggregatorBehaviors MetricAggregatorBehaviors { get; }
}

// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Metrics;

[AttributeUsage(AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
internal sealed class MetricPointBehaviorsAttribute : Attribute
{
    public MetricPointBehaviorsAttribute(MetricPointBehaviors metricPointBehaviors)
    {
        this.MetricPointBehaviors = metricPointBehaviors;
    }

    public MetricPointBehaviors MetricPointBehaviors { get; }
}

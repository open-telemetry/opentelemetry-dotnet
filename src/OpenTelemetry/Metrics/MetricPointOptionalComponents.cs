// <copyright file="MetricPointOptionalComponents.cs" company="OpenTelemetry Authors">
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

#nullable enable

namespace OpenTelemetry.Metrics;

/// <summary>
/// Stores optional components of a metric point.
/// Histogram, Exemplar are current components.
/// ExponentialHistogram is a future component.
/// This is done to keep the MetricPoint (struct)
/// size in control.
/// </summary>
internal sealed class MetricPointOptionalComponents
{
    public HistogramBuckets? HistogramBuckets;

    public Base2ExponentialBucketHistogram? Base2ExponentialBucketHistogram;

    public ExemplarReservoir? ExemplarReservoir;

    public Exemplar[]? Exemplars;

    public int IsCriticalSectionOccupied = 0;

    internal MetricPointOptionalComponents Copy()
    {
        MetricPointOptionalComponents copy = new MetricPointOptionalComponents
        {
            HistogramBuckets = this.HistogramBuckets?.Copy(),
            Base2ExponentialBucketHistogram = this.Base2ExponentialBucketHistogram?.Copy(),
        };

        if (this.Exemplars != null)
        {
            copy.Exemplars = new Exemplar[this.Exemplars.Length];
            Array.Copy(this.Exemplars, copy.Exemplars, this.Exemplars.Length);
        }

        return copy;
    }
}

// <copyright file="AlignedHistogramBucketExemplarReservoir.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics;

namespace OpenTelemetry.Metrics;

/// <summary>
/// The Exemplar Filter which samples measurement done inside context
/// of sampled activity (span).
/// </summary>
internal sealed class AlignedHistogramBucketExemplarReservoir : ExemplarReservoir
{
    private Exemplar[] runningExemplars;
    private Exemplar[] snapshotExemplars;

    private int size;
    private object lockObject = new object();
    private HistogramBuckets histogramBuckets;

    public AlignedHistogramBucketExemplarReservoir(HistogramBuckets histogramBuckets)
    {
        this.size = histogramBuckets.ExplicitBounds.Length;
        this.runningExemplars = new Exemplar[this.size];
        this.snapshotExemplars = new Exemplar[this.size];
        this.histogramBuckets = histogramBuckets;
    }


    public override void Offer(long value, ReadOnlySpan<KeyValuePair<string, object>> tags)
    {
        int i = this.histogramBuckets.FindBucketIndex(value);
        lock (this.lockObject)
        {
            var exemplar = default(Exemplar);
            exemplar.Timestamp = DateTime.UtcNow;
            exemplar.LongValue = value;
            exemplar.TraceId = Activity.Current?.TraceId;
            exemplar.SpanId = Activity.Current?.SpanId;
            this.runningExemplars[i] = exemplar;
        }
    }

    public override void Offer(double value, ReadOnlySpan<KeyValuePair<string, object>> tags)
    {
        // TODO: Replace simple lock with alternates.
        // TODO: Avoid finding index twice, once inside MP and
        // and one here.
        int i = this.histogramBuckets.FindBucketIndex(value);
        lock (this.lockObject)
        {
            var exemplar = default(Exemplar);
            exemplar.Timestamp = DateTime.UtcNow;
            exemplar.DoubleValue = value;
            exemplar.TraceId = Activity.Current?.TraceId;
            exemplar.SpanId = Activity.Current?.SpanId;
            this.runningExemplars[i] = exemplar;
        }
    }

    public override Exemplar[] Collect()
    {
        return this.snapshotExemplars;
    }

    public override void SnapShot()
    {
        lock (this.lockObject)
        {
            for (int i = 0; i < this.size; i++)
            {
                this.snapshotExemplars[i] = this.runningExemplars[i];
                this.runningExemplars[i].Timestamp = default;
            }
        }
    }
}

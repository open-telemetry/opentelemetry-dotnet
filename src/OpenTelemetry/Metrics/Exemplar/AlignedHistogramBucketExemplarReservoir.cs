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

    private long numberOfMeasurementsSeen;
    private int size;
    private object lockObject = new object();

    public AlignedHistogramBucketExemplarReservoir(int bucketCount)
    {
        this.size = bucketCount;
        this.runningExemplars = new Exemplar[bucketCount];
        this.snapshotExemplars = new Exemplar[bucketCount];
    }

    public override void Offer(long value, ReadOnlySpan<KeyValuePair<string, object>> tags)
    {
        lock (this.lockObject)
        {
            var exemplar = new Exemplar();
            exemplar.Timestamp = DateTime.UtcNow;
            exemplar.LongValue = value;
            exemplar.TraceId = Activity.Current?.TraceId.ToHexString();
            exemplar.SpanId = Activity.Current?.SpanId.ToHexString();
            this.runningExemplars[numberOfMeasurementsSeen] = exemplar;
            numberOfMeasurementsSeen++;
        }
    }

    public override void Offer(double value, ReadOnlySpan<KeyValuePair<string, object>> tags)
    {
    }

    public override Exemplar[] Collect()
    {
        lock (this.lockObject)
        {
            for (int i = 0 ; i < this.size; i++)
            {
                this.snapshotExemplars[i] = this.runningExemplars[i];
            }
        }

        return this.snapshotExemplars;
    }
}

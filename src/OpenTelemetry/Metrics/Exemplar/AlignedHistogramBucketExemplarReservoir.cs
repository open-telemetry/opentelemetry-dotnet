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
/// The AlignedHistogramBucketExemplarReservoir implementation.
/// </summary>
internal sealed class AlignedHistogramBucketExemplarReservoir
{
    private readonly Exemplar[] runningExemplars;
    private readonly Exemplar[] snapshotExemplars;

    public AlignedHistogramBucketExemplarReservoir(int length)
    {
        this.runningExemplars = new Exemplar[length + 1];
        this.snapshotExemplars = new Exemplar[length + 1];
    }

    public void OfferAtBoundary(int index, double value)
    {
        var exemplar = default(Exemplar);
        exemplar.Timestamp = DateTime.UtcNow;
        exemplar.DoubleValue = value;
        exemplar.TraceId = Activity.Current?.TraceId;
        exemplar.SpanId = Activity.Current?.SpanId;
        this.runningExemplars[index] = exemplar;
    }

    public Exemplar[] Collect()
    {
        return this.snapshotExemplars;
    }

    public void SnapShot(bool reset)
    {
        for (int i = 0; i < this.runningExemplars.Length; i++)
        {
            this.snapshotExemplars[i] = this.runningExemplars[i];
            if (reset)
            {
                this.runningExemplars[i].Timestamp = default;
            }
        }
    }
}

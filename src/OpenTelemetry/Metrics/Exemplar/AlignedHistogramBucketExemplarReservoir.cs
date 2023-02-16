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

    public void OfferAtBoundary(int index, double value, ReadOnlySpan<KeyValuePair<string, object>> tags)
    {
        ref var exemplar = ref this.runningExemplars[index];
        exemplar.Timestamp = DateTime.UtcNow;
        exemplar.DoubleValue = value;
        exemplar.TraceId = Activity.Current?.TraceId;
        exemplar.SpanId = Activity.Current?.SpanId;

        if (tags == default)
        {
            // default tag is used to indicate
            // the special case where all tags provided at measurement
            // recording time are stored.
            // In this case, Exemplars does not have to store any tags.
            // In other words, FilteredTags will be empty.
            return;
        }

        if (exemplar.AllTags == null)
        {
            exemplar.AllTags = new List<KeyValuePair<string, object>>(tags.Length);
        }
        else
        {
            // Keep the list, but clear contents.
            exemplar.AllTags.Clear();
        }

        // Though only those tags that are filtered need to be
        // stored, finding filtered list from the full tag list
        // is expensive. So all the tags are stored in hot path (this).
        // During snapshot, the filtered list is calculated.
        // TODO: Evaluate alternative approaches based on perf.
        foreach (var tag in tags)
        {
            exemplar.AllTags.Add(tag);
        }
    }

    public Exemplar[] Collect()
    {
        return this.snapshotExemplars;
    }

    public void SnapShot(ReadOnlyTagCollection actualTags, bool reset)
    {
        for (int i = 0; i < this.runningExemplars.Length; i++)
        {
            this.snapshotExemplars[i] = this.runningExemplars[i];
            if (this.snapshotExemplars[i].AllTags != null)
            {
                // TODO: Better data structure to avoid this Linq.
                // This is doing filtered = alltags - storedtags.
                this.snapshotExemplars[i].FilteredTags = this.snapshotExemplars[i].AllTags.Except(actualTags.KeyAndValues.ToList()).ToList();
            }

            if (reset)
            {
                this.runningExemplars[i].Timestamp = default;
            }
        }
    }
}

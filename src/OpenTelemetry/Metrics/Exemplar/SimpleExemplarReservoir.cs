// <copyright file="SimpleExemplarReservoir.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics;

namespace OpenTelemetry.Metrics;

/// <summary>
/// The SimpleExemplarReservoir implementation.
/// </summary>
internal sealed class SimpleExemplarReservoir : ExemplarReservoir
{
    private readonly int poolSize;
    private readonly Random random;
    private readonly Exemplar[] runningExemplars;
    private readonly Exemplar[] tempExemplars;

    private long measurementsSeen;

    public SimpleExemplarReservoir(int poolSize)
    {
        this.poolSize = poolSize;
        this.runningExemplars = new Exemplar[poolSize];
        this.tempExemplars = new Exemplar[poolSize];
        this.measurementsSeen = 0;
        this.random = new Random();
    }

    public override void Offer(long value, ReadOnlySpan<KeyValuePair<string, object?>> tags, int index = default)
    {
        this.Offer(value, tags);
    }

    public override void Offer(double value, ReadOnlySpan<KeyValuePair<string, object?>> tags, int index = default)
    {
        this.Offer(value, tags);
    }

    public override Exemplar[] Collect(ReadOnlyTagCollection actualTags, bool reset)
    {
        for (int i = 0; i < this.runningExemplars.Length; i++)
        {
            this.tempExemplars[i] = this.runningExemplars[i];
            if (this.runningExemplars[i].FilteredTags != null)
            {
                // TODO: Better data structure to avoid this Linq.
                // This is doing filtered = alltags - storedtags.
                // TODO: At this stage, this logic is done inside Reservoir.
                // Kinda hard for end users who write own reservoirs.
                // Evaluate if this logic can be moved elsewhere.
                // TODO: The cost is paid irrespective of whether the
                // Exporter supports Exemplar or not. One idea is to
                // defer this until first exporter attempts read.
                this.tempExemplars[i].FilteredTags = this.runningExemplars[i].FilteredTags!.Except(actualTags.KeyAndValues.ToList()).ToList();
            }

            if (reset)
            {
                this.runningExemplars[i].Timestamp = default;
                this.measurementsSeen = 0;
            }
        }

        return this.tempExemplars;
    }

    private void Offer(double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        if (this.measurementsSeen < this.poolSize)
        {
            ref var exemplar = ref this.runningExemplars[this.measurementsSeen];
            exemplar.Timestamp = DateTimeOffset.UtcNow;
            exemplar.DoubleValue = value;
            exemplar.TraceId = Activity.Current?.TraceId;
            exemplar.SpanId = Activity.Current?.SpanId;
            this.StoreTags(ref exemplar, tags);
        }
        else
        {
            // TODO: RandomNext64 is only available in .NET 6 or newer.
            int upperBound = 0;
            unchecked
            {
                upperBound = (int)this.measurementsSeen;
            }

            var index = this.random.Next(0, upperBound);
            if (index < this.poolSize)
            {
                ref var exemplar = ref this.runningExemplars[index];
                exemplar.Timestamp = DateTimeOffset.UtcNow;
                exemplar.DoubleValue = value;
                exemplar.TraceId = Activity.Current?.TraceId;
                exemplar.SpanId = Activity.Current?.SpanId;
                this.StoreTags(ref exemplar, tags);
            }
        }

        this.measurementsSeen++;
    }

    private void StoreTags(ref Exemplar exemplar, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        if (tags == default)
        {
            // default tag is used to indicate
            // the special case where all tags provided at measurement
            // recording time are stored.
            // In this case, Exemplars does not have to store any tags.
            // In other words, FilteredTags will be empty.
            return;
        }

        if (exemplar.FilteredTags == null)
        {
            exemplar.FilteredTags = new List<KeyValuePair<string, object?>>(tags.Length);
        }
        else
        {
            // Keep the list, but clear contents.
            exemplar.FilteredTags.Clear();
        }

        // Though only those tags that are filtered need to be
        // stored, finding filtered list from the full tag list
        // is expensive. So all the tags are stored in hot path (this).
        // During snapshot, the filtered list is calculated.
        // TODO: Evaluate alternative approaches based on perf.
        // TODO: This is not user friendly to Reservoir authors
        // and must be handled as transparently as feasible.
        foreach (var tag in tags)
        {
            exemplar.FilteredTags.Add(tag);
        }
    }
}

// <copyright file="CumulativeMutableViewData.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
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
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using OpenTelemetry.DistributedContext;

namespace OpenTelemetry.Stats
{
    internal class CumulativeMutableViewData : MutableViewData
    {
        private readonly IDictionary<TagValues, MutableAggregation> tagValueAggregationMap = new ConcurrentDictionary<TagValues, MutableAggregation>();
        private DateTimeOffset start;

        internal CumulativeMutableViewData(IView view, DateTimeOffset start)
            : base(view)
        {
            this.start = start;
        }

        internal override void Record(ITagContext context, double value, DateTimeOffset timestamp)
        {
            var values = GetTagValues(GetTagMap(context), this.View.Columns);
            var tagValues = TagValues.Create(values);
            if (!this.tagValueAggregationMap.ContainsKey(tagValues))
            {
                this.tagValueAggregationMap.Add(tagValues, CreateMutableAggregation(this.View.Aggregation));
            }

            this.tagValueAggregationMap[tagValues].Add(value);
        }

        internal override IViewData ToViewData(DateTimeOffset now, StatsCollectionState state)
        {
            if (state == StatsCollectionState.ENABLED)
            {
                return ViewData.Create(
                    this.View,
                    CreateAggregationMap(this.tagValueAggregationMap, this.View.Measure),
                    this.start,
                    now);
            }
            else
            {
                // If Stats state is DISABLED, return an empty ViewData.
                return ViewData.Create(
                    this.View,
                    new Dictionary<TagValues, IAggregationData>(),
                    DateTimeOffset.MinValue,
                    DateTimeOffset.MinValue);
            }
        }

        internal override void ClearStats()
        {
            this.tagValueAggregationMap.Clear();
        }

        internal override void ResumeStatsCollection(DateTimeOffset now)
        {
            this.start = now;
        }
    }
}

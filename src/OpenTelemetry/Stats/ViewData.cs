// <copyright file="ViewData.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Stats
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using OpenTelemetry.Stats.Aggregations;
    using OpenTelemetry.Tags;
    using OpenTelemetry.Utils;

    public sealed class ViewData : IViewData
    {
        internal ViewData(IView view, IDictionary<TagValues, IAggregationData> aggregationMap, DateTimeOffset start, DateTimeOffset end)
        {
            this.View = view ?? throw new ArgumentNullException(nameof(view));
            this.AggregationMap = aggregationMap ?? throw new ArgumentNullException(nameof(aggregationMap));
            this.Start = start;
            this.End = end;
        }

        public IView View { get; }

        public IDictionary<TagValues, IAggregationData> AggregationMap { get; }

        public DateTimeOffset Start { get; }

        public DateTimeOffset End { get; }

        public static IViewData Create(IView view, IDictionary<TagValues, IAggregationData> map, DateTimeOffset start, DateTimeOffset end)
        {
            IDictionary<TagValues, IAggregationData> deepCopy = new Dictionary<TagValues, IAggregationData>();
            foreach (var entry in map)
            {
                CheckAggregation(view.Aggregation, entry.Value, view.Measure);
                deepCopy.Add(entry.Key, entry.Value);
            }

            return new ViewData(
                view,
                new ReadOnlyDictionary<TagValues, IAggregationData>(deepCopy),
                start,
                end);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return "ViewData{"
                + "view=" + this.View + ", "
                + "aggregationMap=" + Collections.ToString(this.AggregationMap) + ", "
                + "start=" + this.Start + ", "
                + "end=" + this.End
                + "}";
        }

        /// <inheritdoc/>
        public override bool Equals(object o)
        {
            if (o == this)
            {
                return true;
            }

            if (o is ViewData that)
            {
                return this.View.Equals(that.View)
                     && this.AggregationMap.SequenceEqual(that.AggregationMap)
                     && this.Start.Equals(that.Start)
                     && this.End.Equals(that.End);
            }

            return false;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            int h = 1;
            h *= 1000003;
            h ^= this.View.GetHashCode();
            h *= 1000003;
            h ^= this.AggregationMap.GetHashCode();
            h *= 1000003;
            h ^= this.Start.GetHashCode();
            h *= 1000003;
            h ^= this.End.GetHashCode();
            return h;
        }

        private static void CheckAggregation(IAggregation aggregation, IAggregationData aggregationData, IMeasure measure)
        {
            aggregation.Match<object>(
                (arg) =>
                {
                    measure.Match<object>(
                        (arg1) =>
                        {
                            if (!(aggregationData is ISumDataDouble))
                            {
                                throw new ArgumentException(CreateErrorMessageForAggregation(aggregation, aggregationData));
                            }

                            return null;
                        },
                        (arg1) =>
                        {
                            if (!(aggregationData is ISumDataLong))
                            {
                                throw new ArgumentException(CreateErrorMessageForAggregation(aggregation, aggregationData));
                            }

                            return null;
                        },
                        (arg1) =>
                        {
                            throw new ArgumentException();
                        });
                    return null;
                },
                (arg) =>
                {
                    if (!(aggregationData is ICountData))
                    {
                        throw new ArgumentException(CreateErrorMessageForAggregation(aggregation, aggregationData));
                    }

                    return null;
                },
                (arg) =>
                {
                    if (!(aggregationData is IMeanData))
                    {
                        throw new ArgumentException(CreateErrorMessageForAggregation(aggregation, aggregationData));
                    }

                    return null;
                },
                (arg) =>
                {
                    if (!(aggregationData is IDistributionData))
                    {
                        throw new ArgumentException(CreateErrorMessageForAggregation(aggregation, aggregationData));
                    }

                    return null;
                },
                (arg) =>
                {
                    measure.Match<object>(
                        (arg1) =>
                        {
                            if (!(aggregationData is ILastValueDataDouble))
                            {
                                throw new ArgumentException(CreateErrorMessageForAggregation(aggregation, aggregationData));
                            }

                            return null;
                        },
                        (arg1) =>
                        {
                            if (!(aggregationData is ILastValueDataLong))
                            {
                                throw new ArgumentException(CreateErrorMessageForAggregation(aggregation, aggregationData));
                            }

                            return null;
                        },
                        (arg1) =>
                        {
                            throw new ArgumentException();
                        });
                    return null;
                },
                (arg) =>
                {
                    throw new ArgumentException();
                });
        }

        private static string CreateErrorMessageForAggregation(IAggregation aggregation, IAggregationData aggregationData)
        {
            return "Aggregation and AggregationData types mismatch. "
                + "Aggregation: "
                + aggregation
                + " AggregationData: "
                + aggregationData;
        }
    }
}

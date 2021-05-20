// <copyright file="ThreadStaticStorage.cs" company="OpenTelemetry Authors">
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

using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;

namespace OpenTelemetry.Metrics
{
    internal class ThreadStaticStorage
    {
        private const int MaxTagCacheSize = 3;

        [ThreadStatic]
        private static ThreadStaticStorage storage;

        private readonly TagStorage[] tagStorage = new TagStorage[MaxTagCacheSize + 1];

        // .NET Metrics API allows for byte, short, int, long, float, double, decimal
        private readonly Dictionary<Type, IDataPoint> pointT = new Dictionary<Type, IDataPoint>();

        private readonly MeasurementItem measurementItem = new MeasurementItem();

        private ThreadStaticStorage()
        {
            for (int i = 0; i <= MaxTagCacheSize; i++)
            {
                this.tagStorage[i] = new TagStorage(i);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ThreadStaticStorage GetStorage()
        {
            if (ThreadStaticStorage.storage == null)
            {
                ThreadStaticStorage.storage = new ThreadStaticStorage();
            }

            return ThreadStaticStorage.storage;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal KeyValuePair<string, object>[] GetTags(ReadOnlySpan<KeyValuePair<string, object>> tagsRos)
        {
            var len = tagsRos.Length;

            KeyValuePair<string, object>[] tags;

            if (len == 0)
            {
                tags = this.tagStorage[0].Tags;
            }
            else if (len <= MaxTagCacheSize)
            {
                tags = this.tagStorage[len].Tags;

                int i = 0;
                foreach (var tag in tagsRos)
                {
                    tags[i++] = tag;
                }
            }
            else
            {
                tags = tagsRos.ToArray();
            }

            return tags;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal MeasurementItem GetMeasurementItem(Instrument instrument, InstrumentState state, IDataPoint point)
        {
            this.measurementItem.Instrument = instrument;
            this.measurementItem.State = state;
            this.measurementItem.Point = point;

            return this.measurementItem;
        }

        internal IDataPoint GetDataPoint<T>(T value, KeyValuePair<string, object>[] tags)
            where T : struct
        {
            if (!this.pointT.TryGetValue(typeof(T), out var dp))
            {
                dp = new DataPoint<T>(value, tags);
                this.pointT.Add(typeof(T), dp);
            }
            else
            {
                // We are resetting/reusing the same DataPoint<T> object to avoid a new alloc in the hot path.
                if (dp is DataPoint<T> dpT)
                {
                    dpT.Reset(value, tags);
                }
            }

            return dp;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void GetKeysValues(KeyValuePair<string, object>[] tags, out string[] tagKeys, out object[] tagValues)
        {
            var len = tags.Length;

            if (len <= MaxTagCacheSize)
            {
                tagKeys = this.tagStorage[len].TagKey;
                tagValues = this.tagStorage[len].TagValue;
            }
            else
            {
                tagKeys = new string[len];
                tagValues = new object[len];
            }

            for (var n = 0; n < len; n++)
            {
                tagKeys[n] = tags[n].Key;
                tagValues[n] = tags[n].Value;
            }
        }

        internal class TagStorage
        {
            // Used to copy ReadOnlySpan from API
            internal readonly KeyValuePair<string, object>[] Tags;

            // Used to split into Key sequence, Value sequence, and KVPs for Aggregator Processor
            internal readonly string[] TagKey;
            internal readonly object[] TagValue;

            internal TagStorage(int n)
            {
                this.Tags = new KeyValuePair<string, object>[n];

                this.TagKey = new string[n];
                this.TagValue = new object[n];
            }
        }
    }
}

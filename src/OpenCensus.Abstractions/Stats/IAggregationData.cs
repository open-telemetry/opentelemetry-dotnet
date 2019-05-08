// <copyright file="IAggregationData.cs" company="OpenCensus Authors">
// Copyright 2018, OpenCensus Authors
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

namespace OpenCensus.Stats
{
    using System;
    using OpenCensus.Stats.Aggregations;

    /// <summary>
    /// Gets the aggregation data.
    /// </summary>
    public interface IAggregationData
    {
        /// <summary>
        /// Executes aggregation data specific callback without type casting.
        /// </summary>
        /// <typeparam name="T">Callback result type.</typeparam>
        /// <param name="p0">Callback for the double sum data.</param>
        /// <param name="p1">Callback for the long sum data.</param>
        /// <param name="p2">Callback for the count data.</param>
        /// <param name="p3">Callback for the mean data.</param>
        /// <param name="p4">Callback for the distribution data.</param>
        /// <param name="p5">Callback for the double last value data.</param>
        /// <param name="p6">Callback for the long last value data.</param>
        /// <param name="defaultFunction">Callback for any other data.</param>
        /// <returns>Callback executuion result.</returns>
        T Match<T>(
             Func<ISumDataDouble, T> p0,
             Func<ISumDataLong, T> p1,
             Func<ICountData, T> p2,
             Func<IMeanData, T> p3,
             Func<IDistributionData, T> p4,
             Func<ILastValueDataDouble, T> p5,
             Func<ILastValueDataLong, T> p6,
             Func<IAggregationData, T> defaultFunction);
    }
}

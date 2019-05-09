// <copyright file="IAggregation.cs" company="OpenCensus Authors">
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
    /// Represents the type of aggregation.
    /// </summary>
    public interface IAggregation
    {
        /// <summary>
        /// Executed callback specific to aggregation without type casting.
        /// </summary>
        /// <typeparam name="T">Expected return value.</typeparam>
        /// <param name="p0">Callback to be called by sum aggregation.</param>
        /// <param name="p1">Callback to be called by count aggregation.</param>
        /// <param name="p2">Callback to be called by mean aggregation.</param>
        /// <param name="p3">Callback to be called by distribution aggregation.</param>
        /// <param name="p4">Callback to be called by last value aggregation.</param>
        /// <param name="p6">Callback to be called for any other aggregation.</param>
        /// <returns>The result of the aggregator-specific callback execution.</returns>
        T Match<T>(
             Func<ISum, T> p0,
             Func<ICount, T> p1,
             Func<IMean, T> p2,
             Func<IDistribution, T> p3,
             Func<ILastValue, T> p4,
             Func<IAggregation, T> p6);
    }
}

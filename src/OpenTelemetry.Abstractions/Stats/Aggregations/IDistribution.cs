﻿// <copyright file="IDistribution.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Stats.Aggregations
{
    /// <summary>
    /// Distribution aggregation.
    /// </summary>
    public interface IDistribution : IAggregation
    {
        /// <summary>
        /// Gets the configuration for distribution aggregation - distribution bucket boundaries.
        /// </summary>
        IBucketBoundaries BucketBoundaries { get; }
    }
}

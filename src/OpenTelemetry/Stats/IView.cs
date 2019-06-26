// <copyright file="IView.cs" company="OpenTelemetry Authors">
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
    using System.Collections.Generic;
    using OpenTelemetry.Tags;

    /// <summary>
    /// Stats recording view.
    /// </summary>
    public interface IView
    {
        /// <summary>
        /// Gets the name of the view.
        /// </summary>
        IViewName Name { get; }

        /// <summary>
        /// Gets the description of the view.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Gets the measure this view record values for.
        /// </summary>
        IMeasure Measure { get; }

        /// <summary>
        /// Gets the aggregation is used by this view.
        /// </summary>
        IAggregation Aggregation { get; }

        /// <summary>
        /// Gets the columns (dimensions) recorded by this view.
        /// </summary>
        IReadOnlyList<TagKey> Columns { get; }
    }
}

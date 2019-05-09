// <copyright file="IRunningSpanStoreFilter.cs" company="OpenCensus Authors">
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

namespace OpenCensus.Trace.Export
{
    /// <summary>
    /// Filter to query spans from the running spans store.
    /// </summary>
    public interface IRunningSpanStoreFilter
    {
        /// <summary>
        /// Gets the name of the span to query.
        /// </summary>
        string SpanName { get; }

        /// <summary>
        /// Gets the maximum number of spans to return.
        /// </summary>
        int MaxSpansToReturn { get; }
    }
}

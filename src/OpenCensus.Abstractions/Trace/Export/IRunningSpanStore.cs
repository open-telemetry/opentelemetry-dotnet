// <copyright file="IRunningSpanStore.cs" company="OpenCensus Authors">
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
    using System.Collections.Generic;

    /// <summary>
    /// Running spans store.
    /// </summary>
    public interface IRunningSpanStore
    {
        /// <summary>
        /// Gets the summary of this store.
        /// </summary>
        IRunningSpanStoreSummary Summary { get; }

        /// <summary>
        /// Gets the list of all running spans with the applied filter.
        /// </summary>
        /// <param name="filter">Filter to apply to query running spans.</param>
        /// <returns>List of currently running spans.</returns>
        IEnumerable<ISpanData> GetRunningSpans(IRunningSpanStoreFilter filter);

        /// <summary>
        /// Called when span got started.
        /// </summary>
        /// <param name="span">Span that was just started.</param>
        void OnStart(ISpan span);

        /// <summary>
        /// Called when span just ended.
        /// </summary>
        /// <param name="span">Span that just ended.</param>
        void OnEnd(ISpan span);
    }
}

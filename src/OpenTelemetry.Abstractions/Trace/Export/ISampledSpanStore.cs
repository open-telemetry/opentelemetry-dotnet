// <copyright file="ISampledSpanStore.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace.Export
{
    using System.Collections.Generic;

    /// <summary>
    /// Samples spans store.
    /// </summary>
    public interface ISampledSpanStore
    {
        /// <summary>
        /// Gets the summary of sampled spans.
        /// </summary>
        ISampledSpanStoreSummary Summary { get; }

        /// <summary>
        /// Gets all registered span names.
        /// </summary>
        ISet<string> RegisteredSpanNamesForCollection { get; }

        /// <summary>
        /// Gets the list of sampled spans using the provided filter.
        /// </summary>
        /// <param name="filter">Filter to use to query sampled store.</param>
        /// <returns>List of spans satisfying filtering criteria.</returns>
        IEnumerable<ISpanData> GetLatencySampledSpans(ISampledSpanStoreLatencyFilter filter);

        /// <summary>
        /// Gets the list of error spans using provided error filter.
        /// </summary>
        /// <param name="filter">Filter to use to query store.</param>
        /// <returns>List of sampled spans satisfying filtering criteria.</returns>
        IEnumerable<ISpanData> GetErrorSampledSpans(ISampledSpanStoreErrorFilter filter);

        /// <summary>
        /// Registers span names for collection.
        /// </summary>
        /// <param name="spanNames">List of span names.</param>
        void RegisterSpanNamesForCollection(IEnumerable<string> spanNames);

        /// <summary>
        /// Unregister span names for the collection.
        /// </summary>
        /// <param name="spanNames">Span names to unregister.</param>
        void UnregisterSpanNamesForCollection(IEnumerable<string> spanNames);

        /// <summary>
        /// Consider span for sampling.
        /// </summary>
        /// <param name="span">Span to consider.</param>
        void ConsiderForSampling(ISpan span);
    }
}

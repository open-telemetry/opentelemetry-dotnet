// <copyright file="EndSpanOptionsBuilder.cs" company="OpenCensus Authors">
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

namespace OpenCensus.Trace
{
    using System;

    /// <summary>
    /// End span options builder.
    /// </summary>
    public class EndSpanOptionsBuilder
    {
        private bool? sampleToLocalSpanStore;
        private Status status;

        internal EndSpanOptionsBuilder()
        {
        }

        /// <summary>
        /// Indicate whether span is intended for local spans store.
        /// </summary>
        /// <param name="sampleToLocalSpanStore">Value indicating whether span is intended for local span store.</param>
        /// <returns>Span options builder for operations chaining.</returns>
        public EndSpanOptionsBuilder SetSampleToLocalSpanStore(bool sampleToLocalSpanStore)
        {
            this.sampleToLocalSpanStore = sampleToLocalSpanStore;
            return this;
        }

        /// <summary>
        /// Sets the span status.
        /// </summary>
        /// <param name="status">Span status.</param>
        /// <returns>Span options builder for the operations chaining.</returns>
        public EndSpanOptionsBuilder SetStatus(Status status)
        {
            this.status = status;
            return this;
        }

        /// <summary>
        /// Builds the span options.
        /// </summary>
        /// <returns>Span options instance.</returns>
        public EndSpanOptions Build()
        {
            string missing = string.Empty;
            if (!this.sampleToLocalSpanStore.HasValue)
            {
                missing += " sampleToLocalSpanStore";
            }

            if (!string.IsNullOrEmpty(missing))
            {
                throw new ArgumentOutOfRangeException("Missing required properties:" + missing);
            }

            return new EndSpanOptions(
                this.sampleToLocalSpanStore.Value,
                this.status);
        }
    }
}

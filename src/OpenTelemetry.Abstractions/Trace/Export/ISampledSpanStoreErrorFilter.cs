﻿// <copyright file="ISampledSpanStoreErrorFilter.cs" company="OpenTelemetry Authors">
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
    /// <summary>
    /// Sampled spans store error filter.
    /// </summary>
    public interface ISampledSpanStoreErrorFilter
    {
        /// <summary>
        /// Gets the span name to use to filter.
        /// </summary>
        string SpanName { get; }

        /// <summary>
        /// Gets the cannonical code to use to filter.
        /// </summary>
        CanonicalCode? CanonicalCode { get; }

        /// <summary>
        /// Gets the maximum number of spans to return.
        /// </summary>
        int MaxSpansToReturn { get; }
    }
}

// <copyright file="ITraceParams.cs" company="OpenCensus Authors">
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

namespace OpenCensus.Trace.Config
{
    /// <summary>
    /// Trace parameters that can be updates in runtime.
    /// </summary>
    public interface ITraceParams
    {
        /// <summary>
        /// Gets the sampler.
        /// </summary>
        ISampler Sampler { get; }

        /// <summary>
        /// Gets the maximum number of attributes on span.
        /// </summary>
        int MaxNumberOfAttributes { get; }

        /// <summary>
        /// Gets that maximum Number of annotations on span.
        /// </summary>
        int MaxNumberOfAnnotations { get; }

        /// <summary>
        /// Gets the maximum number of messages on span.
        /// </summary>
        int MaxNumberOfMessageEvents { get; }

        /// <summary>
        /// Gets the maximum number of links on span.
        /// </summary>
        int MaxNumberOfLinks { get; }

        /// <summary>
        /// Creates params builder preinitialized with the trace parameters supplied.
        /// </summary>
        /// <returns>Trace parameters builder.</returns>
        TraceParamsBuilder ToBuilder();
    }
}

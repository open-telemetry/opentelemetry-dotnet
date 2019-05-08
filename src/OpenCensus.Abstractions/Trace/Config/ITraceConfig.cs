// <copyright file="ITraceConfig.cs" company="OpenCensus Authors">
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
    /// Trace configuration.
    /// </summary>
    public interface ITraceConfig
    {
        /// <summary>
        /// Gets ths active trace parameters that can be updated in runtime.
        /// </summary>
        ITraceParams ActiveTraceParams { get; }

        /// <summary>
        /// Updates the active trace parameters.
        /// </summary>
        /// <param name="traceParams">New trace parameters to use.</param>
        void UpdateActiveTraceParams(ITraceParams traceParams);
    }
}
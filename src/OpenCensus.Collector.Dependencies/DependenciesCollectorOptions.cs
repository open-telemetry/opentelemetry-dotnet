// <copyright file="DependenciesCollectorOptions.cs" company="OpenCensus Authors">
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

namespace OpenCensus.Collector.Dependencies
{
    using System;
    using System.Net.Http;
    using OpenCensus.Trace;
    using OpenCensus.Trace.Sampler;

    /// <summary>
    /// Options for dependencies collector.
    /// </summary>
    public class DependenciesCollectorOptions
    {
        private static Func<HttpRequestMessage, ISampler> defaultSampler = (req) => { return ((req.RequestUri != null) && req.RequestUri.ToString().Contains("zipkin.azurewebsites.net")) ? Samplers.NeverSample : null; };

        /// <summary>
        /// Initializes a new instance of the <see cref="DependenciesCollectorOptions"/> class.
        /// </summary>
        /// <param name="sampler">Custom sampling function, if any</param>
        public DependenciesCollectorOptions(Func<HttpRequestMessage, ISampler> sampler = null)
        {
            this.CustomSampler = sampler ?? defaultSampler;
        }

        /// <summary>
        /// Gets a hook to exclude calls based on domain
        /// or other per-request criterion.
        /// </summary>
        public Func<HttpRequestMessage, ISampler> CustomSampler { get; private set; }
    }
}

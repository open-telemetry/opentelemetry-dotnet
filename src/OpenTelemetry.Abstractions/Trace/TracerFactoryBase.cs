// <copyright file="TracerFactoryBase.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Creates Tracers for an instrumentation library.
    /// </summary>
    public class TracerFactoryBase
    {
        private static bool isInitialized;
        private readonly ProxyTracer proxy = new ProxyTracer();

        public static TracerFactoryBase Default { get; private set; } = new TracerFactoryBase();

        /// <summary>
        /// Returns an ITracer for a given name and version.
        /// </summary>
        /// <param name="name">Name of the instrumentation library.</param>
        /// <param name="version">Version of the instrumentation library (optional).</param>
        /// <returns>Tracer for the given name and version information.</returns>
        public virtual ITracer GetTracer(string name, string version = null)
        {
            return isInitialized ? Default.GetTracer(name, version) : this.proxy;
        }

        protected void Init(TracerFactoryBase factoryImplementation)
        {
            // if already init - throw
            if (!isInitialized)
            {
                // some libraries might have already used and cached ProxyTracer.
                // let's update it to real one and forward all calls.

                // resource assignment is not possible for libraries that cache tracer before SDK is initialized.
                // SDK (Tracer) must be at least partially initialized before any collection starts to capture resources.
                // we might be able to work this around with events.
                this.proxy.UpdateTracer(factoryImplementation.GetTracer(null));

                Default = factoryImplementation;
                isInitialized = true;
            }
        }
    }
}

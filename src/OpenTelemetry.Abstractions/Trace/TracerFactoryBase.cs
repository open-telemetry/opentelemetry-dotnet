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
using System;

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Creates Tracers for an instrumentation library.
    /// </summary>
    public class TracerFactoryBase
    {
        private static ProxyTracer proxy = new ProxyTracer();
        private static bool isInitialized;
        private static TracerFactoryBase defaultFactory = new TracerFactoryBase();

        public static TracerFactoryBase Default
        {
            get => defaultFactory;
            set
            {
                if (isInitialized)
                {
                    throw new InvalidOperationException("Default factory is already set");
                }

                defaultFactory = value ?? throw new ArgumentNullException(nameof(value));

                // some libraries might have already used and cached ProxyTracer.
                // let's update it to real one and forward all calls.

                // resource assignment is not possible for libraries that cache tracer before SDK is initialized.
                // SDK (Tracer) must be at least partially initialized before any collection starts to capture resources.
                // we might be able to work this around with events.
                proxy.UpdateTracer(defaultFactory.GetTracer(null));

                isInitialized = true;
            }
        }

        /// <summary>
        /// Returns an ITracer for a given name and version.
        /// </summary>
        /// <param name="name">Name of the instrumentation library.</param>
        /// <param name="version">Version of the instrumentation library (optional).</param>
        /// <returns>Tracer for the given name and version information.</returns>
        public virtual ITracer GetTracer(string name, string version = null)
        {
            return isInitialized ? defaultFactory.GetTracer(name, version) : proxy;
        }

        // for tests
        internal void Reset()
        {
            proxy = new ProxyTracer();
            isInitialized = false;
            defaultFactory = new TracerFactoryBase();
        }
    }
}

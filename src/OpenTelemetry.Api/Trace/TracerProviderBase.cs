// <copyright file="TracerProviderBase.cs" company="OpenTelemetry Authors">
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
    public class TracerProviderBase
    {
        private static ProxyTracer proxy = new ProxyTracer();
        private static bool isInitialized;
        private static TracerProviderBase defaultProvider = new TracerProviderBase();

        /// <summary>
        /// Gets the default instance of <see cref="TracerProviderBase"/>.
        /// </summary>
        public static TracerProviderBase Default
        {
            get => defaultProvider;
        }

        /// <summary>
        /// Sets the default instance of <see cref="TracerProviderBase"/>.
        /// </summary>
        /// <param name="tracerProvider">Instance of <see cref="TracerProviderBase"/>.</param>
        /// <remarks>
        /// This method can only be called once. Calling it multiple times will throw an <see cref="System.InvalidOperationException"/>.
        /// </remarks>
        /// <exception cref="System.InvalidOperationException">Thrown when called multiple times.</exception>
        public static void SetDefault(TracerProviderBase tracerProvider)
        {
            if (isInitialized)
            {
                throw new InvalidOperationException("Default provider is already set");
            }

            defaultProvider = tracerProvider ?? throw new ArgumentNullException(nameof(tracerProvider));

            // some libraries might have already used and cached ProxyTracer.
            // let's update it to real one and forward all calls.

            // resource assignment is not possible for libraries that cache tracer before SDK is initialized.
            // SDK (Tracer) must be at least partially initialized before any collection starts to capture resources.
            // we might be able to work this around with events.
            proxy.UpdateTracer(defaultProvider.GetTracer(null));

            isInitialized = true;
        }

        /// <summary>
        /// Returns an Tracer for a given name and version.
        /// </summary>
        /// <param name="name">Name of the instrumentation library.</param>
        /// <param name="version">Version of the instrumentation library (optional).</param>
        /// <returns>Tracer for the given name and version information.</returns>
        public virtual Tracer GetTracer(string name, string version = null)
        {
            return isInitialized ? defaultProvider.GetTracer(name, version) : proxy;
        }

        // for tests
        internal void Reset()
        {
            proxy = new ProxyTracer();
            isInitialized = false;
            defaultProvider = new TracerProviderBase();
        }
    }
}

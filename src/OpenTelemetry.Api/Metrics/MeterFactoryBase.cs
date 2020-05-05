// <copyright file="MeterFactoryBase.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Metrics
{
    /// <summary>
    /// Creates Meters for an instrumentation library.
    /// Libraries should use this class as follows to obtain Meter instance.
    /// MeterFactoryBase.Default.GetMeter("libraryname", "version").
    /// </summary>
    public class MeterFactoryBase
    {
        private static ProxyMeter proxyMeter = new ProxyMeter();
        private static bool isInitialized;
        private static MeterFactoryBase defaultFactory = new MeterFactoryBase();

        /// <summary>
        /// Gets the dafult instance of a <see cref="MeterFactoryBase"/>.
        /// </summary>
        public static MeterFactoryBase Default
        {
            get => defaultFactory;
        }

        /// <summary>
        /// Sets the default instance of <see cref="MeterFactoryBase"/>.
        /// </summary>
        /// <param name="meterFactory">Instance of <see cref="MeterFactoryBase"/>.</param>
        /// <remarks>
        /// This method can only be called once. Calling it multiple times will throw an <see cref="System.InvalidOperationException"/>.
        /// </remarks>
        /// <exception cref="System.InvalidOperationException">Thrown when called multiple times.</exception>
        public static void SetDefault(MeterFactoryBase meterFactory)
        {
            if (isInitialized)
            {
                throw new InvalidOperationException("Default factory is already set");
            }

            defaultFactory = meterFactory ?? throw new ArgumentNullException(nameof(meterFactory));

            // some libraries might have already used and cached ProxyMeter.
            // let's update it to real one and forward all calls.

            // resource assignment is not possible for libraries that cache tracer before SDK is initialized.
            // SDK (Tracer) must be at least partially initialized before any collection starts to capture resources.
            // we might be able to work this around with events.
            proxyMeter.UpdateMeter(defaultFactory.GetMeter(null));

            isInitialized = true;
        }

        /// <summary>
        /// Returns a Meter for a given name and version.
        /// </summary>
        /// <param name="name">Name of the instrumentation library.</param>
        /// <param name="version">Version of the instrumentation library (optional).</param>
        /// <returns>Meter for the given name and version information.</returns>
        public virtual Meter GetMeter(string name, string version = null)
        {
            return isInitialized ? defaultFactory.GetMeter(name, version) : proxyMeter;
        }

        // for tests
        internal void Reset()
        {
            proxyMeter = new ProxyMeter();
            isInitialized = false;
            defaultFactory = new MeterFactoryBase();
        }
    }
}

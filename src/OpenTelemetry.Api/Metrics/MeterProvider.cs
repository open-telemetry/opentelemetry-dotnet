// <copyright file="MeterProvider.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
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
    /// MeterProvider is the entry point of the OpenTelemetry Metrics API. It provides access to Meters.
    /// </summary>
    public class MeterProvider : IDisposable
    {
        private static ProxyMeter proxyMeter = new ProxyMeter();
        private static bool isInitialized;
        private static MeterProvider defaultProvider = new MeterProvider();

        /// <summary>
        /// Initializes a new instance of the <see cref="MeterProvider"/> class.
        /// </summary>
        protected MeterProvider()
        {
        }

        /// <summary>
        /// Gets the dafult instance of a <see cref="MeterProvider"/>.
        /// </summary>
        public static MeterProvider Default
        {
            get => defaultProvider;
        }

        /// <summary>
        /// Sets the default instance of <see cref="MeterProvider"/>.
        /// </summary>
        /// <param name="meterProvider">Instance of <see cref="MeterProvider"/>.</param>
        /// <remarks>
        /// This method can only be called once. Calling it multiple times will throw an <see cref="System.InvalidOperationException"/>.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when called multiple times.</exception>
        public static void SetDefault(MeterProvider meterProvider)
        {
            if (isInitialized)
            {
                throw new InvalidOperationException("Default factory is already set");
            }

            defaultProvider = meterProvider ?? throw new ArgumentNullException(nameof(meterProvider));

            // some libraries might have already used and cached ProxyMeter.
            // let's update it to real one and forward all calls.

            // TODO:
            // resource assignment is not possible for libraries that cache meter before SDK is initialized.
            // SDK (Meter) must be at least partially initialized before any collection starts to capture resources.
            // we might be able to work this around in future.
            proxyMeter.UpdateMeter(defaultProvider.GetMeter(null));

            isInitialized = true;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Returns a Meter for a given name and version.
        /// </summary>
        /// <param name="name">Name of the instrumentation library.</param>
        /// <param name="version">Version of the instrumentation library (optional).</param>
        /// <returns>Meter for the given name and version information.</returns>
        public virtual Meter GetMeter(string name, string version = null)
        {
            return isInitialized ? defaultProvider.GetMeter(name, version) : proxyMeter;
        }

        // for tests
        internal static void Reset()
        {
            proxyMeter = new ProxyMeter();
            isInitialized = false;
            defaultProvider = new MeterProvider();
        }

        /// <summary>
        /// Releases the unmanaged resources used by this class and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing"><see langword="true"/> to release both managed and unmanaged resources; <see langword="false"/> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
        }
    }
}

// <copyright file="TracerProvider.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using System.Threading;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// TracerProvider is the entry point of the OpenTelemetry API. It provides access to <see cref="Tracer"/>.
    /// </summary>
    public class TracerProvider : BaseProvider
    {
        private int shutdownCount;

        /// <summary>
        /// Initializes a new instance of the <see cref="TracerProvider"/> class.
        /// </summary>
        protected TracerProvider()
        {
        }

        /// <summary>
        /// Gets the default Tracer.
        /// </summary>
        public static TracerProvider Default { get; } = new TracerProvider();

        /// <summary>
        /// Gets a tracer with given name and version.
        /// </summary>
        /// <param name="name">Name identifying the instrumentation library.</param>
        /// <param name="version">Version of the instrumentation library.</param>
        /// <returns>Tracer instance.</returns>
        public Tracer GetTracer(string name, string version = null)
        {
            if (name == null)
            {
                name = string.Empty;
            }

            return new Tracer(new ActivitySource(name, version));
        }

        /// <summary>
        /// Attempts to shutdown the processor, blocks the current thread until
        /// shutdown completed or timed out.
        /// </summary>
        /// <param name="timeoutMilliseconds">
        /// The number of milliseconds to wait, or <c>Timeout.Infinite</c> to
        /// wait indefinitely.
        /// </param>
        /// <returns>
        /// Returns <c>true</c> when shutdown succeeded; otherwise, <c>false</c>.
        /// </returns>
        /// <exception cref="System.ArgumentOutOfRangeException">
        /// Thrown when the <c>timeoutMilliseconds</c> is smaller than -1.
        /// </exception>
        /// <remarks>
        /// This function guarantees thread-safety. Only the first call will
        /// win, subsequent calls will be no-op.
        /// </remarks>
        public bool Shutdown(int timeoutMilliseconds = Timeout.Infinite)
        {
            if (timeoutMilliseconds < 0 && timeoutMilliseconds != Timeout.Infinite)
            {
                throw new ArgumentOutOfRangeException(nameof(timeoutMilliseconds), timeoutMilliseconds, "timeoutMilliseconds should be non-negative.");
            }

            if (Interlocked.Increment(ref this.shutdownCount) > 1)
            {
                return false; // shutdown already called
            }

            try
            {
                return this.OnShutdown(timeoutMilliseconds);
            }
            catch (Exception ex)
            {
                OpenTelemetryApiEventSource.Log.TracerProviderException(nameof(this.Shutdown), ex);
                return false;
            }
        }

        /// <summary>
        /// Called by <c>Shutdown</c>. This function should block the current
        /// thread until shutdown completed or timed out.
        /// </summary>
        /// <param name="timeoutMilliseconds">
        /// The number of milliseconds to wait, or <c>Timeout.Infinite</c> to
        /// wait indefinitely.
        /// </param>
        /// <returns>
        /// Returns <c>true</c> when shutdown succeeded; otherwise, <c>false</c>.
        /// </returns>
        /// <remarks>
        /// This function is called synchronously on the thread which made the
        /// first call to <c>Shutdown</c>. This function should not throw
        /// exceptions.
        /// </remarks>
        protected virtual bool OnShutdown(int timeoutMilliseconds)
        {
            return true;
        }
    }
}

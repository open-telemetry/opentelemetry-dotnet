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

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// TracerProvider is the entry point of the OpenTelemetry API. It provides access to Tracers.
    /// </summary>
    public class TracerProvider : IDisposable
    {
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

        /// <inheritdoc/>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
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

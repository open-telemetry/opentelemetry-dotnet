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

using System.Diagnostics;

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// TracerProvider is the entry point of the OTel API. It provides access to Tracers.
    /// </summary>
    public class TracerProvider
    {
        /// <summary>
        /// Gets a tracer with given name and version.
        /// </summary>
        /// <param name="name">Name identifying the instrumentation library.</param>
        /// <param name="version">Version of the instrumentation library.</param>
        /// <returns>Tracer instance.</returns>
        public static TracerNew GetTracer(string name, string version = null)
        {
            return new TracerNew(new ActivitySource(name, version));
        }
    }
}

// <copyright file="Propagators.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Context.Propagation
{
    /// <summary>
    /// Propagators allow setting the global default Propagators.
    /// </summary>
    public static class Propagators
    {
        private static readonly TextMapPropagator Noop = new NoopPropagator();

        /// <summary>
        /// Gets or sets the Default TextMapPropagator to be used.
        /// </summary>
        public static TextMapPropagator DefaultTextMapPropagator { get; set; } = Noop;

        internal static void Reset()
        {
            DefaultTextMapPropagator = Noop;
        }
    }
}

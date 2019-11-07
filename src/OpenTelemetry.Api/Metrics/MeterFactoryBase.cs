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
namespace OpenTelemetry.Metrics
{
    /// <summary>
    /// Creates Meters for an instrumentation library.
    /// </summary>
    public class MeterFactoryBase
    {
        private static NoOpMeter noOpMeter = new NoOpMeter();
        private static MeterFactoryBase defaultFactory = new MeterFactoryBase();

        /// <summary>
        /// Gets the dafult instance of a <see cref="MeterFactoryBase"/>.
        /// </summary>
        public static MeterFactoryBase Default
        {
            get => defaultFactory;
        }

        /// <summary>
        /// Returns an IMeter for a given name and version.
        /// </summary>
        /// <param name="name">Name of the instrumentation library.</param>
        /// <param name="version">Version of the instrumentation library (optional).</param>
        /// <returns>Meter with the given component name and version.</returns>
        public virtual Meter GetMeter(string name, string version = null)
        {
            return noOpMeter;
        }
    }
}

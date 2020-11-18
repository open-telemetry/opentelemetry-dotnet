// <copyright file="Source.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Controls the source name and version that will be analyzed.
    /// </summary>
    public struct Source
    {
        /// <summary>
        /// Source name.
        /// </summary>
        public readonly string Name;

        /// <summary>
        /// Min version of the assembly.
        /// </summary>
        public readonly Version MinVersion;

        /// <summary>
        /// Max version of the assembly.
        /// </summary>
        public readonly Version MaxVersion;

        /// <summary>
        /// Initializes a new instance of the <see cref="Source"/> struct.
        /// </summary>
        /// <param name="name">Name of the source.</param>
        /// <param name="minVersion">Min version.</param>
        /// <param name="maxVersion">Max version.</param>
        public Source(string name, Version minVersion = null, Version maxVersion = null)
        {
            this.Name = name;
            this.MinVersion = minVersion;
            this.MaxVersion = maxVersion;
        }
    }
}

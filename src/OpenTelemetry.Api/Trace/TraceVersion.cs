// <copyright file="TraceVersion.cs" company="OpenTelemetry Authors">
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
using System.Text.RegularExpressions;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Controls the source name and version that will be analyzed.
    /// </summary>
    public class TraceVersion
    {
        /// <summary>
        /// Name of the assembly.
        /// </summary>
        public readonly string Name;

        /// <summary>
        /// Min version of the assembly.
        /// </summary>
        public readonly string MinVersion;

        /// <summary>
        /// Max version of the assembly.
        /// </summary>
        public readonly string MaxVersion;

        /// <summary>
        /// Initializes a new instance of the <see cref="TraceVersion"/> class.
        /// </summary>
        /// <param name="name">Name of the source.</param>
        /// <param name="minVersion">Min version.</param>
        /// <param name="maxVersion">Max version.</param>
        public TraceVersion(string name, string minVersion = null, string maxVersion = null)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException($"{nameof(name)} contains null or whitespace string.");
            }

            if (!string.IsNullOrEmpty(minVersion))
            {
                if (!VersionHelper.ValidateVersion(minVersion))
                {
                    throw new ArgumentException($"{nameof(minVersion)} is invalid.");
                }

                this.MinVersion = minVersion;
            }

            if (!string.IsNullOrEmpty(maxVersion))
            {
                if (!VersionHelper.ValidateVersion(maxVersion))
                {
                    throw new ArgumentException($"{nameof(maxVersion)} is invalid.");
                }

                this.MaxVersion = maxVersion;
            }

            this.Name = name;
        }
    }
}

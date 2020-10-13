// <copyright file="StatusCode.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Canonical result code of span execution.
    /// </summary>
    public enum StatusCode
    {
        /// <summary>
        /// The default status.
        /// </summary>
        Unset = 0,

        /// <summary>
        /// The operation contains an error.
        /// </summary>
        Error = 1,

        /// <summary>
        /// The operation completed successfully.
        /// </summary>
        Ok = 2,
    }
}

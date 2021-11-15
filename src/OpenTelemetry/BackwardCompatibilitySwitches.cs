// <copyright file="BackwardCompatibilitySwitches.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry
{
    public static class BackwardCompatibilitySwitches
    {
        /// <summary>
        /// Gets or sets a value indicating whether activity status switch is enabled
        /// If true then activity Status and StatusDescription properties will be set
        /// using tags otel.status_code and otel.status_description respectively.
        /// </summary>
        public static bool ActivityStatusSwitch { get; set; } = true;
    }
}

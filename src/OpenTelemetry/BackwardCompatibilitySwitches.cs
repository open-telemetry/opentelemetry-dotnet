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

using System.Diagnostics;

namespace OpenTelemetry
{
    public static class BackwardCompatibilitySwitches
    {
        /// <summary>
        /// Gets or sets a value indicating whether or not activity status migration is enabled. Default value: true.
        /// </summary>
        /// <remarks>
        /// If true then <see cref="Activity.Status"/> and <see cref="Activity.StatusDescription"/> properties (added in .NET 6) will be set
        /// from `otel.status_code` and `otel.status_description` tag values respectively prior to export.
        /// </remarks>
        public static bool StatusTagMigrationEnabled { get; set; } = true;
    }
}

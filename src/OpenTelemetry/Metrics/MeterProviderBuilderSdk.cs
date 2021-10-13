// <copyright file="MeterProviderBuilderSdk.cs" company="OpenTelemetry Authors">
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

using System.Text.RegularExpressions;

namespace OpenTelemetry.Metrics
{
    internal class MeterProviderBuilderSdk : MeterProviderBuilderBase
    {
        private static readonly Regex InstrumentNameRegex = new Regex(
            @"^[a-zA-Z][-.\w]{0,62}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Returns whether the given instrument name is valid according to the specification.
        /// </summary>
        /// <remarks>See specification: <see href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/api.md#instrument"/>.</remarks>
        /// <param name="instrumentName">The instrument name.</param>
        /// <returns>Boolean indicating if the instrument is valid.</returns>
        internal static bool IsValidInstrumentName(string instrumentName)
        {
            if (string.IsNullOrWhiteSpace(instrumentName))
            {
                return false;
            }

            return InstrumentNameRegex.IsMatch(instrumentName);
        }

        /// <summary>
        /// Returns whether the given custom view name is valid according to the specification.
        /// </summary>
        /// <remarks>See specification: <see href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/api.md#instrument"/>.</remarks>
        /// <param name="customViewName">The view name.</param>
        /// <returns>Boolean indicating if the instrument is valid.</returns>
        internal static bool IsValidViewName(string customViewName)
        {
            // Only validate the view name in case it's not null. In case it's null, the view name will be the instrument name as per the spec.
            if (customViewName == null)
            {
                return true;
            }

            return InstrumentNameRegex.IsMatch(customViewName);
        }

        internal MeterProvider BuildSdk() => this.Build();
    }
}

// <copyright file="EnvironmentVariableHelper.cs" company="OpenTelemetry Authors">
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
using System.Globalization;
using System.Security;

namespace OpenTelemetry.Internal
{
    /// <summary>
    /// EnvironmentVariableHelper facilitates parsing environmetal variable values as defined by
    /// <a href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/sdk-environment-variables.md">
    /// the specification</a>
    /// </summary>
    internal static class EnvironmentVariableHelper
    {
        /// <summary>
        /// Reads an environmetal variable without any parsing.
        /// </summary>
        /// <returns>
        /// Returns <c>true</c> when if a non-empty value was read; otherwise, <c>false</c>.
        /// </returns>
        public static bool LoadString(string envVarKey, out string result)
        {
            result = null;

            try
            {
                result = Environment.GetEnvironmentVariable(envVarKey);
            }
            catch (SecurityException ex)
            {
                // The caller does not have the required permission to
                // retrieve the value of an environment variable from the current process.
                OpenTelemetrySdkEventSource.Log.MissingPermissionsToReadEnvironmentVariable(ex);
                return false;
            }

            return !string.IsNullOrEmpty(result);
        }

        /// <summary>
        /// Reads an environmetal variable and parses is as a non-negative decimal number.
        /// </summary>
        /// <returns>
        /// Returns <c>true</c> when if a non-empty value was read; otherwise, <c>false</c>.
        /// </returns>
        /// <exception cref="FormatException">
        /// Thrown when failed to parse the non-empty value.
        /// </exception>
        public static bool LoadNumeric(string envVarKey, out int result)
        {
            result = 0;

            if (!LoadString(envVarKey, out string value))
            {
                return false;
            }

            if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out result))
            {
                throw new FormatException($"{envVarKey} environment variable has an invalid value: '${value}'");
            }

            return true;
        }

        /// <summary>
        /// Reads an environmetal variable and parses is as a <see cref="Uri" />.
        /// </summary>
        /// <returns>
        /// Returns <c>true</c> when if a non-empty value was read; otherwise, <c>false</c>.
        /// </returns>
        /// <exception cref="FormatException">
        /// Thrown when failed to parse the non-empty value.
        /// </exception>
        public static bool LoadUri(string envVarKey, out Uri result)
        {
            result = null;

            if (!LoadString(envVarKey, out string value))
            {
                return false;
            }

            if (!Uri.TryCreate(value, UriKind.Absolute, out result))
            {
                throw new FormatException($"{envVarKey} environment variable has an invalid value: '${value}'");
            }

            return true;
        }
    }
}

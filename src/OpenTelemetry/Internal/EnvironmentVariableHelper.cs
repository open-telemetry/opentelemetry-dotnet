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
    internal static class EnvironmentVariableHelper
    {
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

        public static bool LoadNonNegativeInt32(string envVarKey, out int result)
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
    }
}

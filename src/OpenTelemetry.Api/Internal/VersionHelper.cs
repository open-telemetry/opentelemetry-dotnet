// <copyright file="VersionHelper.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Internal
{
    internal static class VersionHelper
    {
        private static readonly Regex RegexVersion = new Regex(@"^(\d+\.)?(\d+\.)?(\d+\.)?(\*|\d+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static bool Compare(string current, string minVersion, string maxVersion)
        {
            if (string.IsNullOrEmpty(minVersion) && string.IsNullOrEmpty(maxVersion))
            {
                return true;
            }

            if ((minVersion?.Equals("*") ?? false) || (maxVersion?.Equals("*") ?? false))
            {
                return true;
            }

            string[] minVersionParts = minVersion?.Split('.');
            int minVersionPartsLength = minVersionParts?.Length ?? 0;
            string[] maxVersionParts = maxVersion?.Split('.');
            int maxVersionPartsLength = maxVersionParts?.Length ?? 0;
            string[] currentParts = current.Split('.');

            for (int i = 0; i < currentParts.Length; i++)
            {
                int.TryParse(currentParts[i], out int version);

                if (i < minVersionPartsLength)
                {
                    if (minVersionParts[i].Equals("*"))
                    {
                        return true;
                    }
                    else
                    {
                        int min = int.Parse(minVersionParts[i]);
                        if (version < min)
                        {
                            return false;
                        }
                    }
                }

                if (i < maxVersionPartsLength)
                {
                    if (maxVersionParts[i].Equals("*"))
                    {
                        return true;
                    }
                    else
                    {
                        int max = int.Parse(maxVersionParts[i]);
                        if (max < version)
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        public static bool ValidateVersion(string version)
        {
            return RegexVersion.Match(version).Success;
        }
    }
}

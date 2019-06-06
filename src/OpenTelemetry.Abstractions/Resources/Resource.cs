// <copyright file="Resource.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Resources
{
    using System;
    using System.Collections.Generic;
    using System.Security;
    using System.Text.RegularExpressions;
    using OpenTelemetry.Tags;
    using OpenTelemetry.Utils;

    /// <summary>
    /// <see cref="Resource"/> represents a resource, which captures identifying information about the entities
    /// for which signals (stats or traces) are reported. It further provides a framework for detection of resource
    /// information from the environment and progressive population as signals propagate from the core
    /// instrumentation library to a backend's exporter.
    /// </summary>
    public abstract class Resource
    {
        /// <summary>
        /// Maximum length of the resource type name.
        /// </summary>
        public const int MaxResourceTypeNameLength = 255;

        /// <summary>
        /// Special resource type name that is assigned if nothing else is detected.
        /// </summary>
        public const string GlobalResourceType = "Global";

        /// <summary>
        /// OpenTelemetry Resource Type Environment Variable Name.
        /// </summary>
        public const string ResourceTypeEnvironmentVariable = "OC_RESOURCE_TYPE";

        /// <summary>
        /// OpenTelemetry Resource Labels Environment Variable Name.
        /// </summary>
        public const string ResourceLabelsEnvironmentVariable = "OC_RESOURCE_LABELS";

        /// <summary>
        /// Tag list splitter.
        /// </summary>
        private const char LabelListSplitter = ',';

        /// <summary>
        /// Key-value splitter.
        /// </summary>
        private const char LabelKeyValueSplitter = '=';

        /// <summary>
        /// Environment identification (for example, AKS/GKE/etc).
        /// </summary>
        private static readonly string EnvironmentType;

        private static readonly Tag[] EnvironmentToLabelMap;

        static Resource()
        {
            string openTelemetryResourceType = string.Empty;
            string openTelemetryEnvironmentTags = string.Empty;

            try
            {
                openTelemetryResourceType = Environment.GetEnvironmentVariable(ResourceTypeEnvironmentVariable);
            }
            catch (SecurityException ex)
            {
                openTelemetryResourceType = GlobalResourceType;
            }

            try
            {
                openTelemetryEnvironmentTags = Environment.GetEnvironmentVariable(ResourceLabelsEnvironmentVariable);
            }
            catch (SecurityException ex)
            {
                openTelemetryEnvironmentTags = string.Empty;
            }

            TryParseResourceType(openTelemetryResourceType, out EnvironmentType);
            EnvironmentToLabelMap = ParseResourceLabels(Environment.GetEnvironmentVariable(ResourceLabelsEnvironmentVariable));
        }

        /// <summary>
        /// Gets or sets the type identifier of the resource.
        /// </summary>
        public abstract string Type { get; protected set; }

        /// <summary>
        /// Gets the map of tags describing the resource.
        /// </summary>
        public abstract IEnumerable<Tag> Tags { get; }

        /// <summary>
        /// Creates a label/tag map from the OC_RESOURCE_LABELS environment variable.
        /// OC_RESOURCE_LABELS: A comma-separated list of labels describing the source in more detail,
        /// e.g. “key1=val1,key2=val2”. Domain names and paths are accepted as label keys.
        /// Values may be quoted or unquoted in general. If a value contains whitespaces, =, or " characters, it must
        /// always be quoted.
        /// </summary>
        /// <param name="rawEnvironmentTags">Environment tags as a raw, comma separated string</param>
        /// <returns>Environment Tags as a list.</returns>
        internal static Tag[] ParseResourceLabels(string rawEnvironmentTags)
        {
            if (rawEnvironmentTags == null)
            {
                return new Tag[0] { };
            }
            else
            {
                var labels = new List<Tag>();
                string[] rawLabels = rawEnvironmentTags.Split(LabelListSplitter);

                Regex regex = new Regex("^\"|\"$", RegexOptions.Compiled);

                foreach (var rawLabel in rawLabels)
                {
                    string[] keyValuePair = rawLabel.Split(LabelKeyValueSplitter);
                    if (keyValuePair.Length != 2)
                    {
                        continue;
                    }

                    string key = keyValuePair[0].Trim();
                    string value = Regex.Replace(keyValuePair[1].Trim(), "^\"|\"$", string.Empty);

                    if (!IsValidAndNotEmpty(key))
                    {
                        return new Tag[0] { };
                    }

                    if (!IsValid(value))
                    {
                        return new Tag[0] { };
                    }

                    labels.Add(new Tag(TagKey.Create(key), TagValue.Create(value)));
                }

                return labels.ToArray();
            }
        }

        internal static bool TryParseResourceType(string rawEnvironmentType, out string resourceType)
        {
            if (string.IsNullOrEmpty(rawEnvironmentType))
            {
                resourceType = GlobalResourceType;
                return false;
            }

            if (rawEnvironmentType.Length > MaxResourceTypeNameLength)
            {
                resourceType = GlobalResourceType;
                return false;
            }

            resourceType = rawEnvironmentType.Trim();
            return true;
        }

        /// <summary>
        /// Checks whether given string is a valid printable ASCII string with a length not exeeding
        /// <see cref="MaxResourceTypeNameLength"/> characters.
        /// </summary>
        /// <param name="name">The string.</param>
        /// <returns>Whether given string is valid.</returns>
        private static bool IsValid(string name)
        {
            return name.Length <= MaxResourceTypeNameLength && StringUtil.IsPrintableString(name);
        }

        /// <summary>
        /// Checks whether given string is a valid printable ASCII string with a length
        /// greater than 0 and not exceeding <see cref="MaxResourceTypeNameLength"/> characters.
        /// </summary>
        /// <param name="name">The string.</param>
        /// <returns>Whether given string is valid.</returns>
        private static bool IsValidAndNotEmpty(string name)
        {
            return !string.IsNullOrEmpty(name) && IsValid(name);
        }
    }
}

// <copyright file="Resource.cs" company="OpenCensus Authors">
// Copyright 2018, OpenCensus Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of theLicense at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

namespace OpenCensus.Resources
{
    using System;
    using System.Collections.Generic;
    using System.Security;
    using System.Text.RegularExpressions;
    using OpenCensus.Implementation;
    using OpenCensus.Tags;
    using OpenCensus.Utils;

    /// <summary>
    /// Represents a resource that captures identification information about the entities for which signals (stats or traces)
    /// are reported. It further provides a framework for detection of resource information from the environment and progressive
    /// population as signals propagate from the core instrumentation library to a backend's exporter.
    /// </summary>
    public abstract class Resource : IResource
    {
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

        private static readonly ITag[] EnvironmentToLabelMap;

        static Resource()
        {
            string openCensusResourceType;
            string openCensusEnvironmentTags;

            try
            {
                openCensusResourceType = Environment.GetEnvironmentVariable(Constants.ResourceTypeEnvironmentVariable);
            }
            catch (SecurityException ex)
            {
                openCensusResourceType = Constants.GlobalResourceType;

                Log.FailedReadingEnvironmentVariableWarning(Constants.ResourceTypeEnvironmentVariable, ex);
            }

            try
            {
                openCensusEnvironmentTags = Environment.GetEnvironmentVariable(Constants.ResourceLabelsEnvironmentVariable);
            }
            catch (SecurityException ex)
            {
                openCensusEnvironmentTags = string.Empty;

                Log.FailedReadingEnvironmentVariableWarning(Constants.ResourceLabelsEnvironmentVariable, ex);
            }

            TryParseResourceType(openCensusResourceType, out EnvironmentType);
            EnvironmentToLabelMap = ParseResourceLabels(Environment.GetEnvironmentVariable(Constants.ResourceLabelsEnvironmentVariable));
        }

        /// <summary>
        /// Gets or sets the identification of the resource.
        /// </summary>
        public abstract string Type { get; protected set; }

        /// <summary>
        /// Gets the map between the tag and its value.
        /// </summary>
        public abstract IEnumerable<ITag> Tags { get; }

        private static OpenCensusEventSource Log => OpenCensusEventSource.Log;

        /// <summary>
        /// Creates a label/tag map from the OC_RESOURCE_LABELS environment variable.
        /// OC_RESOURCE_LABELS: A comma-separated list of labels describing the source in more detail,
        /// e.g. “key1=val1,key2=val2”. Domain names and paths are accepted as label keys.
        /// Values may be quoted or unquoted in general. If a value contains whitespaces, =, or " characters, it must
        /// always be quoted.
        /// </summary>
        /// <param name="rawEnvironmentTags">Environment tags as a raw, comma separated string</param>
        /// <returns>Environment Tags as a list.</returns>
        internal static ITag[] ParseResourceLabels(string rawEnvironmentTags)
        {
            if (rawEnvironmentTags == null)
            {
                return new ITag[0] { };
            }
            else
            {
                var labels = new List<ITag>();
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
                        Log.InvalidCharactersInResourceElement("Label key");
                        return new ITag[0] { };
                    }

                    if (!IsValid(value))
                    {
                        Log.InvalidCharactersInResourceElement("Label key");
                        return new ITag[0] { };
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
                resourceType = Constants.GlobalResourceType;
                return false;
            }

            if (rawEnvironmentType.Length > Constants.MaxResourceTypeNameLength)
            {
                Log.InvalidCharactersInResourceElement(rawEnvironmentType);
                resourceType = Constants.GlobalResourceType;
                return false;
            }

            resourceType = rawEnvironmentType.Trim();
            return true;
        }

        /// <summary>
        /// Checks whether given string is a valid printable ASCII string with a length not exeeding
        /// <see cref="Constants.MaxResourceTypeNameLength"/> characters.
        /// </summary>
        /// <param name="name">The string.</param>
        /// <returns>Whether given string is valid.</returns>
        private static bool IsValid(string name)
        {
            return name.Length <= Constants.MaxResourceTypeNameLength && StringUtil.IsPrintableString(name);
        }

        /// <summary>
        /// Checks whether given string is a valid printable ASCII string with a length
        /// greater than 0 and not exceeding <see cref="Constants.MaxResourceTypeNameLength"/> characters.
        /// </summary>
        /// <param name="name">The string.</param>
        /// <returns>Whether given string is valid.</returns>
        private static bool IsValidAndNotEmpty(string name)
        {
            return !string.IsNullOrEmpty(name) && IsValid(name);
        }
    }
}

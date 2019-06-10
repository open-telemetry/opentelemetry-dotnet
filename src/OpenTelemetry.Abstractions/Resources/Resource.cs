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
    using OpenTelemetry.Utils;

    /// <summary>
    /// <see cref="Resource"/> represents a resource, which captures identifying information about the entities
    /// for which signals (stats or traces) are reported.
    /// </summary>
    public class Resource
    {
        /// <summary>
        /// Maximum length of the resource type name.
        /// </summary>
        private const int MaxResourceTypeNameLength = 255;
        private readonly Dictionary<string, string> labelCollection;

        internal Resource(IDictionary<string, string> labels)
        {
            this.labelCollection = (Dictionary<string, string>)ValidateLabels(labels);
        }

        /// <summary>
        /// Gets an empty Resource.
        /// </summary>
        public static Resource Empty { get; } = new Resource(new Dictionary<string, string>());

        /// <summary>
        /// Gets the collection of key-value pairs describing the resource.
        /// </summary>
        public IReadOnlyDictionary<string, string> Labels => this.labelCollection;

        /// <summary>
        /// Returns a new <see cref="Resource"/>.
        /// </summary>
        /// <param name="labels">An <see cref="IDictionary{string, string}"/> of labels that describe the resource.</param>
        /// <returns><see cref="Resource"/>.</returns>
        public static Resource Create(IDictionary<string, string> labels)
        {
            return new Resource(labels);
        }

        /// <summary>
        /// Returns a new, merged <see cref="Resource"/> by merging the current <see cref="Resource"/> with the
        /// <code>other</code> <see cref="Resource"/>. In case of a collision the current <see cref="Resource"/> takes precedence.
        /// </summary>
        /// <param name="other">The <see cref="Resource"/> that will be merged with <code>this</code>.</param>
        /// <returns><see cref="Resource"/>.</returns>
        public Resource Merge(Resource other)
        {
            if (other == null)
            {
                return this;
            }

            foreach (KeyValuePair<string, string> label in other.Labels)
            {
                if (this.labelCollection.ContainsKey(label.Key) == false)
                {
                    this.labelCollection.Add(label.Key, label.Value);
                }
            }

            return this;
        }

        private static IDictionary<string, string> ValidateLabels(IDictionary<string, string> labels)
        {
            if (labels == null)
            {
                throw new ArgumentNullException(nameof(labels));
            }

            foreach (KeyValuePair<string, string> label in labels)
            {
                if (!IsValidAndNotEmpty(label.Key))
                {
                    throw new ArgumentException($"Label key should be a string with a length greater than 0 and not exceed {MaxResourceTypeNameLength} characters.");
                }

                if (!IsValidAndNotEmpty(label.Value))
                {
                    throw new ArgumentException($"Label value should be a string with a length greater than 0 and not exceed {MaxResourceTypeNameLength} characters.");
                }
            }

            return labels;
        }

        private static bool IsValidAndNotEmpty(string name)
        {
            return !string.IsNullOrEmpty(name) && IsValid(name);
        }

        private static bool IsValid(string name)
        {
            return name.Length <= MaxResourceTypeNameLength && StringUtil.IsPrintableString(name);
        }
    }
}

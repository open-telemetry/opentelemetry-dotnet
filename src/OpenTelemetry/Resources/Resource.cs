// <copyright file="Resource.cs" company="OpenTelemetry Authors">
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

using System.Collections.Generic;
using System.Linq;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Resources
{
    /// <summary>
    /// <see cref="Resource"/> represents a resource, which captures identifying information about the entities
    /// for which telemetry is reported.
    /// Use <see cref="ResourceBuilder"/> to construct resource instances.
    /// </summary>
    public class Resource
    {
        // This implementation follows https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/resource/sdk.md

        /// <summary>
        /// Initializes a new instance of the <see cref="Resource"/> class.
        /// </summary>
        /// <param name="attributes">An <see cref="IEnumerable{T}"/> of attributes that describe the resource.</param>
        internal Resource(IEnumerable<KeyValuePair<string, object>> attributes)
        {
            if (attributes == null)
            {
                OpenTelemetrySdkEventSource.Log.InvalidArgument("Create resource", "attributes", "are null");
                this.Attributes = Enumerable.Empty<KeyValuePair<string, object>>();
                return;
            }

            // resource creation is expected to be done a few times during app startup i.e. not on the hot path, we can copy attributes.
            this.Attributes = attributes.Select(SanitizeAttribute).ToList();
        }

        /// <summary>
        /// Gets an empty Resource.
        /// </summary>
        public static Resource Empty { get; } = new Resource(Enumerable.Empty<KeyValuePair<string, object>>());

        /// <summary>
        /// Gets the collection of key-value pairs describing the resource.
        /// </summary>
        public IEnumerable<KeyValuePair<string, object>> Attributes { get; }

        /// <summary>
        /// Returns a new, merged <see cref="Resource"/> by merging the old <see cref="Resource"/> with the
        /// <c>other</c> <see cref="Resource"/>. In case of a collision the other <see cref="Resource"/> takes precedence.
        /// </summary>
        /// <param name="other">The <see cref="Resource"/> that will be merged with <c>this</c>.</param>
        /// <returns><see cref="Resource"/>.</returns>
        public Resource Merge(Resource other)
        {
            var newAttributes = new Dictionary<string, object>();

            if (other != null)
            {
                foreach (var attribute in other.Attributes)
                {
                    if (!newAttributes.TryGetValue(attribute.Key, out var value))
                    {
                        newAttributes[attribute.Key] = attribute.Value;
                    }
                }
            }

            foreach (var attribute in this.Attributes)
            {
                if (!newAttributes.TryGetValue(attribute.Key, out var value))
                {
                    newAttributes[attribute.Key] = attribute.Value;
                }
            }

            return new Resource(newAttributes);
        }

        private static KeyValuePair<string, object> SanitizeAttribute(KeyValuePair<string, object> attribute)
        {
            string sanitizedKey;
            if (attribute.Key == null)
            {
                OpenTelemetrySdkEventSource.Log.InvalidArgument("Create resource", "attribute key", "Attribute key should be non-null string.");
                sanitizedKey = string.Empty;
            }
            else
            {
                sanitizedKey = attribute.Key;
            }

            object sanitizedValue = SanitizeValue(attribute.Value, sanitizedKey);
            return new KeyValuePair<string, object>(sanitizedKey, sanitizedValue);
        }

        private static object SanitizeValue(object value, string keyName)
        {
            if (value != null)
            {
                if (value is string || value is bool || value is long || value is double)
                {
                    return value;
                }

                if (value is int || value is short)
                {
                    return System.Convert.ToInt64(value);
                }

                if (value is float)
                {
                    return System.Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);
                }

                throw new System.ArgumentException("Attribute value type is not an accepted primitive", keyName);
            }

            throw new System.ArgumentException("Attribute value is null", keyName);
        }
    }
}

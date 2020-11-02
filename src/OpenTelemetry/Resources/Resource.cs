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

using System;
using System.Collections.Generic;
using System.Linq;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Resources
{
    /// <summary>
    /// <see cref="Resource"/> represents a resource, which captures identifying information about the entities
    /// for which signals (stats or traces) are reported.
    /// </summary>
    public class Resource
    {
        public const string ServiceNameKey = "service.name";
        public const string ServiceNamespaceKey = "service.namespace";
        public const string ServiceInstanceIdKey = "service.instance.id";
        public const string ServiceVersionKey = "service.version";
        public const string LibraryNameKey = "name";
        public const string LibraryVersionKey = "version";
        public const string TelemetrySdkNameKey = "telemetry.sdk.name";
        public const string TelemetrySdkLanguageKey = "telemetry.sdk.language";
        public const string TelemetrySdkVersionKey = "telemetry.sdk.version";

        private const string OTelResourceEnvVarKey = "OTEL_RESOURCE_ATTRIBUTES";
        private const char AttributeListSplitter = ',';
        private const char AttributeKeyValueSplitter = '=';

        private static readonly Version Version = typeof(Resource).Assembly.GetName().Version;

        // this implementation follows https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/resource/sdk.md

        /// <summary>
        /// Initializes a new instance of the <see cref="Resource"/> class.
        /// </summary>
        /// <param name="attributes">An <see cref="IDictionary{String, Object}"/> of attributes that describe the resource.</param>
        public Resource(IEnumerable<KeyValuePair<string, object>> attributes)
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
        /// Gets the dafault Resource with telemetry sdk attributes set.
        /// </summary>
        public static Resource Default { get; } = new Resource(new List<KeyValuePair<string, object>>
            {
                new KeyValuePair<string, object>(TelemetrySdkNameKey, "opentelemetry"),
                new KeyValuePair<string, object>(TelemetrySdkLanguageKey, "dotnet"),
                new KeyValuePair<string, object>(TelemetrySdkVersionKey, Version.ToString()),
            });

        /// <summary>
        /// Gets the collection of key-value pairs describing the resource.
        /// </summary>
        public IEnumerable<KeyValuePair<string, object>> Attributes { get; }

        /// <summary>
        /// Creates a new <see cref="Resource"/> with user provided attributes, telemetry sdk attributes, and attributes from OTEL_RESOURCE_ATTRIBUTES environment variable.
        /// </summary>
        /// <param name="attributes">An <see cref="IDictionary{String, Object}"/> of attributes that describe the resource.</param>
        /// <returns><see cref="Resource"/>.</returns>
        public static Resource Create(IEnumerable<KeyValuePair<string, object>> attributes = null)
        {
            return new Resource(attributes).Merge(Default).Merge(GetOTelEnvVarResource());
        }

        /// <summary>
        /// Returns a new, merged <see cref="Resource"/> by merging the current <see cref="Resource"/> with the.
        /// <code>other</code> <see cref="Resource"/>. In case of a collision the current <see cref="Resource"/> takes precedence.
        /// </summary>
        /// <param name="other">The <see cref="Resource"/> that will be merged with. <code>this</code>.</param>
        /// <returns><see cref="Resource"/>.</returns>
        public Resource Merge(Resource other)
        {
            var newAttributes = new Dictionary<string, object>();

            foreach (var attribute in this.Attributes)
            {
                if (!newAttributes.TryGetValue(attribute.Key, out var value) || (value is string strValue && string.IsNullOrEmpty(strValue)))
                {
                    newAttributes[attribute.Key] = attribute.Value;
                }
            }

            if (other != null)
            {
                foreach (var attribute in other.Attributes)
                {
                    if (!newAttributes.TryGetValue(attribute.Key, out var value) || (value is string strValue && string.IsNullOrEmpty(strValue)))
                    {
                        newAttributes[attribute.Key] = attribute.Value;
                    }
                }
            }

            return new Resource(newAttributes);
        }

        private static KeyValuePair<string, object> SanitizeAttribute(KeyValuePair<string, object> attribute)
        {
            string sanitizedKey = null;
            object sanitizedValue = null;

            if (attribute.Key == null)
            {
                OpenTelemetrySdkEventSource.Log.InvalidArgument("Create resource", "attribute key", "Attribute key should be non-null string.");
                sanitizedKey = string.Empty;
            }
            else
            {
                sanitizedKey = attribute.Key;
            }

            if (!IsValidValue(attribute.Value))
            {
                OpenTelemetrySdkEventSource.Log.InvalidArgument("Create resource", "attribute value", "Attribute value should be a non-null string, long, bool or double.");
                sanitizedValue = string.Empty;
            }
            else
            {
                sanitizedValue = attribute.Value;
            }

            return new KeyValuePair<string, object>(sanitizedKey, sanitizedValue);
        }

        private static bool IsValidValue(object value)
        {
            if (value != null && (value is string || value is bool || value is long || value is double))
            {
                return true;
            }

            return false;
        }

        private static Resource GetOTelEnvVarResource()
        {
            var resource = Resource.Empty;

            string envResourceAttributeValue = Environment.GetEnvironmentVariable(OTelResourceEnvVarKey);
            if (!string.IsNullOrEmpty(envResourceAttributeValue))
            {
                var attributes = ParseResourceAttributes(envResourceAttributeValue);
                return new Resource(attributes);
            }

            return resource;
        }

        private static IEnumerable<KeyValuePair<string, object>> ParseResourceAttributes(string resourceAttributes)
        {
            var attributes = new List<KeyValuePair<string, object>>();

            string[] rawAttributes = resourceAttributes.Split(AttributeListSplitter);
            foreach (string rawKeyValuePair in rawAttributes)
            {
                string[] keyValuePair = rawKeyValuePair.Split(AttributeKeyValueSplitter);
                if (keyValuePair.Length != 2)
                {
                    continue;
                }

                attributes.Add(new KeyValuePair<string, object>(keyValuePair[0].Trim(), keyValuePair[1].Trim()));
            }

            return attributes;
        }
    }
}

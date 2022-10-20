// <copyright file="OtelEnvResourceDetector.cs" company="OpenTelemetry Authors">
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

#nullable enable

using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Resources
{
    internal sealed class OtelEnvResourceDetector : IResourceDetector
    {
        public const string EnvVarKey = "OTEL_RESOURCE_ATTRIBUTES";
        private const char AttributeListSplitter = ',';
        private const char AttributeKeyValueSplitter = '=';

        private readonly IConfiguration configuration;

        public OtelEnvResourceDetector(IConfiguration configuration)
        {
            this.configuration = configuration;
        }

        public Resource Detect()
        {
            var resource = Resource.Empty;

            if (this.configuration.TryGetStringValue(EnvVarKey, out string? envResourceAttributeValue))
            {
                var attributes = ParseResourceAttributes(envResourceAttributeValue!);
                resource = new Resource(attributes);
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

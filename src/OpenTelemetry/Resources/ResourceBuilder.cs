// <copyright file="ResourceBuilder.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Resources
{
    /// <summary>
    /// Contains methods for building <see cref="Resource"/> instances.
    /// </summary>
    public class ResourceBuilder
    {
        internal readonly List<Resource> Resources = new();

        static ResourceBuilder()
        {
            var defaultServiceName = "unknown_service";

            try
            {
                var processName = Process.GetCurrentProcess().ProcessName;
                if (!string.IsNullOrWhiteSpace(processName))
                {
                    defaultServiceName = $"{defaultServiceName}:{processName}";
                }
            }
            catch
            {
                // GetCurrentProcess can throw PlatformNotSupportedException
            }

            DefaultResource = new Resource(new Dictionary<string, object>
            {
                [ResourceSemanticConventions.AttributeServiceName] = defaultServiceName,
            });
        }

        private ResourceBuilder()
        {
        }

        private static Resource DefaultResource { get; }

        /// <summary>
        /// Creates a <see cref="ResourceBuilder"/> instance with Default
        /// service.name added. See <a
        /// href="https://github.com/open-telemetry/opentelemetry-specification/tree/main/specification/resource/semantic_conventions#semantic-attributes-with-sdk-provided-default-value">resource
        /// semantic conventions</a> for details.
        /// Additionally it adds resource attributes parsed from OTEL_RESOURCE_ATTRIBUTES, OTEL_SERVICE_NAME environment variables
        /// to a <see cref="ResourceBuilder"/> following the <a
        /// href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/resource/sdk.md#specifying-resource-information-via-an-environment-variable">Resource
        /// SDK</a>.
        /// </summary>
        /// <returns>Created <see cref="ResourceBuilder"/>.</returns>
        public static ResourceBuilder CreateDefault()
            => new ResourceBuilder().AddResource(DefaultResource).AddEnvironmentVariableDetector();

        /// <summary>
        /// Creates an empty <see cref="ResourceBuilder"/> instance.
        /// </summary>
        /// <returns>Created <see cref="ResourceBuilder"/>.</returns>
        public static ResourceBuilder CreateEmpty()
            => new();

        /// <summary>
        /// Clears the <see cref="Resource"/>s added to the builder.
        /// </summary>
        /// <returns><see cref="ResourceBuilder"/> for chaining.</returns>
        public ResourceBuilder Clear()
        {
            this.Resources.Clear();

            return this;
        }

        /// <summary>
        /// Build a merged <see cref="Resource"/> from all the <see cref="Resource"/>s added to the builder.
        /// </summary>
        /// <returns><see cref="Resource"/>.</returns>
        public Resource Build()
        {
            Resource finalResource = Resource.Empty;

            foreach (Resource resource in this.Resources)
            {
                finalResource = finalResource.Merge(resource);
            }

            return finalResource;
        }

        public ResourceBuilder AddDetector(IResourceDetector resourceDetector)
        {
            Guard.ThrowIfNull(resourceDetector);

            Resource resource = resourceDetector.Detect();

            if (resource != null)
            {
                this.Resources.Add(resource);
            }

            return this;
        }

        internal ResourceBuilder AddResource(Resource resource)
        {
            Guard.ThrowIfNull(resource);

            this.Resources.Add(resource);

            return this;
        }
    }
}

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

using System;
using System.Collections.Generic;

namespace OpenTelemetry.Resources
{
    /// <summary>
    /// Contains methods for building <see cref="Resource"/> instances.
    /// </summary>
    public class ResourceBuilder
    {
        private readonly List<Resource> resources = new List<Resource>();

        private ResourceBuilder()
        {
        }

        private static Resource DefaultResource { get; } = new Resource(new Dictionary<string, object>
        {
            [ResourceSemanticConventions.AttributeServiceName] = "unknown_service"
                + (string.IsNullOrWhiteSpace(System.Diagnostics.Process.GetCurrentProcess().ProcessName)
                ? string.Empty :
                ":" + System.Diagnostics.Process.GetCurrentProcess().ProcessName),
        });

        /// <summary>
        /// Creates a <see cref="ResourceBuilder"/> instance with Default
        /// service.name added. See <a
        /// href="https://github.com/open-telemetry/opentelemetry-specification/tree/main/specification/resource/semantic_conventions#semantic-attributes-with-sdk-provided-default-value">resource
        /// semantic conventions</a> for details.
        /// </summary>
        /// <returns>Created <see cref="ResourceBuilder"/>.</returns>
        public static ResourceBuilder CreateDefault()
            => new ResourceBuilder().AddResource(DefaultResource);

        /// <summary>
        /// Creates an empty <see cref="ResourceBuilder"/> instance.
        /// </summary>
        /// <returns>Created <see cref="ResourceBuilder"/>.</returns>
        public static ResourceBuilder CreateEmpty()
            => new ResourceBuilder();

        /// <summary>
        /// Clears the <see cref="Resource"/>s added to the builder.
        /// </summary>
        /// <returns><see cref="ResourceBuilder"/> for chaining.</returns>
        public ResourceBuilder Clear()
        {
            this.resources.Clear();

            return this;
        }

        /// <summary>
        /// Build a merged <see cref="Resource"/> from all the <see cref="Resource"/>s added to the builder.
        /// </summary>
        /// <returns><see cref="Resource"/>.</returns>
        public Resource Build()
        {
            Resource finalResource = Resource.Empty;

            foreach (Resource resource in this.resources)
            {
                finalResource = finalResource.Merge(resource);
            }

            return finalResource;
        }

        // Internal until spec is finalized.
        // https://github.com/open-telemetry/oteps/blob/master/text/0111-auto-resource-detection.md
        internal ResourceBuilder AddDetector(IResourceDetector resourceDetector)
        {
            if (resourceDetector == null)
            {
                throw new ArgumentNullException(nameof(resourceDetector));
            }

            Resource resource = resourceDetector.Detect();

            if (resource != null)
            {
                this.resources.Add(resource);
            }

            return this;
        }

        internal ResourceBuilder AddResource(Resource resource)
        {
            if (resource == null)
            {
                throw new ArgumentNullException(nameof(resource));
            }

            this.resources.Add(resource);

            return this;
        }
    }
}

// <copyright file="ResourceBuilderExtensions.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;

namespace OpenTelemetry.Resources
{
    /// <summary>
    /// Contains extension methods for building <see cref="Resource"/>s.
    /// </summary>
    public static class ResourceBuilderExtensions
    {
        private static readonly string FileVersion = FileVersionInfo.GetVersionInfo(typeof(Resource).Assembly.Location).FileVersion;

        private static Resource TelemetryResource { get; } = new Resource(new Dictionary<string, object>
        {
            [ResourceSemanticConventions.AttributeTelemetrySdkName] = "opentelemetry",
            [ResourceSemanticConventions.AttributeTelemetrySdkLanguage] = "dotnet",
            [ResourceSemanticConventions.AttributeTelemetrySdkVersion] = FileVersion,
        });

        /// <summary>
        /// Adds service information to a <see cref="ResourceBuilder"/>
        /// following <a
        /// href="https://github.com/open-telemetry/opentelemetry-specification/tree/main/specification/resource/semantic_conventions#service">semantic
        /// conventions</a>.
        /// </summary>
        /// <param name="resourceBuilder"><see cref="ResourceBuilder"/>.</param>
        /// <param name="serviceName">Name of the service.</param>
        /// <param name="serviceNamespace">Optional namespace of the service.</param>
        /// <param name="serviceVersion">Optional version of the service.</param>
        /// <param name="autoGenerateServiceInstanceId">Specify <see langword="true"/> to automatically generate a <see cref="Guid"/> for <paramref name="serviceInstanceId"/> if not supplied.</param>
        /// <param name="serviceInstanceId">Optional unique identifier of the service instance.</param>
        /// <returns>Returns <see cref="ResourceBuilder"/> for chaining.</returns>
        public static ResourceBuilder AddService(
            this ResourceBuilder resourceBuilder,
            string serviceName,
            string serviceNamespace = null,
            string serviceVersion = null,
            bool autoGenerateServiceInstanceId = true,
            string serviceInstanceId = null)
        {
            Dictionary<string, object> resourceAttributes = new Dictionary<string, object>();

            if (string.IsNullOrEmpty(serviceName))
            {
                throw new ArgumentNullException(nameof(serviceName));
            }

            resourceAttributes.Add(ResourceSemanticConventions.AttributeServiceName, serviceName);

            if (!string.IsNullOrEmpty(serviceNamespace))
            {
                resourceAttributes.Add(ResourceSemanticConventions.AttributeServiceNamespace, serviceNamespace);
            }

            if (!string.IsNullOrEmpty(serviceVersion))
            {
                resourceAttributes.Add(ResourceSemanticConventions.AttributeServiceVersion, serviceVersion);
            }

            if (serviceInstanceId == null && autoGenerateServiceInstanceId)
            {
                serviceInstanceId = Guid.NewGuid().ToString();
            }

            if (serviceInstanceId != null)
            {
                resourceAttributes.Add(ResourceSemanticConventions.AttributeServiceInstance, serviceInstanceId);
            }

            return resourceBuilder.AddResource(new Resource(resourceAttributes));
        }

        /// <summary>
        /// Adds service information to a <see cref="ResourceBuilder"/>
        /// following <a
        /// href="https://github.com/open-telemetry/opentelemetry-specification/tree/main/specification/resource/semantic_conventions#telemetry-sdk">semantic
        /// conventions</a>.
        /// </summary>
        /// <param name="resourceBuilder"><see cref="ResourceBuilder"/>.</param>
        /// <returns>Returns <see cref="ResourceBuilder"/> for chaining.</returns>
        public static ResourceBuilder AddTelemetrySdk(this ResourceBuilder resourceBuilder)
        {
            return resourceBuilder.AddResource(TelemetryResource);
        }

        /// <summary>
        /// Adds attributes to a <see cref="ResourceBuilder"/>.
        /// </summary>
        /// <param name="resourceBuilder"><see cref="ResourceBuilder"/>.</param>
        /// <param name="attributes">An <see cref="IEnumerable{T}"/> of attributes that describe the resource.</param>
        /// <returns>Returns <see cref="ResourceBuilder"/> for chaining.</returns>
        public static ResourceBuilder AddAttributes(this ResourceBuilder resourceBuilder, IEnumerable<KeyValuePair<string, object>> attributes)
        {
            return resourceBuilder.AddResource(new Resource(attributes));
        }

        /// <summary>
        /// Adds resource attributes parsed from an environment variable to a
        /// <see cref="ResourceBuilder"/> following the <a
        /// href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/resource/sdk.md#specifying-resource-information-via-an-environment-variable">Resource
        /// SDK</a>.
        /// </summary>
        /// <param name="resourceBuilder"><see cref="ResourceBuilder"/>.</param>
        /// <returns>Returns <see cref="ResourceBuilder"/> for chaining.</returns>
        public static ResourceBuilder AddEnvironmentVariableDetector(this ResourceBuilder resourceBuilder)
        {
            return resourceBuilder.AddDetector(new OtelEnvResourceDetector());
        }
    }
}

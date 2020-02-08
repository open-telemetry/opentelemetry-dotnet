// <copyright file="Resources.cs" company="OpenTelemetry Authors">
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

using System;
using System.Collections.Generic;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Resources
{
    public static class Resources
    {
        /// <summary>
        /// Creates a new <see cref="Resource"/> from service information following standard convention
        /// https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/data-resource-semantic-conventions.md#service.
        /// </summary>
        /// <param name="serviceName">Name of the service.</param>
        /// <param name="serviceInstanceId">Unique identifier of the service instance.</param>
        /// <param name="serviceNamespace">Optional namespace of the service.</param>
        /// <param name="serviceVersion">Optional version of the service.</param>
        /// <param name="attributes">Optional additional service attributes.</param>
        public static Resource CreateServiceResource(
            string serviceName,
            string serviceInstanceId = null,
            string serviceNamespace = null,
            string serviceVersion = null,
            IDictionary<string, object> attributes = null)
        {
            if (serviceName == null)
            {
                OpenTelemetrySdkEventSource.Log.InvalidArgument("Create service resource", "serviceName", "is null");
                return Resource.Empty;
            }

            var resourceAttributes = attributes != null
                ? new Dictionary<string, object>(attributes)
                : new Dictionary<string, object>();

            resourceAttributes[Resource.ServiceNameKey] = serviceName;

            if (serviceInstanceId == null)
            {
                serviceInstanceId = Guid.NewGuid().ToString();
            }

            resourceAttributes[Resource.ServiceInstanceIdKey] = serviceInstanceId;

            if (serviceNamespace != null)
            {
                resourceAttributes[Resource.ServiceNamespaceKey] = serviceNamespace;
            }

            if (serviceVersion != null)
            {
                resourceAttributes[Resource.ServiceVersionKey] = serviceVersion;
            }

            return new Resource(resourceAttributes);
        }
    }
}

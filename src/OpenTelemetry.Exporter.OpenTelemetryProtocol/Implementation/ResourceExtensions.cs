// <copyright file="ResourceExtensions.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Resources;
using OtlpCommon = Opentelemetry.Proto.Common.V1;
using OtlpResource = Opentelemetry.Proto.Resource.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation
{
    internal static class ResourceExtensions
    {
        public static OtlpResource.Resource ToOtlpResource(this Resource resource)
        {
            var processResource = new OtlpResource.Resource();

            foreach (KeyValuePair<string, object> attribute in resource.Attributes)
            {
                var otlpAttribute = attribute.ToOtlpAttribute();
                if (otlpAttribute != null)
                {
                    processResource.Attributes.Add(otlpAttribute);
                }
            }

            if (!processResource.Attributes.Any(kvp => kvp.Key == ResourceSemanticConventions.AttributeServiceName))
            {
                var serviceName = (string)ResourceBuilder.CreateDefault().Build().Attributes.FirstOrDefault(
                    kvp => kvp.Key == ResourceSemanticConventions.AttributeServiceName).Value;
                processResource.Attributes.Add(new OtlpCommon.KeyValue
                {
                    Key = ResourceSemanticConventions.AttributeServiceName,
                    Value = new OtlpCommon.AnyValue { StringValue = serviceName },
                });
            }

            return processResource;
        }
    }
}

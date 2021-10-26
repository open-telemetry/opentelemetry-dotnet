// <copyright file="OtelServiceNameEnvVarDetector.cs" company="OpenTelemetry Authors">
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
using System.Security;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Resources
{
    internal class OtelServiceNameEnvVarDetector : IResourceDetector
    {
        public const string EnvVarKey = "OTEL_SERVICE_NAME";

        public Resource Detect()
        {
            var resource = Resource.Empty;

            try
            {
                if (EnvironmentVariableHelper.LoadString(EnvVarKey, out string envResourceAttributeValue))
                {
                    resource = new Resource(new Dictionary<string, object>
                    {
                        [ResourceSemanticConventions.AttributeServiceName] = envResourceAttributeValue,
                    });
                }
            }
            catch (SecurityException ex)
            {
                OpenTelemetrySdkEventSource.Log.ResourceDetectorFailed(nameof(OtelServiceNameEnvVarDetector), ex.Message);
            }

            return resource;
        }
    }
}

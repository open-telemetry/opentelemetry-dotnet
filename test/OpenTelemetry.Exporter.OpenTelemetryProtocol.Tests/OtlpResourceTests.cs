// <copyright file="OtlpResourceTests.cs" company="OpenTelemetry Authors">
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

using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Resources;
using Xunit;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests
{
    public class OtlpResourceTests
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ToOtlpResourceTest(bool includeServiceNameInResource)
        {
            // Targeted test to cover OTel Resource to OTLP Resource
            // conversion, independent of signals.
            var resourceBuilder = ResourceBuilder.CreateEmpty();
            if (includeServiceNameInResource)
            {
                resourceBuilder.AddService("service-name", "ns1");
            }

            var resource = resourceBuilder.Build();
            var otlpResource = resource.ToOtlpResource();
            if (includeServiceNameInResource)
            {
                Assert.Contains(otlpResource.Attributes, (kvp) => kvp.Key == ResourceSemanticConventions.AttributeServiceName && kvp.Value.StringValue == "service-name");
                Assert.Contains(otlpResource.Attributes, (kvp) => kvp.Key == ResourceSemanticConventions.AttributeServiceNamespace && kvp.Value.StringValue == "ns1");
            }
            else
            {
                Assert.Contains(otlpResource.Attributes, (kvp) => kvp.Key == ResourceSemanticConventions.AttributeServiceName && kvp.Value.ToString().Contains("unknown_service:"));
            }
        }
    }
}

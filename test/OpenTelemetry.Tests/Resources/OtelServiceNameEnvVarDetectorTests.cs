// <copyright file="OtelServiceNameEnvVarDetectorTests.cs" company="OpenTelemetry Authors">
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

using Microsoft.Extensions.Configuration;
using Xunit;

namespace OpenTelemetry.Resources.Tests
{
    public class OtelServiceNameEnvVarDetectorTests : IDisposable
    {
        public OtelServiceNameEnvVarDetectorTests()
        {
            Environment.SetEnvironmentVariable(OtelServiceNameEnvVarDetector.EnvVarKey, null);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(OtelServiceNameEnvVarDetector.EnvVarKey, null);
            GC.SuppressFinalize(this);
        }

        [Fact]
        public void OtelServiceNameEnvVar_EnvVarKey()
        {
            Assert.Equal("OTEL_SERVICE_NAME", OtelServiceNameEnvVarDetector.EnvVarKey);
        }

        [Fact]
        public void OtelServiceNameEnvVar_Null()
        {
            // Act
            var resource = new OtelServiceNameEnvVarDetector(
                new ConfigurationBuilder().AddEnvironmentVariables().Build())
                .Detect();

            // Assert
            Assert.Equal(Resource.Empty, resource);
        }

        [Fact]
        public void OtelServiceNameEnvVar_WithValue()
        {
            // Arrange
            var envVarValue = "my-service";
            Environment.SetEnvironmentVariable(OtelServiceNameEnvVarDetector.EnvVarKey, envVarValue);

            // Act
            var resource = new OtelServiceNameEnvVarDetector(
                new ConfigurationBuilder().AddEnvironmentVariables().Build())
                .Detect();

            // Assert
            Assert.NotEqual(Resource.Empty, resource);
            Assert.Contains(new KeyValuePair<string, object>(ResourceSemanticConventions.AttributeServiceName, envVarValue), resource.Attributes);
        }

        [Fact]
        public void OtelServiceNameEnvVar_UsingIConfiguration()
        {
            var values = new Dictionary<string, string>()
            {
                [OtelServiceNameEnvVarDetector.EnvVarKey] = "my-service",
            };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(values)
                .Build();

            var resource = new OtelServiceNameEnvVarDetector(configuration).Detect();

            Assert.NotEqual(Resource.Empty, resource);
            Assert.Contains(new KeyValuePair<string, object>(ResourceSemanticConventions.AttributeServiceName, "my-service"), resource.Attributes);
        }
    }
}

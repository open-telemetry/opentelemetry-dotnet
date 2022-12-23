// <copyright file="OtelEnvResourceDetectorTest.cs" company="OpenTelemetry Authors">
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
    public class OtelEnvResourceDetectorTest : IDisposable
    {
        public OtelEnvResourceDetectorTest()
        {
            Environment.SetEnvironmentVariable(OtelEnvResourceDetector.EnvVarKey, null);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(OtelEnvResourceDetector.EnvVarKey, null);
            GC.SuppressFinalize(this);
        }

        [Fact]
        public void OtelEnvResource_EnvVarKey()
        {
            Assert.Equal("OTEL_RESOURCE_ATTRIBUTES", OtelEnvResourceDetector.EnvVarKey);
        }

        [Fact]
        public void OtelEnvResource_NullEnvVar()
        {
            // Arrange
            var resource = new OtelEnvResourceDetector(
                new ConfigurationBuilder().AddEnvironmentVariables().Build())
                .Detect();

            // Assert
            Assert.Equal(Resource.Empty, resource);
        }

        [Fact]
        public void OtelEnvResource_WithEnvVar_1()
        {
            // Arrange
            var envVarValue = "Key1=Val1,Key2=Val2";
            Environment.SetEnvironmentVariable(OtelEnvResourceDetector.EnvVarKey, envVarValue);
            var resource = new OtelEnvResourceDetector(
                new ConfigurationBuilder().AddEnvironmentVariables().Build())
                .Detect();

            // Assert
            Assert.NotEqual(Resource.Empty, resource);
            Assert.Contains(new KeyValuePair<string, object>("Key1", "Val1"), resource.Attributes);
        }

        [Fact]
        public void OtelEnvResource_WithEnvVar_2()
        {
            // Arrange
            var envVarValue = "Key1,Key2=Val2";
            Environment.SetEnvironmentVariable(OtelEnvResourceDetector.EnvVarKey, envVarValue);
            var resource = new OtelEnvResourceDetector(
                new ConfigurationBuilder().AddEnvironmentVariables().Build())
                .Detect();

            // Assert
            Assert.NotEqual(Resource.Empty, resource);
            Assert.Single(resource.Attributes);
            Assert.Contains(new KeyValuePair<string, object>("Key2", "Val2"), resource.Attributes);
        }

        [Fact]
        public void OtelEnvResource_UsingIConfiguration()
        {
            var values = new Dictionary<string, string>()
            {
                [OtelEnvResourceDetector.EnvVarKey] = "Key1=Val1,Key2=Val2",
            };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(values)
                .Build();

            var resource = new OtelEnvResourceDetector(configuration).Detect();

            Assert.NotEqual(Resource.Empty, resource);
            Assert.Contains(new KeyValuePair<string, object>("Key1", "Val1"), resource.Attributes);
            Assert.Contains(new KeyValuePair<string, object>("Key2", "Val2"), resource.Attributes);
        }
    }
}

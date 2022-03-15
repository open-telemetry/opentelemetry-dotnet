// <copyright file="LogRecordTest.cs" company="OpenTelemetry Authors">
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
#if !NET461
using System;

using OpenTelemetry.Resources;

using Xunit;

namespace OpenTelemetry.Logs.Tests
{
    public sealed class LoggingProviderTests
    {
        [Fact]
        public void VerifyDefaultBehavior()
        {
            var options = new OpenTelemetryLoggerOptions();
            var provider = new OpenTelemetryLoggerProvider(options);

            Assert.Contains(provider.Resource.Attributes, (kvp) => kvp.Key == "service.name" && kvp.Value.ToString() == "unknown_service:testhost");
        }

        [Fact]
        public void VerifyResourceBuilderAddService()
        {
            var options = new OpenTelemetryLoggerOptions
            {
                ResourceBuilder = ResourceBuilder.CreateDefault().AddService(serviceName: "MyService", serviceVersion: "1.2.3"),
            };
            var provider = new OpenTelemetryLoggerProvider(options);

            Assert.Contains(provider.Resource.Attributes, (kvp) => kvp.Key == "service.name" && kvp.Value.ToString() == "MyService");
            Assert.Contains(provider.Resource.Attributes, (kvp) => kvp.Key == "service.version" && kvp.Value.ToString() == "1.2.3");
        }

        [Fact]
        public void VerifyResourceBuilder_WithServiceNameEnVar()
        {
            try
            {
                Environment.SetEnvironmentVariable(OtelServiceNameEnvVarDetector.EnvVarKey, "MyService");

                var options = new OpenTelemetryLoggerOptions();
                var provider = new OpenTelemetryLoggerProvider(options);

                Assert.Contains(provider.Resource.Attributes, (kvp) => kvp.Key == "service.name" && kvp.Value.ToString() == "MyService");
            }
            finally
            {
                Environment.SetEnvironmentVariable(OtelServiceNameEnvVarDetector.EnvVarKey, null);
            }
        }

        [Fact]
        public void VerifyResourceBuilder_WithAttributesEnVar()
        {
            try
            {
                Environment.SetEnvironmentVariable(OtelEnvResourceDetector.EnvVarKey, "Key1=Val1,Key2=Val2");

                var options = new OpenTelemetryLoggerOptions();
                var provider = new OpenTelemetryLoggerProvider(options);

                Assert.Contains(provider.Resource.Attributes, (kvp) => kvp.Key == "Key1" && kvp.Value.ToString() == "Val1");
                Assert.Contains(provider.Resource.Attributes, (kvp) => kvp.Key == "Key2" && kvp.Value.ToString() == "Val2");
            }
            finally
            {
                Environment.SetEnvironmentVariable(OtelEnvResourceDetector.EnvVarKey, null);
            }
        }
    }
}
#endif

// <copyright file="ExporterClientValidationTests.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
using Xunit;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests
{
    public class ExporterClientValidationTests
    {
#if NETCOREAPP3_1
        [Fact]
        public void ExporterClientValidation_FlagIsEnabledForHttpEndpoint()
        {
            var initialFlagStatus = this.DetermineInitialFlagStatus();

            try
            {
                var options = new OtlpExporterOptions
                {
                    Endpoint = new Uri("http://localhost:4173"),
                };

                AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
                ExporterClientValidation.EnsureUnencryptedSupportIsEnabled(options);
            }
            finally
            {
                AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", initialFlagStatus);
            }
        }

        [Fact]
        public void ExporterClientValidation_FlagIsNotEnabledForHttpEndpoint()
        {
            var initialFlagStatus = this.DetermineInitialFlagStatus();

            try
            {
                var options = new OtlpExporterOptions
                {
                    Endpoint = new Uri("http://localhost:4173"),
                };

                AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", false);
                Assert.Throws<InvalidOperationException>(() => ExporterClientValidation.EnsureUnencryptedSupportIsEnabled(options));
            }
            finally
            {
                AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", initialFlagStatus);
            }
        }

        [Fact]
        public void ExporterClientValidation_FlagIsNotEnabledForHttpsEndpoint()
        {
            var options = new OtlpExporterOptions
            {
                Endpoint = new Uri("https://localhost:4173"),
            };

            ExporterClientValidation.EnsureUnencryptedSupportIsEnabled(options);
        }

        private bool DetermineInitialFlagStatus()
        {
            if (AppContext.TryGetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", out var flag))
            {
                return flag;
            }

            return false;
        }
#endif
    }
}

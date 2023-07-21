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

using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
using Xunit;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

public class ExporterClientValidationTests : Http2UnencryptedSupportTests
{
    private const string HttpEndpoint = "http://localhost:4173";
    private const string HttpsEndpoint = "https://localhost:4173";

    [Fact]
    public void ExporterClientValidation_FlagIsEnabledForHttpEndpoint()
    {
        var options = new OtlpExporterOptions
        {
            Endpoint = new Uri(HttpEndpoint),
        };

        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

        var exception = Record.Exception(() => ExporterClientValidation.EnsureUnencryptedSupportIsEnabled(options));
        Assert.Null(exception);
    }

    [Fact]
    public void ExporterClientValidation_FlagIsNotEnabledForHttpEndpoint()
    {
        var options = new OtlpExporterOptions
        {
            Endpoint = new Uri(HttpEndpoint),
        };

        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", false);

        var exception = Record.Exception(() => ExporterClientValidation.EnsureUnencryptedSupportIsEnabled(options));

        if (Environment.Version.Major == 3)
        {
            Assert.NotNull(exception);
            Assert.IsType<InvalidOperationException>(exception);
        }
        else
        {
            Assert.Null(exception);
        }
    }

    [Fact]
    public void ExporterClientValidation_FlagIsNotEnabledForHttpsEndpoint()
    {
        var options = new OtlpExporterOptions
        {
            Endpoint = new Uri(HttpsEndpoint),
        };

        var exception = Record.Exception(() => ExporterClientValidation.EnsureUnencryptedSupportIsEnabled(options));
        Assert.Null(exception);
    }
}

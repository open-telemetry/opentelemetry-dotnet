// <copyright file="OtlpGrpcTraceExportClientTests.cs" company="OpenTelemetry Authors">
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

#if NET6_0_OR_GREATER
using System.Reflection;
using Grpc.Net.Client;
using Moq;
#endif
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
using Xunit;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

public class OtlpGrpcTraceExportClientTests
{
    [Fact]
    public void NewOtlpGrpcTraceExportClient_OtlpExporterOptions_ExporterHasCorrectProperties()
    {
        var header1 = new
        {
            Name = "hdr1",
            Value = "val1"
        };
        var header2 = new
        {
            Name = "hdr2",
            Value = "val2"
        };

        var options = new OtlpExporterOptions
        {
            Headers = $"{header1.Name}={header1.Value}, {header2.Name} = {header2.Value}",
        };

        var client = new OtlpGrpcTraceExportClient(options);

        Assert.NotNull(client.Channel);

        Assert.Equal(2 + OtlpExporterOptions.StandardHeaders.Length, client.Headers.Count);
        Assert.Contains(client.Headers, kvp => kvp.Key == header1.Name && kvp.Value == header1.Value);
        Assert.Contains(client.Headers, kvp => kvp.Key == header2.Name && kvp.Value == header2.Value);

        for (int i = 0; i < OtlpExporterOptions.StandardHeaders.Length; i++)
        {
            Assert.Contains(client.Headers, entry => entry.Key.Equals(OtlpExporterOptions.StandardHeaders[i].Key, StringComparison.OrdinalIgnoreCase) && entry.Value == OtlpExporterOptions.StandardHeaders[i].Value);
        }
    }

#if NET6_0_OR_GREATER
    [Fact]
    public void NewOtlpGrpcTraceExportClient_UseCustomHttpClient()
    {
        var httpHandler = new Mock<HttpMessageHandler>();

        var options = new OtlpExporterOptions
        {
            HttpHandlerFactory = () => httpHandler.Object
        };

        var client = new OtlpGrpcTraceExportClient(options);

        var httpInvoker = Assert.IsAssignableFrom<HttpMessageInvoker>(typeof(GrpcChannel).GetProperty("HttpInvoker", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(client.Channel));

        var httpMessageHandler = Assert.IsAssignableFrom<HttpMessageHandler>(typeof(HttpMessageInvoker).GetField("_handler", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(httpInvoker));

        while (httpMessageHandler is DelegatingHandler delegatingHandler)
        {
            httpMessageHandler = delegatingHandler.InnerHandler;
        }

        Assert.Equal(httpHandler.Object, httpMessageHandler);
    }
#endif
}

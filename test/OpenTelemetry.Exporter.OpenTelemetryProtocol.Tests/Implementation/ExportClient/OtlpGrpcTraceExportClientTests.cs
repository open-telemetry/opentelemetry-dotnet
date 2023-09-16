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

using System.Reflection;
using Grpc.Core;
using Moq;
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

    [Fact]
    public void NewOtlpGrpcTraceExportClient_CallInvokerFactory()
    {
        var mock = new Mock<CallInvoker>();

        var options = new OtlpExporterOptions
        {
            CallInvokerFactory = endpoint => mock.Object,
        };

        var client = new OtlpGrpcTraceExportClient(options);

        Assert.Null(client.Channel);

        var clientBase = Assert.IsAssignableFrom<ClientBase>(client.GetType().GetField("traceClient", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(client));

        var callInvoker = Assert.IsAssignableFrom<CallInvoker>(typeof(ClientBase).GetField("callInvoker", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(clientBase));

        Assert.Same(mock.Object, callInvoker.GetType().GetField("invoker", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(callInvoker));
    }
}

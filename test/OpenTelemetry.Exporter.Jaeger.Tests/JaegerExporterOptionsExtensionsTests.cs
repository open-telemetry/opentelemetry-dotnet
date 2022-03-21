// <copyright file="JaegerExporterOptionsExtensionsTests.cs" company="OpenTelemetry Authors">
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

using Xunit;

namespace OpenTelemetry.Exporter.Jaeger.Tests;

public class JaegerExporterOptionsExtensionsTests
{
    [Theory]
    [InlineData("udp/thrift.compact", JaegerExportProtocol.UdpCompactThrift)]
    [InlineData("http/thrift.binary", JaegerExportProtocol.HttpBinaryThrift)]
    [InlineData("unsupported", null)]
    public void ToJaegerExportProtocol_Protocol_MapsToCorrectValue(string protocol, JaegerExportProtocol? expectedExportProtocol)
    {
        var exportProtocol = protocol.ToJaegerExportProtocol();

        Assert.Equal(expectedExportProtocol, exportProtocol);
    }
}

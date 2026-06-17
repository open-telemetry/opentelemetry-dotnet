// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#pragma warning disable OTEL1006

using YamlDotNet.RepresentationModel;

namespace OpenTelemetry.Configuration.Declarative.Tests;

public sealed class YamlNodeReaderTests
{
    [Fact]
    public void GetScalarString_NullScalarValue_ReturnsNull()
    {
        var scalar = new YamlScalarNode((string?)null);
        Assert.Null(scalar.GetScalarString());
    }
}

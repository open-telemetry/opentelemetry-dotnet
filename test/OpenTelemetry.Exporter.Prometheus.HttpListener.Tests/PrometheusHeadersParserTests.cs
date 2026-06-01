// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace OpenTelemetry.Exporter.Prometheus.Tests;

public class PrometheusHeadersParserTests
{
    [Theory]
    [MemberData(nameof(PrometheusAcceptHeaders.Valid), MemberType = typeof(PrometheusAcceptHeaders))]
    public void Negotiate_Parses_Valid_Header(
        string accept,
        string mediaType,
        bool isOpenMetrics,
        string version,
        string? escaping)
    {
        var actual = PrometheusHeadersParser.Negotiate(accept);

        Assert.Equal(mediaType, actual.MediaType);
        Assert.Equal(isOpenMetrics, actual.IsOpenMetrics);
        Assert.Equal(Version.Parse(version), actual.Version);
        Assert.Equal(escaping, actual.Escaping);
    }

    [Theory]
    [MemberData(nameof(PrometheusAcceptHeaders.Invalid), MemberType = typeof(PrometheusAcceptHeaders))]
    [InlineData("application/openmetrics-text; version=1.0.0; q=1.1")]
    public void Negotiate_Uses_Fallback_For_Invalid_Header(string header)
    {
        var actual = PrometheusHeadersParser.Negotiate(header);

        Assert.Equal(PrometheusProtocol.Fallback, actual);
    }
}

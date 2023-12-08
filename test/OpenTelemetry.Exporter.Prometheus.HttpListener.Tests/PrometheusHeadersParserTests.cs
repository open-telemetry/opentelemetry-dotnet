// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace OpenTelemetry.Exporter.Prometheus.Tests;

public class PrometheusHeadersParserTests
{
    [Theory]
    [InlineData("application/openmetrics-text")]
    [InlineData("application/openmetrics-text; version=1.0.0")]
    [InlineData("application/openmetrics-text; version=1.0.0; charset=utf-8")]
    [InlineData("text/plain,application/openmetrics-text; version=1.0.0; charset=utf-8")]
    [InlineData("text/plain; charset=utf-8,application/openmetrics-text; version=1.0.0; charset=utf-8")]
    [InlineData("text/plain, */*;q=0.8,application/openmetrics-text; version=1.0.0; charset=utf-8")]
    public void ParseHeader_AcceptHeaders_OpenMetricsValid(string header)
    {
        var result = PrometheusHeadersParser.AcceptsOpenMetrics(header);

        Assert.True(result);
    }

    [Theory]
    [InlineData("text/plain")]
    [InlineData("text/plain; charset=utf-8")]
    [InlineData("text/plain; charset=utf-8; version=0.0.4")]
    [InlineData("*/*;q=0.8,text/plain; charset=utf-8; version=0.0.4")]
    public void ParseHeader_AcceptHeaders_OtherHeadersInvalid(string header)
    {
        var result = PrometheusHeadersParser.AcceptsOpenMetrics(header);

        Assert.False(result);
    }
}

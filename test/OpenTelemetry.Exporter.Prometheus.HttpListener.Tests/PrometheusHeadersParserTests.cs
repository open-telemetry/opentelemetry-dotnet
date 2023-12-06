// <copyright file="PrometheusHeadersParserTests.cs" company="OpenTelemetry Authors">
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

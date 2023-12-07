// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using System.Text.Json;
using System.Text.Json.Serialization;
using RouteTests.TestApplication;

namespace RouteTests;

public class RoutingTestResult
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new() { WriteIndented = true };

    public string? IdealHttpRoute { get; set; }

    public string ActivityDisplayName { get; set; } = string.Empty;

    public string? ActivityHttpRoute { get; set; }

    public string? MetricHttpRoute { get; set; }

    public RouteInfo RouteInfo { get; set; } = new RouteInfo();

    [JsonIgnore]
    public RoutingTestCases.TestCase TestCase { get; set; } = new RoutingTestCases.TestCase();

    public override string ToString()
    {
        return JsonSerializer.Serialize(this, JsonSerializerOptions);
    }
}

// <copyright file="TestResult.cs" company="OpenTelemetry Authors">
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

#nullable enable

using System.Text.Json;
using System.Text.Json.Serialization;

namespace RouteTests;

public class TestResult
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new() { WriteIndented = true };

    public string? IdealHttpRoute { get; set; }

    public string ActivityDisplayName { get; set; } = string.Empty;

    public string? ActivityHttpRoute { get; set; }

    public string? MetricHttpRoute { get; set; }

    public RouteInfo RouteInfo { get; set; } = new RouteInfo();

    [JsonIgnore]
    public RouteTestData.RouteTestCase TestCase { get; set; } = new RouteTestData.RouteTestCase();

    public override string ToString()
    {
        return JsonSerializer.Serialize(this, JsonSerializerOptions);
    }
}

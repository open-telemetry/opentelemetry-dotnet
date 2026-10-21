// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter.Prometheus.Tests;

/// <summary>
/// Collection for the tests which lint a scrape with promtool. Parallelization is
/// disabled because the ASP.NET Core instrumentation subscribes to the
/// <c>Microsoft.AspNetCore.*</c> meters by name, so the hosts started by other
/// tests running at the same time leak their series (for example duplicate
/// <c>http_server_active_requests</c> samples) into the scrape being linted.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
public sealed class PromToolCollection : ICollectionFixture<PromToolFixture>
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix
{
    public const string Name = "PromTool";
}

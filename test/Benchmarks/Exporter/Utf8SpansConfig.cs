// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;

namespace Benchmarks.Exporter;

/// <summary>
/// Runs each benchmark with <c>OTEL_DOTNET_EXPERIMENTAL_OTLP_UTF8_SPANS</c> unset and set, since the
/// setting is read once per process.
/// </summary>
public sealed class Utf8SpansConfig : ManualConfig
{
    public Utf8SpansConfig()
    {
        this.AddJob(Job.Default.WithId("Utf8SpansOff").AsBaseline());
        this.AddJob(Job.Default.WithId("Utf8SpansOn").WithEnvironmentVariables(new EnvironmentVariable("OTEL_DOTNET_EXPERIMENTAL_OTLP_UTF8_SPANS", "true")));
    }
}

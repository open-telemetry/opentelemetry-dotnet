// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using BenchmarkDotNet.Attributes;

namespace OpenTelemetry.Benchmarks;

public class SuppressInstrumentationScopeBenchmarks
{
    [Benchmark]
    public void Begin()
    {
        using var scope1 = SuppressInstrumentationScope.Begin();

        using var scope2 = SuppressInstrumentationScope.Begin();

        using var scope3 = SuppressInstrumentationScope.Begin();
    }

    [Benchmark]
    public void Enter()
    {
        SuppressInstrumentationScope.Enter();

        SuppressInstrumentationScope.IncrementIfTriggered();

        SuppressInstrumentationScope.DecrementIfTriggered();

        SuppressInstrumentationScope.DecrementIfTriggered();
    }
}

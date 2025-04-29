// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using BenchmarkDotNet.Attributes;

namespace OpenTelemetry.Benchmarks;

public class SuppressInstrumentationScopeBenchmarks
{
    [Benchmark]
#pragma warning disable CA1822 // Mark members as static
    public void Begin()
#pragma warning restore CA1822 // Mark members as static
    {
        using var scope1 = SuppressInstrumentationScope.Begin();

        using var scope2 = SuppressInstrumentationScope.Begin();

        using var scope3 = SuppressInstrumentationScope.Begin();
    }

    [Benchmark]
#pragma warning disable CA1822 // Mark members as static
    public void Enter()
#pragma warning restore CA1822 // Mark members as static
    {
        SuppressInstrumentationScope.Enter();

        SuppressInstrumentationScope.IncrementIfTriggered();

        SuppressInstrumentationScope.DecrementIfTriggered();

        SuppressInstrumentationScope.DecrementIfTriggered();
    }
}

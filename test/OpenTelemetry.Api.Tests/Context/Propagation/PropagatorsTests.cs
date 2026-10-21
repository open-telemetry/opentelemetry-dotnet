// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Tests;

namespace OpenTelemetry.Context.Propagation.Tests;

[Collection(PropagatorsCollectionDefinition.Name)]
public sealed class PropagatorsTests : IDisposable
{
    public PropagatorsTests()
    {
        Propagators.Reset();
    }

    [Fact]
    public void DefaultTextMapPropagatorIsNoop()
    {
        Assert.IsType<NoopTextMapPropagator>(Propagators.DefaultTextMapPropagator);
        Assert.Same(Propagators.DefaultTextMapPropagator, Propagators.DefaultTextMapPropagator);
    }

    [Fact]
    public void CanSetPropagator()
    {
        var testPropagator = new TestPropagator(string.Empty, string.Empty);
        Propagators.DefaultTextMapPropagator = testPropagator;
        Assert.Same(testPropagator, Propagators.DefaultTextMapPropagator);
    }

    public void Dispose()
    {
        Propagators.Reset();
        GC.SuppressFinalize(this);
    }
}

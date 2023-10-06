// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace OpenTelemetry.Metrics.Tests;

public class MeterProviderSdkTest
{
    [Fact]
    public void BuilderTypeDoesNotChangeTest()
    {
        var originalBuilder = Sdk.CreateMeterProviderBuilder();
        var currentBuilder = originalBuilder;

        var deferredBuilder = currentBuilder as IDeferredMeterProviderBuilder;
        Assert.NotNull(deferredBuilder);

        currentBuilder = deferredBuilder.Configure((sp, innerBuilder) => { });
        Assert.True(ReferenceEquals(originalBuilder, currentBuilder));

        currentBuilder = currentBuilder.ConfigureServices(s => { });
        Assert.True(ReferenceEquals(originalBuilder, currentBuilder));

        currentBuilder = currentBuilder.AddInstrumentation(() => new object());
        Assert.True(ReferenceEquals(originalBuilder, currentBuilder));

        currentBuilder = currentBuilder.AddMeter("MySource");
        Assert.True(ReferenceEquals(originalBuilder, currentBuilder));

        using var provider = currentBuilder.Build();

        Assert.NotNull(provider);
    }
}

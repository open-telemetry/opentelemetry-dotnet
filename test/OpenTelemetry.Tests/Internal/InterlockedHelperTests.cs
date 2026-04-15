// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace OpenTelemetry.Internal.Tests;

public class InterlockedHelperTests
{
    [Fact]
    public async Task AddWhenCurrentValueIsNaNShouldNotHang()
    {
        var value = double.NaN;

        var task = Task.Run(() => InterlockedHelper.Add(ref value, 1d));

#if NET
        await task.WaitAsync(TimeSpan.FromSeconds(2));
#else
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(2), cts.Token)) == task;
        Assert.True(completed);
#endif

        Assert.True(double.IsNaN(value));
    }
}

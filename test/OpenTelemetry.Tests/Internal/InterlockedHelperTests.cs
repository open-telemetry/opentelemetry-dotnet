// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace OpenTelemetry.Internal.Tests;

public class InterlockedHelperTests
{
    [Fact]
    public async Task AddWhenCurrentValueIsNaNShouldNotHang()
    {
        var timeout = TimeSpan.FromSeconds(2);
        var value = double.NaN;

        var task = Task.Run(() => InterlockedHelper.Add(ref value, 1d));

#if NET
        await task.WaitAsync(timeout);
#else
        using var cts = new CancellationTokenSource(timeout);
        var completed = await Task.WhenAny(task, Task.Delay(timeout, cts.Token)) == task;
        Assert.True(completed);
#endif

        Assert.True(double.IsNaN(value));
    }
}

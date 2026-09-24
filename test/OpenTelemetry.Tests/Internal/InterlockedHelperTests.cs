// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Internal.Tests;

public class InterlockedHelperTests
{
    [Fact]
    public async Task AddWhenCurrentValueIsNaNShouldNotHang()
    {
        var timeout = TimeSpan.FromSeconds(2);
        var value = double.NaN;

        var task = Task.Run(() => InterlockedHelper.Add(ref value, 1d), TestContext.Current.CancellationToken);

#if NET
        await task.WaitAsync(timeout, TestContext.Current.CancellationToken);
#else
        using var cts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, TestContext.Current.CancellationToken);
        var completed = await Task.WhenAny(task, Task.Delay(timeout, linkedCts.Token)) == task;
        Assert.True(completed);
#endif

        Assert.True(double.IsNaN(value));
    }
}

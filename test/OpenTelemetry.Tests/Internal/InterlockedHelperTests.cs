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
        var completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(2))) == task;

        Assert.True(completed);
        Assert.True(double.IsNaN(value));
    }
}

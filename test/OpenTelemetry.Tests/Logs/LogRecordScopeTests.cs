// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace OpenTelemetry.Logs.Tests;

public class LogRecordScopeTests
{
    [Fact]
    public void Verify_Equals_SameScope()
    {
        var scope = new object();
        var record1 = new LogRecordScope(scope);
        var record2 = new LogRecordScope(scope);

        Assert.True(record1.Equals(record2));
        Assert.True(record1.Equals((object)record2));
    }

    [Fact]
    public void Verify_Equals_DifferentScope()
    {
        var record1 = new LogRecordScope("scope-a");
        var record2 = new LogRecordScope("scope-b");

        Assert.False(record1.Equals(record2));
        Assert.False(record1.Equals((object)record2));
    }

    [Fact]
    public void Verify_Equals_NullScope()
    {
        var record1 = new LogRecordScope(null);
        var record2 = new LogRecordScope(null);

        Assert.True(record1.Equals(record2));
        Assert.True(record1.Equals((object)record2));
    }

    [Fact]
    public void Verify_Equals_WrongType()
    {
        var record1 = new LogRecordScope("scope");
        Assert.False(record1.Equals("scope"));
        Assert.False(record1.Equals(42));
    }

    [Fact]
    public void VerifyOperator_Equals()
    {
        var scope = "my-scope";
        var record1 = new LogRecordScope(scope);
        var record2 = new LogRecordScope(scope);
        var record3 = new LogRecordScope("other-scope");

        Assert.True(record1 == record2);
        Assert.False(record1 == record3);
    }

    [Fact]
    public void VerifyOperator_NotEquals()
    {
        var scope = "my-scope";
        var record1 = new LogRecordScope(scope);
        var record2 = new LogRecordScope(scope);
        var record3 = new LogRecordScope("other-scope");

        Assert.False(record1 != record2);
        Assert.True(record1 != record3);
    }

    [Fact]
    public void Verify_GetHashCode()
    {
        var scope = "my-scope";
        var record1 = new LogRecordScope(scope);
        var record2 = new LogRecordScope(scope);
        var record3 = new LogRecordScope("other-scope");
        var nullRecord = new LogRecordScope(null);

        Assert.Equal(record1.GetHashCode(), record2.GetHashCode());
        Assert.NotEqual(record1.GetHashCode(), record3.GetHashCode());
        Assert.Equal(0, nullRecord.GetHashCode());
    }
}

// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace OpenTelemetry.Trace.Tests;

public class StatusTests
{
    [Fact]
    public void Status_Ok()
    {
        Assert.Equal(StatusCode.Ok, Status.Ok.StatusCode);
        Assert.Null(Status.Ok.Description);
    }

    [Fact]
    public void CheckingDefaultStatus()
    {
        Assert.Equal(default, Status.Unset);
    }

    [Fact]
    public void CreateStatus_Error_WithDescription()
    {
        var status = Status.Error.WithDescription("This is an error.");
        Assert.Equal(StatusCode.Error, status.StatusCode);
        Assert.Equal("This is an error.", status.Description);
    }

    [Fact]
    public void CreateStatus_Ok_WithDescription()
    {
        var status = Status.Ok.WithDescription("This is will not be set.");
        Assert.Equal(StatusCode.Ok, status.StatusCode);
        Assert.Null(status.Description);
    }

    [Fact]
    public void Equality()
    {
        var status1 = new Status(StatusCode.Ok);
        var status2 = new Status(StatusCode.Ok);
        object status3 = new Status(StatusCode.Ok);

        Assert.Equal(status1, status2);
        Assert.True(status1 == status2);
        Assert.True(status1.Equals(status3));
    }

    [Fact]
    public void Equality_WithDescription()
    {
        var status1 = new Status(StatusCode.Error, "error");
        var status2 = new Status(StatusCode.Error, "error");

        Assert.Equal(status1, status2);
        Assert.True(status1 == status2);
    }

    [Fact]
    public void Not_Equality()
    {
        var status1 = new Status(StatusCode.Ok);
        var status2 = new Status(StatusCode.Error);
        object notStatus = 1;

        Assert.NotEqual(status1, status2);
        Assert.True(status1 != status2);
        Assert.False(status1.Equals(notStatus));
    }

    [Fact]
    public void Not_Equality_WithDescription1()
    {
        var status1 = new Status(StatusCode.Ok, "ok");
        var status2 = new Status(StatusCode.Error, "error");

        Assert.NotEqual(status1, status2);
        Assert.True(status1 != status2);
    }

    [Fact]
    public void Not_Equality_WithDescription2()
    {
        var status1 = new Status(StatusCode.Ok);
        var status2 = new Status(StatusCode.Error, "error");

        Assert.NotEqual(status1, status2);
        Assert.True(status1 != status2);
    }

    [Fact]
    public void TestToString()
    {
        var status = new Status(StatusCode.Ok);
        Assert.Equal($"Status{{StatusCode={status.StatusCode}, Description={status.Description}}}", status.ToString());
    }

    [Fact]
    public void TestGetHashCode()
    {
        var status = new Status(StatusCode.Ok);
        Assert.NotEqual(0, status.GetHashCode());
    }
}

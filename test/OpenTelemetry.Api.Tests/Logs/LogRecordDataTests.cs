// <copyright file="LogRecordDataTests.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

#nullable enable

using System.Diagnostics;
using Xunit;

namespace OpenTelemetry.Logs.Tests;

public sealed class LogRecordDataTests
{
    [Fact]
    public void ParameterlessConstructorWithActiveActivityTest()
    {
        using var activity = new Activity("Test");
        activity.ActivityTraceFlags = ActivityTraceFlags.Recorded;
        activity.Start();

        var record = new LogRecordData();

        Assert.Equal(activity.TraceId, record.TraceId);
        Assert.Equal(activity.SpanId, record.SpanId);
        Assert.Equal(activity.ActivityTraceFlags, record.TraceFlags);

        record = default;

        Assert.Equal(default, record.TraceId);
        Assert.Equal(default, record.SpanId);
        Assert.Equal(default, record.TraceFlags);
    }

    [Fact]
    public void ParameterlessConstructorWithoutActiveActivityTest()
    {
        var record = new LogRecordData();

        Assert.Equal(default, record.TraceId);
        Assert.Equal(default, record.SpanId);
        Assert.Equal(default, record.TraceFlags);
    }

    [Fact]
    public void ActivityConstructorTest()
    {
        using var activity = new Activity("Test");
        activity.ActivityTraceFlags = ActivityTraceFlags.Recorded;
        activity.Start();

        Activity.Current = null;

        var record = new LogRecordData(activity);

        Assert.Equal(activity.TraceId, record.TraceId);
        Assert.Equal(activity.SpanId, record.SpanId);
        Assert.Equal(activity.ActivityTraceFlags, record.TraceFlags);

        record = new LogRecordData(activity: null);

        Assert.Equal(default, record.TraceId);
        Assert.Equal(default, record.SpanId);
        Assert.Equal(default, record.TraceFlags);
    }

    [Fact]
    public void ActivityContextConstructorTest()
    {
        var context = new ActivityContext(
            ActivityTraceId.CreateRandom(),
            ActivitySpanId.CreateRandom(),
            ActivityTraceFlags.Recorded,
            traceState: null,
            isRemote: true);

        var record = new LogRecordData(in context);

        Assert.Equal(context.TraceId, record.TraceId);
        Assert.Equal(context.SpanId, record.SpanId);
        Assert.Equal(context.TraceFlags, record.TraceFlags);

        record = new LogRecordData(activityContext: default);

        Assert.Equal(default, record.TraceId);
        Assert.Equal(default, record.SpanId);
        Assert.Equal(default, record.TraceFlags);
    }

    [Fact]
    public void TimestampTest()
    {
        var nowUtc = DateTime.UtcNow;

        var record = new LogRecordData();
        Assert.True(record.Timestamp >= nowUtc);

        record = default;
        Assert.Equal(DateTime.MinValue, record.Timestamp);

        var now = DateTime.Now;

        record.Timestamp = now;

        Assert.Equal(DateTimeKind.Utc, record.Timestamp.Kind);
        Assert.Equal(now.ToUniversalTime(), record.Timestamp);
    }

    [Fact]
    public void SetActivityContextTest()
    {
        LogRecordData record = default;

        using var activity = new Activity("Test");
        activity.ActivityTraceFlags = ActivityTraceFlags.Recorded;
        activity.Start();

        LogRecordData.SetActivityContext(ref record, activity);

        Assert.Equal(activity.TraceId, record.TraceId);
        Assert.Equal(activity.SpanId, record.SpanId);
        Assert.Equal(activity.ActivityTraceFlags, record.TraceFlags);

        LogRecordData.SetActivityContext(ref record, activity: null);

        Assert.Equal(default, record.TraceId);
        Assert.Equal(default, record.SpanId);
        Assert.Equal(default, record.TraceFlags);
    }
}

// <copyright file="ExceptionProcessorTest.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Trace.Tests;

public class ExceptionProcessorTest
{
    [Fact]
    public void ActivityStatusSetToErrorWhenExceptionProcessorEnabled()
    {
        var activitySourceName = Utils.GetCurrentMethodName();
        using var activitySource = new ActivitySource(activitySourceName);
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(activitySourceName)
            .SetSampler(new AlwaysOnSampler())
            .AddProcessor(new ExceptionProcessor())
            .Build();

        Activity activity1 = null;
        Activity activity2 = null;
        Activity activity3 = null;
        Activity activity4 = null;
        Activity activity5 = null;

        try
        {
            using (activity1 = activitySource.StartActivity("Activity1"))
            {
                using (activity2 = activitySource.StartActivity("Activity2"))
                {
                    throw new Exception("Oops!");
                }
            }
        }
        catch (Exception)
        {
            using (activity3 = activitySource.StartActivity("Activity3"))
            {
            }
        }
        finally
        {
            using (activity4 = activitySource.StartActivity("Activity4"))
            {
            }
        }

        try
        {
            throw new Exception("Oops!");
        }
        catch (Exception)
        {
            /*
               Note: Behavior here is different depending on the processor
               architecture.

               x86: Exception is cleared BEFORE the catch runs.
               Marshal.GetExceptionPointers returns zero.

               non-x86: Exception is cleared AFTER the catch runs.
               Marshal.GetExceptionPointers returns non-zero.
            */
            activity5 = activitySource.StartActivity("Activity5");
        }
        finally
        {
            activity5.Dispose();
        }

        Assert.Equal(StatusCode.Error, activity1.GetStatus().StatusCode);
        Assert.Null(GetTagValue(activity1, "otel.exception_pointers"));
        Assert.Equal(StatusCode.Error, activity2.GetStatus().StatusCode);
        Assert.Null(GetTagValue(activity2, "otel.exception_pointers"));
        Assert.Equal(StatusCode.Unset, activity3.GetStatus().StatusCode);
        Assert.Null(GetTagValue(activity3, "otel.exception_pointers"));
        Assert.Equal(StatusCode.Unset, activity4.GetStatus().StatusCode);
        Assert.Null(GetTagValue(activity4, "otel.exception_pointers"));
        Assert.Equal(StatusCode.Unset, activity5.GetStatus().StatusCode);
#if !NETFRAMEWORK
        if (Environment.Is64BitProcess)
        {
            // In this rare case, the following Activity tag will not get cleaned up due to perf consideration.
            Assert.NotNull(GetTagValue(activity5, "otel.exception_pointers"));
        }
        else
        {
            Assert.Null(GetTagValue(activity5, "otel.exception_pointers"));
        }
#endif
    }

    [Fact]
    public void ActivityStatusNotSetWhenExceptionProcessorNotEnabled()
    {
        var activitySourceName = Utils.GetCurrentMethodName();
        using var activitySource = new ActivitySource(activitySourceName);
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(activitySourceName)
            .SetSampler(new AlwaysOnSampler())
            .Build();

        Activity activity = null;

        try
        {
            using (activity = activitySource.StartActivity("Activity"))
            {
                throw new Exception("Oops!");
            }
        }
        catch (Exception)
        {
        }

        Assert.Equal(StatusCode.Unset, activity.GetStatus().StatusCode);
    }

    private static object GetTagValue(Activity activity, string tagName)
    {
        Debug.Assert(activity != null, "Activity should not be null");

        foreach (ref readonly var tag in activity.EnumerateTagObjects())
        {
            if (tag.Key == tagName)
            {
                return tag.Value;
            }
        }

        return null;
    }
}

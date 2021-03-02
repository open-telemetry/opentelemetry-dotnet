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

using System;
using System.Diagnostics;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Trace.Tests
{
    public class ExceptionProcessorTest
    {
        private const string ActivitySourceName = "ExceptionProcessorTest";

        [Fact]
        public void ActivityStatusSetToErrorWhenExceptionProcessorEnabled()
        {
            using var activitySource = new ActivitySource(ActivitySourceName);
            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddSource(ActivitySourceName)
                .SetSampler(new AlwaysOnSampler())
                .AddProcessor(new ExceptionProcessor())
                .Build();

            Activity activity1 = null;
            Activity activity2 = null;
            Activity activity3 = null;
            Activity activity4 = null;

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

            Assert.Equal(StatusCode.Error, activity1.GetStatus().StatusCode);
            Assert.Equal(StatusCode.Error, activity2.GetStatus().StatusCode);
            Assert.Equal(StatusCode.Unset, activity3.GetStatus().StatusCode);
            Assert.Equal(StatusCode.Unset, activity4.GetStatus().StatusCode);
        }

        [Fact]
        public void ActivityStatusNotSetWhenExceptionProcessorNotEnabled()
        {
            using var activitySource = new ActivitySource(ActivitySourceName);
            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddSource(ActivitySourceName)
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
    }
}

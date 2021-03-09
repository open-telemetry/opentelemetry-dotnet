// <copyright file="TracerProviderBuilderExtensionsTest.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Trace.Tests
{
    public class TracerProviderBuilderExtensionsTest
    {
        private const string ActivitySourceName = "TracerProviderBuilderExtensionsTest";

        [Fact]
        public void AddLegacyOperationName_NullBuilder_Noop()
        {
            TracerProviderBuilder builder = null;

            // No exception is thrown on executing this line
            builder.AddLegacySource("TestOperationName");
            using var provider = builder.Build();

            var emptyActivitySource = new ActivitySource(string.Empty);
            Assert.False(emptyActivitySource.HasListeners()); // Check if AddLegacyOperationName was noop after TracerProviderBuilder.Build
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void AddLegacyOperationName_BadArgs(string operationName)
        {
            var builder = Sdk.CreateTracerProviderBuilder();
            Assert.Throws<ArgumentException>(() => builder.AddLegacySource(operationName));
        }

        [Fact]
        public void AddLegacyOperationNameAddsActivityListenerForEmptyActivitySource()
        {
            var emptyActivitySource = new ActivitySource(string.Empty);
            var builder = Sdk.CreateTracerProviderBuilder();
            builder.AddLegacySource("TestOperationName");

            Assert.False(emptyActivitySource.HasListeners());
            using var provider = builder.Build();
            Assert.True(emptyActivitySource.HasListeners());
        }

        [Fact]
        public void SetErrorStatusOnExceptionEnabled()
        {
            using var activitySource = new ActivitySource(ActivitySourceName);
            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddSource(ActivitySourceName)
                .SetSampler(new AlwaysOnSampler())
                .SetErrorStatusOnException(false)
                .SetErrorStatusOnException(false)
                .SetErrorStatusOnException(true)
                .SetErrorStatusOnException(true)
                .SetErrorStatusOnException(false)
                .SetErrorStatusOnException()
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

            Assert.Equal(StatusCode.Error, activity.GetStatus().StatusCode);
        }

        [Fact]
        public void SetErrorStatusOnExceptionDisabled()
        {
            using var activitySource = new ActivitySource(ActivitySourceName);
            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddSource(ActivitySourceName)
                .SetSampler(new AlwaysOnSampler())
                .SetErrorStatusOnException()
                .SetErrorStatusOnException(false)
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

        [Fact]
        public void SetErrorStatusOnExceptionDefault()
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

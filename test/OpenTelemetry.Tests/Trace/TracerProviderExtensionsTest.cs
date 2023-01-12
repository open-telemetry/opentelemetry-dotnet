// <copyright file="TracerProviderExtensionsTest.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace.Tests
{
    public class TracerProviderExtensionsTest
    {
        [Fact]
        public void Verify_ForceFlush_HandlesException()
        {
            using var testProcessor = new DelegatingProcessor<Activity>();

            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddProcessor(testProcessor)
                .Build();

            Assert.True(tracerProvider.ForceFlush());

            testProcessor.OnForceFlushFunc = (timeout) => throw new Exception("test exception");

            Assert.False(tracerProvider.ForceFlush());
        }

        [Fact]
        public void Verify_Shutdown_HandlesSecond()
        {
            using var testProcessor = new DelegatingProcessor<Activity>();

            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddProcessor(testProcessor)
                .Build();

            Assert.True(tracerProvider.Shutdown());
            Assert.False(tracerProvider.Shutdown());
        }

        [Fact]
        public void Verify_Shutdown_HandlesException()
        {
            using var testProcessor = new DelegatingProcessor<Activity>
            {
                OnShutdownFunc = (timeout) => throw new Exception("test exception"),
            };

            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddProcessor(testProcessor)
                .Build();

            Assert.False(tracerProvider.Shutdown());
        }
    }
}

﻿// <copyright file="EventSourceTest.cs" company="OpenTelemetry Authors">
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

using OpenTelemetry.Instrumentation;
using OpenTelemetry.Internal;
using Xunit;

namespace OpenTelemetry.Tests
{
    public class EventSourceTest
    {
        [Fact]
        public void EventSourceTest_InstrumentationEventSource()
        {
            EventSourceTestHelper.MethodsAreImplementedConsistentlyWithTheirAttributes(InstrumentationEventSource.Log);
        }

        [Fact]
        public void EventSourceTest_OpenTelemetryApiEventSource()
        {
            EventSourceTestHelper.MethodsAreImplementedConsistentlyWithTheirAttributes(OpenTelemetryApiEventSource.Log);
        }

        [Fact]
        public void EventSourceTest_OpenTelemetrySdkEventSource()
        {
            EventSourceTestHelper.MethodsAreImplementedConsistentlyWithTheirAttributes(OpenTelemetrySdkEventSource.Log);
        }
    }
}

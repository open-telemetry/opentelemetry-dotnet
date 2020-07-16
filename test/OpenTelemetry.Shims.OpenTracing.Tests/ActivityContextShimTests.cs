// <copyright file="ActivityContextShimTests.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Shims.OpenTracing.Tests
{
    public class ActivityContextShimTests
    {
        [Fact]
        public void CtorArgumentValidation()
        {
            Assert.Throws<ArgumentException>(() => new ActivityContextShim(default));
            Assert.Throws<ArgumentException>(() => new ActivityContextShim(new ActivityContext(default, default, ActivityTraceFlags.None)));
        }

        [Fact]
        public void GetTraceId()
        {
            var shim = GetSpanContextShim();

            Assert.Equal(shim.Context.TraceId.ToString(), shim.TraceId);
        }

        [Fact]
        public void GetSpanId()
        {
            var shim = GetSpanContextShim();

            Assert.Equal(shim.Context.SpanId.ToString(), shim.SpanId);
        }

        [Fact]
        public void GetBaggage()
        {
            // TODO
        }

        internal static ActivityContextShim GetSpanContextShim()
        {
            return new ActivityContextShim(new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.None));
        }
    }
}

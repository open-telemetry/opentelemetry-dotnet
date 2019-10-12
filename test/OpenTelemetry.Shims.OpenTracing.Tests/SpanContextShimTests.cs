// <copyright file="SpanContextShimTests.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
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
    public class SpanContextShimTests
    {
        [Fact]
        public void CtorArgumentValidation()
        {
            Assert.Throws<ArgumentNullException>(() => new SpanContextShim(null));
            Assert.Throws<ArgumentException>(() => new SpanContextShim(SpanContext.Blank));
            Assert.Throws<ArgumentException>(() => new SpanContextShim(new SpanContext(default, default, ActivityTraceFlags.None)));
        }

        [Fact]
        public void GetTraceId()
        {
            var shim = GetSpanContextShim();

            Assert.Equal(shim.SpanContext.TraceId.ToString(), shim.TraceId);
        }

        [Fact]
        public void GetSpanId()
        {
            var shim = GetSpanContextShim();

            Assert.Equal(shim.SpanContext.SpanId.ToString(), shim.SpanId);
        }

        [Fact]
        public void GetBaggage()
        {
            // TODO
        }

        internal static SpanContextShim GetSpanContextShim()
        {
            return new SpanContextShim(Defaults.GetOpenTelemetrySpanContext());
        }
    }
}

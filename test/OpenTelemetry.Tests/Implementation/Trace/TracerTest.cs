// <copyright file="TracerTest.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Diagnostics;
using Xunit;

namespace OpenTelemetry.Trace.Test
{
    public class TracerTest : IDisposable
    {
        // TODO: This is only a basic test. This must cover the entire shim API scenarios.
        private readonly Tracer tracer;

        public TracerTest()
        {
            this.tracer = TracerProvider.Default.GetTracer("tracername", "tracerversion");
        }

        [Fact]
        public void CurrentSpanNullByDefault()
        {
            var current = this.tracer.CurrentSpan;
            Assert.True(IsNoOpSpan(current));
            Assert.False(current.Context.IsValid);
        }

        [Fact]
        public void TracerStartWithSpan()
        {
            this.tracer.WithSpan(TelemetrySpan.NoopInstance);
            var current = this.tracer.CurrentSpan;
            Assert.Same(current, TelemetrySpan.NoopInstance);
        }

        [Fact]
        public void TracerStartReturnsNoOpSpanWhenNoSdk()
        {
            var span = this.tracer.StartSpan("name");
            Assert.True(IsNoOpSpan(span));
            Assert.False(span.Context.IsValid);
            Assert.False(span.IsRecording);
        }

        public void Dispose()
        {
            Activity.Current = null;
        }

        private static bool IsNoOpSpan(TelemetrySpan span)
        {
            return span.Activity == null;
        }
    }
}

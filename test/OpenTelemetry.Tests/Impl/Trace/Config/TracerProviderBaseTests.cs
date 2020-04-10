// <copyright file="TracerProviderBaseTests.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Configuration;
using Xunit;

namespace OpenTelemetry.Tests.Impl.Trace.Config
{
    public class TracerProviderBaseTests : IDisposable
    {
        public TracerProviderBaseTests()
        {
            TracerProviderBase.Default.Reset();
        }

        [Fact]
        public void TraceProvider_Default()
        {
            Assert.NotNull(TracerProviderBase.Default);
            var defaultTracer = TracerProviderBase.Default.GetTracer("");
            Assert.NotNull(defaultTracer);
            Assert.Same(defaultTracer, TracerProviderBase.Default.GetTracer("named tracerSdk"));

            var span = defaultTracer.StartSpan("foo");
            Assert.IsType<BlankSpan>(span);
        }

        [Fact]
        public void TraceProvider_SetDefault()
        {
            var provider = TracerProvider.Create(b => { });
            TracerProviderBase.SetDefault(provider);

            var defaultTracer = TracerProviderBase.Default.GetTracer("");
            Assert.NotNull(defaultTracer);
            Assert.IsType<TracerSdk>(defaultTracer);

            Assert.NotSame(defaultTracer, TracerProviderBase.Default.GetTracer("named tracerSdk"));

            var span = defaultTracer.StartSpan("foo");
            Assert.IsType<SpanSdk>(span);
        }

        [Fact]
        public void TraceProvider_SetDefaultNull()
        {
            Assert.Throws<ArgumentNullException>(() => TracerProviderBase.SetDefault(null));
        }

        [Fact]
        public void TraceProvider_SetDefaultTwice_Throws()
        {
            TracerProviderBase.SetDefault(TracerProvider.Create(b => { }));
            Assert.Throws<InvalidOperationException>(() => TracerProviderBase.SetDefault(TracerProvider.Create(b => { })));
        }

        [Fact]
        public void TraceProvider_UpdateDefault_CachedTracer()
        {
            var defaultTracer = TracerProviderBase.Default.GetTracer("");
            var noopSpan = defaultTracer.StartSpan("foo");
            Assert.IsType<BlankSpan>(noopSpan);

            TracerProviderBase.SetDefault(TracerProvider.Create(b => { }));
            var span = defaultTracer.StartSpan("foo");
            Assert.IsType<SpanSdk>(span);

            var newDefaultTracer = TracerProviderBase.Default.GetTracer("");
            Assert.NotSame(defaultTracer, newDefaultTracer);
            Assert.IsType<TracerSdk>(newDefaultTracer);
        }

        public void Dispose()
        {
            TracerProviderBase.Default.Reset();
        }
    }
}

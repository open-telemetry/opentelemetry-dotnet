// <copyright file="TracerFactoryTests.cs" company="OpenTelemetry Authors">
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
    public class TracerFactoryTests : IDisposable
    {
        public TracerFactoryTests()
        {
            TracerFactory.Reset();
        }

        [Fact]
        public void TraceFactory_Default()
        {
            Assert.NotNull(TracerFactory.Default);
            var defaultTracer = TracerFactory.Default.GetTracer("");
            Assert.NotNull(defaultTracer);
            Assert.Same(defaultTracer, TracerFactory.Default.GetTracer("named tracerSdk"));

            var span = defaultTracer.StartSpan("foo");
            Assert.IsType<NoOpSpan>(span);
        }

        [Fact]
        public void TraceFactory_SetDefault()
        {
            var factory = TracerProviderSdk.Create(b => { });
            TracerFactory.SetDefault(factory);

            var defaultTracer = TracerFactory.Default.GetTracer("");
            Assert.NotNull(defaultTracer);
            Assert.IsType<TracerSdk>(defaultTracer);

            Assert.NotSame(defaultTracer, TracerFactory.Default.GetTracer("named tracerSdk"));

            var span = defaultTracer.StartSpan("foo");
            Assert.IsType<SpanSdk>(span);
        }

        [Fact]
        public void TraceFactory_SetDefaultNull()
        {
            Assert.Throws<ArgumentNullException>(() => TracerFactory.SetDefault(null));
        }

        [Fact]
        public void TraceFactory_SetDefaultTwice_Throws()
        {
            TracerFactory.SetDefault(TracerProviderSdk.Create(b => { }));
            Assert.Throws<InvalidOperationException>(() => TracerFactory.SetDefault(TracerProviderSdk.Create(b => { })));
        }

        [Fact]
        public void TraceFactory_UpdateDefault_CachedTracer()
        {
            var defaultTracer = TracerFactory.Default.GetTracer("");
            var noopSpan = defaultTracer.StartSpan("foo");
            Assert.IsType<NoOpSpan>(noopSpan);

            TracerFactory.SetDefault(TracerProviderSdk.Create(b => { }));
            var span = defaultTracer.StartSpan("foo");
            Assert.IsType<SpanSdk>(span);

            var newDefaultTracer = TracerFactory.Default.GetTracer("");
            Assert.NotSame(defaultTracer, newDefaultTracer);
            Assert.IsType<TracerSdk>(newDefaultTracer);
        }

        public void Dispose()
        {
            TracerFactory.Reset();
        }
    }
}

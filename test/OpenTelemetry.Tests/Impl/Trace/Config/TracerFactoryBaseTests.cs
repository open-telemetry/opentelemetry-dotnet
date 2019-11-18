// <copyright file="TracerFactoryBaseTests.cs" company="OpenTelemetry Authors">
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
    public class TracerFactoryBaseTests : IDisposable
    {
        public TracerFactoryBaseTests()
        {
            TracerFactoryBase.Default.Reset();
        }

        [Fact]
        public void TraceFactory_Default()
        {
            Assert.NotNull(TracerFactoryBase.Default);
            var defaultTracer = TracerFactoryBase.Default.GetTracer("");
            Assert.NotNull(defaultTracer);
            Assert.Same(defaultTracer, TracerFactoryBase.Default.GetTracer("named tracer"));

            var span = defaultTracer.StartSpan("foo");
            Assert.IsType<BlankSpan>(span);
        }

        [Fact]
        public void TraceFactory_SetDefault()
        {
            var factory = TracerFactory.Create(b => { });
            TracerFactoryBase.SetDefault(factory);

            var defaultTracer = TracerFactoryBase.Default.GetTracer("");
            Assert.NotNull(defaultTracer);
            Assert.IsType<Tracer>(defaultTracer);

            Assert.NotSame(defaultTracer, TracerFactoryBase.Default.GetTracer("named tracer"));

            var span = defaultTracer.StartSpan("foo");
            Assert.IsType<Span>(span);
        }

        [Fact]
        public void TraceFactory_SetDefaultNull()
        {
            Assert.Throws<ArgumentNullException>(() => TracerFactoryBase.SetDefault(null));
        }

        [Fact]
        public void TraceFactory_SetDefaultTwice_Throws()
        {
            TracerFactoryBase.SetDefault(TracerFactory.Create(b => { }));
            Assert.Throws<InvalidOperationException>(() => TracerFactoryBase.SetDefault(TracerFactory.Create(b => { })));
        }

        [Fact]
        public void TraceFactory_UpdateDefault_CachedTracer()
        {
            var defaultTracer = TracerFactoryBase.Default.GetTracer("");
            var noopSpan = defaultTracer.StartSpan("foo");
            Assert.IsType<BlankSpan>(noopSpan);

            TracerFactoryBase.SetDefault(TracerFactory.Create(b => { }));
            var span = defaultTracer.StartSpan("foo");
            Assert.IsType<Span>(span);

            var newDefaultTracer = TracerFactoryBase.Default.GetTracer("");
            Assert.NotSame(defaultTracer, newDefaultTracer);
            Assert.IsType<Tracer>(newDefaultTracer);
        }

        public void Dispose()
        {
            TracerFactoryBase.Default.Reset();
        }
    }
}

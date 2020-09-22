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
using System.Diagnostics;
using Xunit;

namespace OpenTelemetry.Trace.Tests
{
    public class TracerTest : IDisposable
    {
        // TODO: This is only a basic test. This must cover the entire shim API scenarios.
        private readonly TracerProvider tracerProvider;
        private readonly Tracer tracer;

        public TracerTest()
        {
            this.tracerProvider = TracerProvider.Default;
            this.tracer = TracerProvider.Default.GetTracer("tracername", "tracerversion");
        }

        [Fact]
        public void CurrentSpanNullByDefault()
        {
            var current = Tracer.CurrentSpan;
            Assert.True(IsNoopSpan(current));
            Assert.False(current.Context.IsValid);
        }

        [Fact]
        public void TracerStartWithSpan()
        {
            Tracer.WithSpan(TelemetrySpan.NoopInstance);
            var current = Tracer.CurrentSpan;
            Assert.Same(current, TelemetrySpan.NoopInstance);
        }

        [Fact]
        public void TracerStartReturnsNoopSpanWhenNoSdk()
        {
            var span = this.tracer.StartSpan("name");
            Assert.True(IsNoopSpan(span));
            Assert.False(span.Context.IsValid);
            Assert.False(span.IsRecording);
        }

        [Fact]
        public void Tracer_StartRootSpan_BadArgs_NullSpanName()
        {
            using var openTelemetry = Sdk.CreateTracerProviderBuilder()
                .AddSource("tracername")
                .Build();

            var span1 = this.tracer.StartRootSpan(null);
            Assert.Null(span1.Activity.DisplayName);

            var span2 = this.tracer.StartRootSpan(null, SpanKind.Client);
            Assert.Null(span2.Activity.DisplayName);

            var span3 = this.tracer.StartRootSpan(null, SpanKind.Client, default);
            Assert.Null(span3.Activity.DisplayName);
        }

        [Fact]
        public void Tracer_StartSpan_BadArgs_NullSpanName()
        {
            using var openTelemetry = Sdk.CreateTracerProviderBuilder()
                .AddSource("tracername")
                .Build();

            var span1 = this.tracer.StartSpan(null);
            Assert.Null(span1.Activity.DisplayName);

            var span2 = this.tracer.StartSpan(null, SpanKind.Client);
            Assert.Null(span2.Activity.DisplayName);

            var span3 = this.tracer.StartSpan(null, SpanKind.Client, null);
            Assert.Null(span3.Activity.DisplayName);
        }

        [Fact]
        public void Tracer_StartActiveSpan_BadArgs_NullSpanName()
        {
            using var openTelemetry = Sdk.CreateTracerProviderBuilder()
                .AddSource("tracername")
                .Build();

            var span1 = this.tracer.StartActiveSpan(null);
            Assert.Null(span1.Activity.DisplayName);

            var span2 = this.tracer.StartActiveSpan(null, SpanKind.Client);
            Assert.Null(span2.Activity.DisplayName);

            var span3 = this.tracer.StartActiveSpan(null, SpanKind.Client, null);
            Assert.Null(span3.Activity.DisplayName);
        }

        [Fact]
        public void Tracer_StartSpan_FromParent_BadArgs_NullSpanName()
        {
            using var openTelemetry = Sdk.CreateTracerProviderBuilder()
                .AddSource("tracername")
                .Build();

            var span1 = this.tracer.StartSpan(null, SpanKind.Client, TelemetrySpan.NoopInstance);
            Assert.Null(span1.Activity.DisplayName);

            var span2 = this.tracer.StartSpan(null, SpanKind.Client, TelemetrySpan.NoopInstance, default);
            Assert.Null(span2.Activity.DisplayName);
        }

        [Fact]
        public void Tracer_StartSpan_FromParentContext_BadArgs_NullSpanName()
        {
            using var openTelemetry = Sdk.CreateTracerProviderBuilder()
                .AddSource("tracername")
                .Build();

            var blankContext = default(SpanContext);

            var span1 = this.tracer.StartSpan(null, SpanKind.Client, blankContext);
            Assert.Null(span1.Activity.DisplayName);

            var span2 = this.tracer.StartSpan(null, SpanKind.Client, blankContext, default);
            Assert.Null(span2.Activity.DisplayName);
        }

        [Fact]
        public void Tracer_StartActiveSpan_FromParent_BadArgs_NullSpanName()
        {
            using var openTelemetry = Sdk.CreateTracerProviderBuilder()
                .AddSource("tracername")
                .Build();

            var span1 = this.tracer.StartActiveSpan(null, SpanKind.Client, TelemetrySpan.NoopInstance);
            Assert.Null(span1.Activity.DisplayName);

            var span2 = this.tracer.StartActiveSpan(null, SpanKind.Client, TelemetrySpan.NoopInstance, default);
            Assert.Null(span2.Activity.DisplayName);
        }

        [Fact]
        public void Tracer_StartActiveSpan_FromParentContext_BadArgs_NullSpanName()
        {
            using var openTelemetry = Sdk.CreateTracerProviderBuilder()
                .AddSource("tracername")
                .Build();

            var blankContext = default(SpanContext);

            var span1 = this.tracer.StartActiveSpan(null, SpanKind.Client, blankContext);
            Assert.Null(span1.Activity.DisplayName);

            var span2 = this.tracer.StartActiveSpan(null, SpanKind.Client, blankContext, default);
            Assert.Null(span2.Activity.DisplayName);
        }

        [Fact]
        public void Tracer_StartActiveSpan_CreatesActiveSpan()
        {
            using var openTelemetry = Sdk.CreateTracerProviderBuilder()
                .AddSource("tracername")
                .Build();

            var span1 = this.tracer.StartActiveSpan("Test");
            Assert.Equal(span1.Activity.SpanId, Tracer.CurrentSpan.Context.SpanId);

            var span2 = this.tracer.StartActiveSpan("Test", SpanKind.Client);
            Assert.Equal(span2.Activity.SpanId, Tracer.CurrentSpan.Context.SpanId);

            var span = this.tracer.StartSpan("foo");
            Tracer.WithSpan(span);

            var span3 = this.tracer.StartActiveSpan("Test", SpanKind.Client, span);
            Assert.Equal(span3.Activity.SpanId, Tracer.CurrentSpan.Context.SpanId);

            var spanContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
            var span4 = this.tracer.StartActiveSpan("Test", SpanKind.Client, spanContext);
            Assert.Equal(span4.Activity.SpanId, Tracer.CurrentSpan.Context.SpanId);
        }

        [Fact]
        public void GetCurrentSpanBlank()
        {
            using var openTelemetry = Sdk.CreateTracerProviderBuilder()
                .AddSource("tracername")
                .Build();
            Assert.False(Tracer.CurrentSpan.Context.IsValid);
        }

        [Fact]
        public void GetCurrentSpanBlankWontThrowOnEnd()
        {
            using var openTelemetry = Sdk.CreateTracerProviderBuilder()
                .AddSource("tracername")
                .Build();
            var current = Tracer.CurrentSpan;
            current.End();
        }

        [Fact]
        public void GetCurrentSpan()
        {
            using var openTelemetry = Sdk.CreateTracerProviderBuilder()
                .AddSource("tracername")
                .Build();

            var span = this.tracer.StartSpan("foo");
            Tracer.WithSpan(span);

            Assert.Equal(span.Context.SpanId, Tracer.CurrentSpan.Context.SpanId);
            Assert.True(Tracer.CurrentSpan.Context.IsValid);
        }

        [Fact]
        public void CreateSpan_Sampled()
        {
            using var openTelemetry = Sdk.CreateTracerProviderBuilder()
                .AddSource("tracername")
                .Build();
            var span = this.tracer.StartSpan("foo");
            Assert.True(span.IsRecording);
        }

        [Fact]
        public void CreateSpan_NotSampled()
        {
            using var openTelemetry = Sdk.CreateTracerProviderBuilder()
                .AddSource("tracername")
                .SetSampler(new AlwaysOffSampler())
                .Build();

            var span = this.tracer.StartSpan("foo");
            Assert.False(span.IsRecording);
        }

        public void Dispose()
        {
            Activity.Current = null;
            this.tracerProvider.Dispose();
            GC.SuppressFinalize(this);
        }

        private static bool IsNoopSpan(TelemetrySpan span)
        {
            return span.Activity == null;
        }
    }
}

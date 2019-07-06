// <copyright file="SpanBuilderTest.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace.Test
{
    using System;
    using System.Diagnostics;
    using Moq;
    using OpenTelemetry.Common;
    using OpenTelemetry.Trace.Config;
    using OpenTelemetry.Trace.Sampler;
    using Xunit;

    public class SpanBuilderTest
    {
        private static readonly string SpanName = "MySpanName";
        private readonly SpanBuilderOptions spanBuilderOptions;

        private readonly TraceParams alwaysSampleTraceParams =
            TraceParams.Default.ToBuilder().SetSampler(Samplers.AlwaysSample).Build();

        private readonly IStartEndHandler startEndHandler = Mock.Of<IStartEndHandler>();
        private readonly ITraceConfig traceConfig = Mock.Of<ITraceConfig>();

        public SpanBuilderTest()
        {
            // MockitoAnnotations.initMocks(this);
            spanBuilderOptions =
                new SpanBuilderOptions(startEndHandler, traceConfig);
            var configMock = Mock.Get<ITraceConfig>(traceConfig);
            configMock.Setup((c) => c.ActiveTraceParams).Returns(alwaysSampleTraceParams);
            // when(traceConfig.getActiveTraceParams()).thenReturn(alwaysSampleTraceParams);
        }

        [Fact]
        public void StartSpanNullParent()
        {
            var span = new SpanBuilder(SpanName, spanBuilderOptions)
                .SetSpanKind(SpanKind.Internal)
                .SetNoParent()
                .StartSpan();
            Assert.True(span.Context.IsValid);
            Assert.True(span.IsRecordingEvents);
            Assert.True((span.Context.TraceOptions & ActivityTraceFlags.Recorded) != 0);
            var spanData = ((Span)span).ToSpanData();
            Assert.True(spanData.ParentSpanId == default);
            Assert.InRange(spanData.StartTimestamp,
                Timestamp.FromDateTimeOffset(DateTimeOffset.Now).AddDuration(Duration.Create(-1, 0)),
                Timestamp.FromDateTimeOffset(DateTimeOffset.Now).AddDuration(Duration.Create(1, 0)));
            Assert.Equal(SpanName, spanData.Name);
        }

        [Fact]
        public void StartSpanLastParentWins1()
        {
            var spanContext =
                SpanContext.Create(
                    ActivityTraceId.CreateRandom(),
                    ActivitySpanId.CreateRandom(),
                    ActivityTraceFlags.None, Tracestate.Empty);

            var span = (Span)new SpanBuilder(SpanName, spanBuilderOptions)
                .SetNoParent()
                .SetParent(spanContext)
                .StartSpan();

            Assert.True(span.Context.IsValid);
            Assert.Equal(spanContext.TraceId, span.Context.TraceId);
            Assert.Equal(spanContext.SpanId, span.ParentSpanId);
        }

        [Fact]
        public void StartSpanLastParentWins2()
        {
            var spanContext =
                SpanContext.Create(
                    ActivityTraceId.CreateRandom(),
                    ActivitySpanId.CreateRandom(),
                    ActivityTraceFlags.None, Tracestate.Empty);

            var span = (Span)new SpanBuilder(SpanName, spanBuilderOptions)
                .SetParent(spanContext)
                .SetNoParent()
                .StartSpan();

            Assert.True(span.Context.IsValid);
            Assert.NotEqual(spanContext.TraceId, span.Context.TraceId);
            Assert.True(span.ParentSpanId == default);
        }

        [Fact]
        public void StartSpanLastParentWins3()
        {
            var spanContext =
                SpanContext.Create(
                    ActivityTraceId.CreateRandom(),
                    ActivitySpanId.CreateRandom(),
                    ActivityTraceFlags.None, Tracestate.Empty);
            var rootSpan = (Span)new SpanBuilder(SpanName, spanBuilderOptions)
                .StartSpan();

            var childSpan = (Span)new SpanBuilder(SpanName, spanBuilderOptions)
                .SetParent(spanContext)
                .SetParent(rootSpan)
                .StartSpan();

            Assert.True(childSpan.Context.IsValid);
            Assert.Equal(rootSpan.Context.TraceId, childSpan.Context.TraceId);
            Assert.Equal(rootSpan.Context.SpanId, childSpan.ParentSpanId);
        }

        [Fact]
        public void StartSpanLastParentWins4()
        {
            var spanContext =
                SpanContext.Create(
                    ActivityTraceId.CreateRandom(),
                    ActivitySpanId.CreateRandom(),
                    ActivityTraceFlags.None, Tracestate.Empty);
            var rootSpan = (Span)new SpanBuilder(SpanName, spanBuilderOptions)
                .StartSpan();

            var childSpan = (Span)new SpanBuilder(SpanName, spanBuilderOptions)
                .SetParent(rootSpan)
                .SetParent(spanContext)
                .StartSpan();

            Assert.True(childSpan.Context.IsValid);
            Assert.Equal(spanContext.TraceId, childSpan.Context.TraceId);
            Assert.Equal(spanContext.SpanId, childSpan.ParentSpanId);
        }

        [Fact]
        public void StartSpanNullParentWithRecordEvents()
        {
            var span = new SpanBuilder(SpanName, spanBuilderOptions)
                .SetSpanKind(SpanKind.Internal)
                .SetSampler(Samplers.NeverSample)
                .SetRecordEvents(true)
                .SetNoParent()
                .StartSpan();
            Assert.True(span.Context.IsValid);
            Assert.True(span.IsRecordingEvents);
            Assert.True((span.Context.TraceOptions & ActivityTraceFlags.Recorded) != 0);
            var spanData = ((Span)span).ToSpanData();
            Assert.True(spanData.ParentSpanId == default);
        }

        [Fact]
        public void StartSpanNullParentNoRecordOptions()
        {
            var span = new SpanBuilder(SpanName, spanBuilderOptions)
                .SetSpanKind(SpanKind.Internal)
                .SetSampler(Samplers.NeverSample)
                .SetNoParent()
                .StartSpan();
            Assert.True(span.Context.IsValid);
            Assert.False(span.IsRecordingEvents);
            Assert.True((span.Context.TraceOptions & ActivityTraceFlags.Recorded) == 0);
        }

        [Fact]
        public void StartChildSpan()
        {
            var rootSpan = new SpanBuilder(SpanName, spanBuilderOptions)
                .SetSpanKind(SpanKind.Internal)
                .SetNoParent()
                .StartSpan();
            Assert.True(rootSpan.Context.IsValid);
            Assert.True(rootSpan.IsRecordingEvents);
            Assert.True((rootSpan.Context.TraceOptions & ActivityTraceFlags.Recorded) != 0);

            var childSpan = new SpanBuilder(SpanName, spanBuilderOptions)
                .SetSpanKind(SpanKind.Internal)
                .SetParent(rootSpan)
                .StartSpan();

            Assert.True(childSpan.Context.IsValid);
            Assert.Equal(rootSpan.Context.TraceId, childSpan.Context.TraceId);
            Assert.Equal(rootSpan.Context.SpanId, ((Span)childSpan).ToSpanData().ParentSpanId);
            Assert.Equal(((Span)rootSpan).TimestampConverter, ((Span)childSpan).TimestampConverter);
        }

        [Fact]
        public void StartSpan_ExplicitNoParent()
        {
            var span = new SpanBuilder(SpanName, spanBuilderOptions)
                .SetSpanKind(SpanKind.Internal)
                .SetNoParent()
                .StartSpan();

            Assert.True(span.Context.IsValid);
            Assert.True(span.IsRecordingEvents);
            Assert.True((span.Context.TraceOptions & ActivityTraceFlags.Recorded) != 0);
            var spanData = ((Span)span).ToSpanData();
            Assert.True(spanData.ParentSpanId == default);
        }

        [Fact]
        public void StartSpan_NoParent()
        {
            var span = new SpanBuilder(SpanName, spanBuilderOptions)
                .SetSpanKind(SpanKind.Internal)
                .StartSpan();

            Assert.True(span.Context.IsValid);
            Assert.True(span.IsRecordingEvents);
            Assert.True((span.Context.TraceOptions & ActivityTraceFlags.Recorded) != 0);
            var spanData = ((Span)span).ToSpanData();
            Assert.True(spanData.ParentSpanId == default);
        }

        [Fact]
        public void StartSpan_CurrentSpanParent()
        {
            var rootSpan = new SpanBuilder(SpanName, spanBuilderOptions)
                .StartSpan();
            using (CurrentSpanUtils.WithSpan(rootSpan, true))
            {
                var childSpan = (Span)new SpanBuilder(SpanName, spanBuilderOptions)
                    .StartSpan();

                Assert.True(childSpan.Context.IsValid);
                Assert.Equal(rootSpan.Context.TraceId, childSpan.Context.TraceId);
                Assert.Equal(rootSpan.Context.SpanId, childSpan.ParentSpanId);
            }
        }

        [Fact]
        public void StartSpan_NoParentInScopeOfCurrentSpan()
        {
            var rootSpan = new SpanBuilder(SpanName, spanBuilderOptions)
                .StartSpan();
            using (CurrentSpanUtils.WithSpan(rootSpan, true))
            {
                var childSpan = (Span)new SpanBuilder(SpanName, spanBuilderOptions)
                    .SetNoParent()
                    .StartSpan();

                Assert.True(childSpan.Context.IsValid);
                Assert.NotEqual(rootSpan.Context.TraceId, childSpan.Context.TraceId);
                Assert.True(childSpan.ParentSpanId == default);
            }
        }

        [Fact]
        public void StartSpanInvalidParent()
        {
            var span = new SpanBuilder(SpanName, spanBuilderOptions)
                .SetSpanKind(SpanKind.Internal)
                .SetParent(SpanContext.Blank)
                .StartSpan();
            Assert.True(span.Context.IsValid);
            Assert.True(span.IsRecordingEvents);
            Assert.True((span.Context.TraceOptions & ActivityTraceFlags.Recorded) != 0);
            var spanData = ((Span)span).ToSpanData();
            Assert.True(spanData.ParentSpanId == default);
        }

        [Fact]
        public void StartRemoteSpan()
        {
            var spanContext =
                SpanContext.Create(
                    ActivityTraceId.CreateRandom(),
                    ActivitySpanId.CreateRandom(),
                    ActivityTraceFlags.None, Tracestate.Empty);

            var span = new SpanBuilder(SpanName, spanBuilderOptions)
                .SetSpanKind(SpanKind.Internal)
                .SetParent(spanContext)
                .SetRecordEvents(true)
                .StartSpan();

            Assert.True(span.Context.IsValid);
            Assert.Equal(spanContext.TraceId, span.Context.TraceId);
            Assert.True((span.Context.TraceOptions & ActivityTraceFlags.Recorded) != 0);
            var spanData = ((Span)span).ToSpanData();
            Assert.Equal(spanContext.SpanId, spanData.ParentSpanId);
        }

        [Fact]
        public void StartRootSpan_WithSpecifiedSampler()
        {
            // Apply given sampler before default sampler for root spans.
            var rootSpan = new SpanBuilder(SpanName, spanBuilderOptions)
                .SetSpanKind(SpanKind.Internal)
                .SetNoParent()
                .SetSampler(Samplers.NeverSample)
                .StartSpan();

            Assert.True(rootSpan.Context.IsValid);
            Assert.True((rootSpan.Context.TraceOptions & ActivityTraceFlags.Recorded) == 0);
        }

        [Fact]
        public void StartRootSpan_WithoutSpecifiedSampler()
        {
            // Apply default sampler (always true in the tests) for root spans.
            var rootSpan = new SpanBuilder(SpanName, spanBuilderOptions)
                .SetSpanKind(SpanKind.Internal)
                .SetNoParent()
                .StartSpan();

            Assert.True(rootSpan.Context.IsValid);
            Assert.True((rootSpan.Context.TraceOptions & ActivityTraceFlags.Recorded) != 0);
        }

        [Fact]
        public void StartRemoteChildSpan_WithSpecifiedSampler()
        {
            var rootSpan = new SpanBuilder(SpanName, spanBuilderOptions)
                .SetSpanKind(SpanKind.Internal)
                .SetSampler(Samplers.AlwaysSample)
                .SetNoParent()
                .StartSpan();

            Assert.True(rootSpan.Context.IsValid);
            Assert.True((rootSpan.Context.TraceOptions & ActivityTraceFlags.Recorded) != 0);
            // Apply given sampler before default sampler for spans with remote parent.
            var childSpan = new SpanBuilder(SpanName, spanBuilderOptions)
                .SetSpanKind(SpanKind.Internal)
                .SetSampler(Samplers.NeverSample)
                .SetParent(rootSpan.Context)
                .StartSpan();

            Assert.True(childSpan.Context.IsValid);
            Assert.Equal(rootSpan.Context.TraceId, childSpan.Context.TraceId);
            Assert.True((childSpan.Context.TraceOptions & ActivityTraceFlags.Recorded) == 0);
        }

        [Fact]
        public void StartRemoteChildSpan_WithoutSpecifiedSampler()
        {
            var rootSpan = new SpanBuilder(SpanName, spanBuilderOptions)
                .SetSpanKind(SpanKind.Internal)
                .SetSampler(Samplers.NeverSample)
                .SetNoParent()
                .StartSpan();

            Assert.True(rootSpan.Context.IsValid);
            Assert.True((rootSpan.Context.TraceOptions & ActivityTraceFlags.Recorded) == 0);
            // Apply default sampler (always true in the tests) for spans with remote parent.
            var childSpan = new SpanBuilder(SpanName, spanBuilderOptions)
                .SetSpanKind(SpanKind.Internal)
                .SetParent(rootSpan.Context)
                .StartSpan();

            Assert.True(childSpan.Context.IsValid);
            Assert.Equal(rootSpan.Context.TraceId, childSpan.Context.TraceId);
            Assert.True((childSpan.Context.TraceOptions & ActivityTraceFlags.Recorded) == 0);
        }

        [Fact]
        public void StartChildSpan_WithSpecifiedSampler()
        {
            var rootSpan = new SpanBuilder(SpanName, spanBuilderOptions)
                .SetSpanKind(SpanKind.Internal)
                .SetSampler(Samplers.AlwaysSample)
                .SetNoParent()
                .StartSpan();

            Assert.True(rootSpan.Context.IsValid);
            Assert.True((rootSpan.Context.TraceOptions & ActivityTraceFlags.Recorded) != 0);
            // Apply the given sampler for child spans.

            var childSpan = new SpanBuilder(SpanName, spanBuilderOptions)
                .SetSpanKind(SpanKind.Internal)
                .SetSampler(Samplers.NeverSample)
                .SetParent(rootSpan)
                .StartSpan();

            Assert.True(childSpan.Context.IsValid);
            Assert.Equal(rootSpan.Context.TraceId, childSpan.Context.TraceId);
            Assert.True((childSpan.Context.TraceOptions & ActivityTraceFlags.Recorded) == 0);
        }

        [Fact]
        public void StartChildSpan_WithoutSpecifiedSampler()
        {
            var rootSpan = new SpanBuilder(SpanName, spanBuilderOptions)
                .SetSpanKind(SpanKind.Internal)
                .SetSampler(Samplers.NeverSample)
                .SetNoParent()
                .StartSpan();

            Assert.True(rootSpan.Context.IsValid);
            Assert.True((rootSpan.Context.TraceOptions & ActivityTraceFlags.Recorded) == 0);

            // Don't apply the default sampler (always true) for child spans.
            var childSpan = new SpanBuilder(SpanName, spanBuilderOptions)
                .SetSpanKind(SpanKind.Internal)
                .SetParent(rootSpan)
                .StartSpan();

            Assert.True(childSpan.Context.IsValid);
            Assert.Equal(rootSpan.Context.TraceId, childSpan.Context.TraceId);
            Assert.True((childSpan.Context.TraceOptions & ActivityTraceFlags.Recorded) == 0);
        }

        [Fact]
        public void StartChildSpan_SampledLinkedParent()
        {
            var rootSpanUnsampled = new SpanBuilder(SpanName, spanBuilderOptions)
                .SetSpanKind(SpanKind.Internal)
                .SetSampler(Samplers.NeverSample)
                .SetNoParent()
                .StartSpan();

            Assert.True((rootSpanUnsampled.Context.TraceOptions & ActivityTraceFlags.Recorded) == 0);
            var rootSpanSampled =
                new SpanBuilder(SpanName, spanBuilderOptions)
                    .SetSpanKind(SpanKind.Internal)
                    .SetSampler(Samplers.AlwaysSample)
                    .SetNoParent()
                    .StartSpan();

            Assert.True((rootSpanSampled.Context.TraceOptions & ActivityTraceFlags.Recorded) != 0);
            // Sampled because the linked parent is sampled.
            var childSpan = new SpanBuilder(SpanName, spanBuilderOptions)
                .SetSpanKind(SpanKind.Internal)
                .AddLink(Link.FromSpanContext(rootSpanSampled.Context))
                .SetParent(rootSpanUnsampled)
                .StartSpan();

            Assert.True(childSpan.Context.IsValid);
            Assert.Equal(rootSpanUnsampled.Context.TraceId, childSpan.Context.TraceId);
            Assert.True((childSpan.Context.TraceOptions & ActivityTraceFlags.Recorded) != 0);
        }

        [Fact]
        public void StartRemoteChildSpan_WithProbabilitySamplerDefaultSampler()
        {
            var configMock = Mock.Get<ITraceConfig>(traceConfig);
            configMock.Setup((c) => c.ActiveTraceParams).Returns(TraceParams.Default);
            // This traceId will not be sampled by the ProbabilitySampler because the first 8 bytes as long
            // is not less than probability * Long.MAX_VALUE;
            var traceId =
                ActivityTraceId.CreateFromBytes(
                    new byte[] {0x8F, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0, 0, 0, 0, 0, 0, 0, 0,});

            // If parent is sampled then the remote child must be sampled.
            var childSpan =
                new SpanBuilder(SpanName, spanBuilderOptions)
                    .SetSpanKind(SpanKind.Internal)
                    .SetParent(SpanContext.Create(
                        traceId,
                        ActivitySpanId.CreateRandom(),
                        ActivityTraceFlags.Recorded,
                        Tracestate.Empty))
                    .StartSpan();

            Assert.True(childSpan.Context.IsValid);
            Assert.Equal(traceId, childSpan.Context.TraceId);
            Assert.True((childSpan.Context.TraceOptions & ActivityTraceFlags.Recorded) != 0);
            childSpan.End();

            Assert.Equal(TraceParams.Default, traceConfig.ActiveTraceParams);

            // If parent is not sampled then the remote child must be not sampled.
            childSpan =
                new SpanBuilder(SpanName, spanBuilderOptions)
                    .SetSpanKind(SpanKind.Internal)
                    .SetParent(SpanContext.Create(
                        traceId,
                        ActivitySpanId.CreateRandom(),
                        ActivityTraceFlags.None,
                        Tracestate.Empty))
                    .StartSpan();

            Assert.True(childSpan.Context.IsValid);
            Assert.Equal(traceId, childSpan.Context.TraceId);
            Assert.True((childSpan.Context.TraceOptions & ActivityTraceFlags.Recorded) == 0);
            childSpan.End();
        }

        [Fact]
        public void SpanBuilder_BadArguments()
        {
            var spanBuilder = new SpanBuilder(SpanName, spanBuilderOptions);

            Assert.Throws<ArgumentNullException>(() => spanBuilder.SetParent((ISpan)null));
            Assert.Throws<ArgumentNullException>(() => spanBuilder.SetParent((SpanContext)null));
            Assert.Throws<ArgumentNullException>(() => spanBuilder.SetSampler(null));
            Assert.Throws<ArgumentNullException>(() => spanBuilder.AddLink((ILink)null));
            Assert.Throws<ArgumentNullException>(() => spanBuilder.AddLink((SpanContext)null));
            Assert.Throws<ArgumentNullException>(() => spanBuilder.AddLink(null, null));
            Assert.Throws<ArgumentNullException>(() => spanBuilder.AddLink(SpanContext.Blank, null));
        }
    }
}

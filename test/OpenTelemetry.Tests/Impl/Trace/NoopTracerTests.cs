// <copyright file="NoopTracerTests.cs" company="OpenTelemetry Authors">
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

using OpenTelemetry.Resources;

namespace OpenTelemetry.Tests.Impl.Trace
{
    using System;
    using OpenTelemetry.Common;
    using OpenTelemetry.Context.Propagation;
    using OpenTelemetry.Trace;
    using Xunit;

    public class NoopTracerTests
    {
        [Fact]
        public void NoopTracer_CurrentSpan()
        {
            Assert.Same(BlankSpan.Instance, NoopTracer.Instance.CurrentSpan);
        }

        [Fact]
        public void NoopTracer_WithSpan()
        {
            Assert.Same(NoopScope.Instance, NoopTracer.Instance.WithSpan(BlankSpan.Instance));
        }

        [Fact]
        public void NoopTracer_SpanBuilder()
        {
            Assert.IsType<NoopSpanBuilder>(NoopTracer.Instance.SpanBuilder("foo"));
        }

        [Fact]
        public void NoopTracer_Formats()
        {
            Assert.NotNull(NoopTracer.Instance.TextFormat);
            Assert.NotNull(NoopTracer.Instance.BinaryFormat);
            Assert.IsAssignableFrom<ITextFormat>(NoopTracer.Instance.TextFormat);
            Assert.IsAssignableFrom<IBinaryFormat>(NoopTracer.Instance.BinaryFormat);
        }

        [Fact]
        public void NoopTracer_RecordData()
        {
            Assert.Throws<ArgumentNullException>(() => NoopTracer.Instance.RecordSpanData(null));

            // does not throw
            NoopTracer.Instance.RecordSpanData(SpanData.Create(SpanContext.Blank, null, Resource.Empty, "foo", Timestamp.Zero, null, null, null, null, Status.Ok, SpanKind.Internal, Timestamp.Zero));
        }
    }
}


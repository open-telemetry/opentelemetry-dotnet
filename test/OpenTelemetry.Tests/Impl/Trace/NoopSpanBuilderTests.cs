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

using System;

namespace OpenTelemetry.Tests.Impl.Trace
{
    using OpenTelemetry.Trace;
    using Xunit;

    public class NoopSpanBuilderTests
    {
        [Fact]
        public void NoopSpanBuilder_BadArguments()
        {
            Assert.Throws<ArgumentNullException>(() => new NoopSpanBuilder(null));

            var spanBuilder = new NoopSpanBuilder("foo");
            Assert.Throws<ArgumentNullException>(() => spanBuilder.SetParent((ISpan)null));
            Assert.Throws<ArgumentNullException>(() => spanBuilder.SetParent((SpanContext)null));
            Assert.Throws<ArgumentNullException>(() => spanBuilder.SetSampler(null));
            Assert.Throws<ArgumentNullException>(() => spanBuilder.AddLink((ILink)null));
            Assert.Throws<ArgumentNullException>(() => spanBuilder.AddLink((SpanContext)null));
            Assert.Throws<ArgumentNullException>(() => spanBuilder.AddLink(null, null));
            Assert.Throws<ArgumentNullException>(() => spanBuilder.AddLink(SpanContext.Blank, null));
        }

        [Fact]
        public void NoopSpanBuilder_Ok()
        {
            Assert.Same(BlankSpan.Instance, new NoopSpanBuilder("foo").StartSpan());
        }
    }
}

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
using System.Linq;
using System.Threading.Tasks;
using OpenTelemetry.Resources;
using OpenTelemetry.Testing.Export;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace.Configuration;
using OpenTelemetry.Trace.Export;
using OpenTelemetry.Trace.Samplers;
using OpenTelemetry.Utils;
using Xunit;

namespace OpenTelemetry.Trace.Test
{
    public class TracerTest
    {
        private const string SpanName = "MySpanName";
        private readonly SpanProcessor spanProcessor;
        private readonly TracerConfiguration tracerConfiguration;
        private readonly TracerSdk tracerSdk;
        private readonly TracerFactory tracerFactory;

        public TracerTest()
        {
            this.spanProcessor = new SimpleSpanProcessor(new TestExporter(null));
            this.tracerConfiguration = new TracerConfiguration();
            this.tracerFactory = TracerFactory.Create(b => b
                    .AddProcessorPipeline(p => p.AddProcessor(_ => this.spanProcessor)));
            this.tracerSdk = (TracerSdk)this.tracerFactory.GetTracer(null);
        }

        [Fact]
        public void BadConstructorArgumentsThrow()
        {
            var noopProc = new SimpleSpanProcessor(new TestExporter(null));
            Assert.Throws<ArgumentNullException>(() => new TracerSdk(null, new AlwaysOnSampler(), new TracerConfiguration(), Resource.Empty));
            Assert.Throws<ArgumentNullException>(() => new TracerSdk(noopProc, new AlwaysOnSampler(), null, Resource.Empty));
            Assert.Throws<ArgumentNullException>(() => new TracerSdk(noopProc, new AlwaysOnSampler(), new TracerConfiguration(), null));
        }

        [Fact]
        public void Tracer_StartRootSpan_BadArgs_NullSpanName()
        {
            var span1 = (SpanSdk)this.tracerSdk.StartRootSpan(null);
            Assert.Equal(string.Empty, span1.Name);

            var span2 = (SpanSdk)this.tracerSdk.StartRootSpan(null, SpanKind.Client);
            Assert.Equal(string.Empty, span2.Name);

            var span3 = (SpanSdk)this.tracerSdk.StartRootSpan(null, SpanKind.Client, null);
            Assert.Equal(string.Empty, span3.Name);
        }

        [Fact]
        public void Tracer_StartSpan_BadArgs_NullSpanName()
        {
            var span1 = (SpanSdk)this.tracerSdk.StartSpan(null);
            Assert.Equal(string.Empty, span1.Name);

            var span2 = (SpanSdk)this.tracerSdk.StartSpan(null, SpanKind.Client);
            Assert.Equal(string.Empty, span2.Name);

            var span3 = (SpanSdk)this.tracerSdk.StartSpan(null, SpanKind.Client, null);
            Assert.Equal(string.Empty, span3.Name);
        }

        [Fact]
        public void Tracer_StartSpan_FromParent_BadArgs_NullSpanName()
        {
            var span1 = (SpanSdk)this.tracerSdk.StartSpan(null, BlankSpan.Instance);
            Assert.Equal(string.Empty, span1.Name);

            var span2 = (SpanSdk)this.tracerSdk.StartSpan(null, BlankSpan.Instance, SpanKind.Client);
            Assert.Equal(string.Empty, span2.Name);

            var span3 = (SpanSdk)this.tracerSdk.StartSpan(null, BlankSpan.Instance, SpanKind.Client, null);
            Assert.Equal(string.Empty, span3.Name);
        }

        [Fact]
        public void Tracer_StartSpan_FromParentContext_BadArgs_NullSpanName()
        {
            var blankContext = default(SpanContext);
            var span1 = (SpanSdk)this.tracerSdk.StartSpan(null, blankContext);
            Assert.Equal(string.Empty, span1.Name);

            var span2 = (SpanSdk)this.tracerSdk.StartSpan(null, blankContext, SpanKind.Client);
            Assert.Equal(string.Empty, span2.Name);

            var span3 = (SpanSdk)this.tracerSdk.StartSpan(null, blankContext, SpanKind.Client, null);
            Assert.Equal(string.Empty, span3.Name);
        }

        [Fact]
        public void Tracer_StartSpan_FromActivity_BadArgs_NullSpanName()
        {
            var span = (SpanSdk)this.tracerSdk.StartSpanFromActivity(null, new Activity("foo").Start());
            Assert.Equal(string.Empty, span.Name);
        }

        [Fact]
        public void Tracer_StartSpan_FromActivity_BadArgs_NullActivity()
        {
            var span = (SpanSdk)this.tracerSdk.StartSpanFromActivity("foo", null);
            Assert.NotNull(span);
            Assert.Equal("foo", span.Name);
            Assert.Equal(default, span.ParentSpanId);
        }

        [Fact]
        public void GetCurrentSpanBlank()
        {
            Assert.False(this.tracerSdk.CurrentSpan.Context.IsValid);
        }

        [Fact]
        public void GetCurrentSpanBlankWontThrowOnEnd()
        {
            var current = this.tracerSdk.CurrentSpan;
            current.End();
        }

        [Fact]
        public void GetCurrentSpan()
        {
            var span = this.tracerSdk.StartSpan("foo");
            using (this.tracerSdk.WithSpan(span))
            {
                Assert.Same(span, this.tracerSdk.CurrentSpan);
            }

            Assert.False(this.tracerSdk.CurrentSpan.Context.IsValid);
        }

        [Fact]
        public void CreateSpan_Sampled()
        {
            var span = this.tracerSdk.StartSpan("foo");
            Assert.True(span.IsRecording);
        }

        [Fact]
        public void CreateSpan_NotSampled()
        {
            var tracer = TracerFactory.Create(b => b
                    .SetSampler(new AlwaysOffSampler())
                    .AddProcessorPipeline(p => p.AddProcessor(n => this.spanProcessor)))
                .GetTracer(null);

            var span = tracer.StartSpan("foo");
            Assert.False(span.IsRecording);
        }

        [Fact]
        public void CreateSpan_ByTracerWithResource()
        {
            var tracer = (TracerSdk)this.tracerFactory.GetTracer("foo", "semver:1.0.0");
            var span = (SpanSdk)tracer.StartSpan("some span");
            Assert.Equal(tracer.LibraryResource, span.LibraryResource);
        }

        [Fact]
        public void WithSpanNull()
        {
            Assert.NotNull(this.tracerSdk.WithSpan(null));
            Assert.False(this.tracerSdk.CurrentSpan.Context.IsValid);

            using (this.tracerSdk.StartActiveSpan("some span", out var span))
            {
                this.tracerSdk.WithSpan(null);
                Assert.Equal(span, this.tracerSdk.CurrentSpan);
            }
        }

        [Fact]
        public void DroppingAndAddingAttributes()
        {
            var maxNumberOfAttributes = 8;
            var traceConfig = new TracerConfiguration(maxNumberOfAttributes, 128, 32);
            var tracer = TracerFactory.Create(b => b
                    .AddProcessorPipeline(p => p.AddProcessor(n => this.spanProcessor))
                    .SetTracerOptions(traceConfig)
                    .SetSampler(new AlwaysOnSampler()))
                .GetTracer(null);

            var span = (SpanSdk)tracer.StartRootSpan(SpanName);

            for (long i = 0; i < 2 * maxNumberOfAttributes; i++)
            {
                span.SetAttribute("MyStringAttributeKey" + i, i);
            }

            Assert.Equal(maxNumberOfAttributes, span.Attributes.Count());
            for (long i = 0; i < maxNumberOfAttributes; i++)
            {
                Assert.Equal(
                    i + maxNumberOfAttributes,
                    span
                        .Attributes
                        .GetValue("MyStringAttributeKey" + (i + maxNumberOfAttributes)));
            }

            for (long i = 0; i < maxNumberOfAttributes / 2; i++)
            {
                span.SetAttribute("MyStringAttributeKey" + i, i);
            }

            Assert.Equal(maxNumberOfAttributes, span.Attributes.Count());

            // Test that we still have in the attributes map the latest maxNumberOfAttributes / 2 entries.
            for (long i = 0; i < maxNumberOfAttributes / 2; i++)
            {
                Assert.Equal(
                    i + (maxNumberOfAttributes * 3 / 2),
                    span
                        .Attributes
                        .GetValue("MyStringAttributeKey" + (i + (maxNumberOfAttributes * 3 / 2))));
            }

            // Test that we have the newest re-added initial entries.
            for (long i = 0; i < maxNumberOfAttributes / 2; i++)
            {
                Assert.Equal(
                    i,
                    span.Attributes.GetValue("MyStringAttributeKey" + i));
            }
        }

        [Fact]
        public async Task DroppingEvents()
        {
            var maxNumberOfEvents = 8;
            var traceConfig = new TracerConfiguration(32, maxNumberOfEvents, 32);
            var tracer = TracerFactory.Create(b => b
                    .AddProcessorPipeline(p => p.AddProcessor(n => this.spanProcessor))
                    .SetTracerOptions(traceConfig)
                    .SetSampler(new AlwaysOnSampler()))
                .GetTracer(null);

            var span = (SpanSdk)tracer.StartRootSpan(SpanName);

            var eventTimestamps = new DateTimeOffset[2 * maxNumberOfEvents];

            for (int i = 0; i < 2 * maxNumberOfEvents; i++)
            {
                eventTimestamps[i] = PreciseTimestamp.GetUtcNow();
                span.AddEvent(new Event("foo", eventTimestamps[i]));
                await Task.Delay(10);
            }

            Assert.Equal(maxNumberOfEvents, span.Events.Count());

            var events = span.Events.ToArray();

            for (int i = 0; i < maxNumberOfEvents; i++)
            {
                Assert.Equal(eventTimestamps[i + maxNumberOfEvents], events[i].Timestamp);
            }

            span.End();

            Assert.Equal(maxNumberOfEvents, span.Events.Count());
        }

        [Fact]
        public void DroppingLinksFactory()
        {
            var contextLink = new SpanContext(
                ActivityTraceId.CreateRandom(),
                ActivitySpanId.CreateRandom(),
                ActivityTraceFlags.None);

            var maxNumberOfLinks = 8;
            var traceConfig = new TracerConfiguration(32, 128, maxNumberOfLinks);
            var tracer = TracerFactory.Create(b => b
                    .AddProcessorPipeline(p => p.AddProcessor(n => this.spanProcessor))
                    .SetTracerOptions(traceConfig)
                    .SetSampler(new AlwaysOnSampler()))
                .GetTracer(null);

            var overflowedLinks = new List<Link>();
            var link = new Link(contextLink);
            for (var i = 0; i < 2 * maxNumberOfLinks; i++)
            {
                overflowedLinks.Add(link);
            }

            var span = (SpanSdk)tracer.StartSpan(SpanName, SpanKind.Client, new SpanCreationOptions
            {
                LinksFactory = () => overflowedLinks,
            });

            Assert.Equal(maxNumberOfLinks, span.Links.Count());
            foreach (var actualLink in span.Links)
            {
                Assert.Equal(link, actualLink);
            }

            span.End();

            Assert.Equal(maxNumberOfLinks, span.Links.Count());
            foreach (var actualLink in span.Links)
            {
                Assert.Equal(link, actualLink);
            }
        }

        [Fact]
        public void DroppingLinksEnumerable()
        {
            var contextLink = new SpanContext(
                ActivityTraceId.CreateRandom(),
                ActivitySpanId.CreateRandom(),
                ActivityTraceFlags.None);

            var maxNumberOfLinks = 8;
            var traceConfig = new TracerConfiguration(32, 128, maxNumberOfLinks);
            var tracer = TracerFactory.Create(b => b
                    .AddProcessorPipeline(p => p.AddProcessor(n => this.spanProcessor))
                    .SetTracerOptions(traceConfig)
                    .SetSampler(new AlwaysOnSampler()))
                .GetTracer(null);

            var overflowedLinks = new List<Link>();
            var link = new Link(contextLink);
            for (var i = 0; i < 2 * maxNumberOfLinks; i++)
            {
                overflowedLinks.Add(link);
            }

            var span = (SpanSdk)tracer.StartSpan(SpanName, SpanKind.Client, new SpanCreationOptions
            {
                Links = overflowedLinks,
            });

            Assert.Equal(maxNumberOfLinks, span.Links.Count());
            foreach (var actualLink in span.Links)
            {
                Assert.Equal(link, actualLink);
            }

            span.End();

            Assert.Equal(maxNumberOfLinks, span.Links.Count());
            foreach (var actualLink in span.Links)
            {
                Assert.Equal(link, actualLink);
            }
        }

        [Fact]
        public void DroppingAttributes()
        {
            var maxNumberOfAttributes = 8;
            var traceConfig = new TracerConfiguration(maxNumberOfAttributes, 128, 32);
            var tracer = TracerFactory.Create(b => b
                    .AddProcessorPipeline(p => p.AddProcessor(_ => this.spanProcessor))
                    .SetTracerOptions(traceConfig)
                    .SetSampler(new AlwaysOnSampler()))
                .GetTracer(null);

            var span = (SpanSdk)tracer.StartRootSpan(SpanName);
            for (long i = 0; i < 2 * maxNumberOfAttributes; i++)
            {
                span.SetAttribute("MyStringAttributeKey" + i, i);
            }

            Assert.Equal(maxNumberOfAttributes, span.Attributes.Count());
            for (long i = 0; i < maxNumberOfAttributes; i++)
            {
                Assert.Equal(
                    i + maxNumberOfAttributes,
                    span
                        .Attributes
                        .GetValue("MyStringAttributeKey" + (i + maxNumberOfAttributes)));
            }

            span.End();

            Assert.Equal(maxNumberOfAttributes, span.Attributes.Count());
            for (long i = 0; i < maxNumberOfAttributes; i++)
            {
                Assert.Equal(
                    i + maxNumberOfAttributes,
                    span
                        .Attributes
                        .GetValue("MyStringAttributeKey" + (i + maxNumberOfAttributes)));
            }
        }

        [Fact]
        public void Tracer_StartSpanFromActivity_ValidArguments_ReturnsSpanWithActivityAttributesAsSpanTags()
        {
            var activity = new Activity("foo")
                .SetStartTime(DateTime.UtcNow)
                .SetIdFormat(ActivityIdFormat.W3C)
                .Start();

            activity.AddTag("key1", "value1");
            activity.AddTag("key2", "value2");

            var span = (SpanSdk)this.tracerSdk.StartSpanFromActivity("foo", activity);

            Assert.NotNull(span);

            Assert.Single(span.Attributes.Where(k => k.Key == "key1" && (string)k.Value == "value1"));
            Assert.Single(span.Attributes.Where(k => k.Key == "key2" && (string)k.Value == "value2"));
        }
    }
}

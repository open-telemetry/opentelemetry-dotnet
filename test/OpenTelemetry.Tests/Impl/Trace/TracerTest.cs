// <copyright file="TracerTest.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Resources;
using OpenTelemetry.Tests;
using OpenTelemetry.Utils;
using OpenTelemetry.Trace.Sampler;
using Xunit;
using System;
using System.Collections.Generic;
using OpenTelemetry.Testing.Export;
using OpenTelemetry.Trace.Configuration;
using OpenTelemetry.Trace.Export;

namespace OpenTelemetry.Trace.Test
{
    public class TracerTest
    {
        private const string SpanName = "MySpanName";
        private readonly SpanProcessor spanProcessor;
        private readonly TracerConfiguration tracerConfiguration;
        private readonly Tracer tracer;
        private readonly TracerFactory tracerFactory;

        public TracerTest()
        {
            spanProcessor = new SimpleSpanProcessor(new TestExporter(null));
            tracerConfiguration = new TracerConfiguration();
            tracerFactory = TracerFactory.Create(b => b
                    .AddProcessorPipeline(p => p.AddProcessor(_ => spanProcessor)));
            tracer = (Tracer)tracerFactory.GetTracer(null);
        }

        [Fact]
        public void BadConstructorArgumentsThrow()
        {
            var noopProc = new SimpleSpanProcessor(new TestExporter(null));
            Assert.Throws<ArgumentNullException>(() => new Tracer(null, Samplers.AlwaysSample, new TracerConfiguration(), new BinaryFormat(), new TraceContextFormat(), Resource.Empty));

            Assert.Throws<ArgumentNullException>(() => new Tracer(noopProc, Samplers.AlwaysSample, null, new BinaryFormat(), new TraceContextFormat(), Resource.Empty));

            Assert.Throws<ArgumentNullException>(() => new Tracer(noopProc, Samplers.AlwaysSample, new TracerConfiguration(), null, new TraceContextFormat(), Resource.Empty));
            Assert.Throws<ArgumentNullException>(() => new Tracer(noopProc, Samplers.AlwaysSample, new TracerConfiguration(), new BinaryFormat(), null, Resource.Empty));

            Assert.Throws<ArgumentNullException>(() => new Tracer(noopProc, Samplers.AlwaysSample, new TracerConfiguration(), new BinaryFormat(), new TraceContextFormat(), null));
        }

        [Fact]
        public void Tracer_CreateSpan_BadArgs()
        {
            Assert.Throws<ArgumentNullException>(() => tracer.StartRootSpan(null));
            Assert.Throws<ArgumentNullException>(() => tracer.StartRootSpan(null, SpanKind.Client));
            Assert.Throws<ArgumentNullException>(() => tracer.StartRootSpan(null, SpanKind.Client, null));

            Assert.Throws<ArgumentNullException>(() => tracer.StartSpan(null));
            Assert.Throws<ArgumentNullException>(() => tracer.StartSpan(null, SpanKind.Client));
            Assert.Throws<ArgumentNullException>(() => tracer.StartSpan(null, SpanKind.Client, null));

            Assert.Throws<ArgumentNullException>(() => tracer.StartSpan(null, BlankSpan.Instance));
            Assert.Throws<ArgumentNullException>(() => tracer.StartSpan(null, BlankSpan.Instance, SpanKind.Client));
            Assert.Throws<ArgumentNullException>(() => tracer.StartSpan(null, BlankSpan.Instance, SpanKind.Client, null));

            Assert.Throws<ArgumentNullException>(() => tracer.StartSpan(null, SpanContext.BlankLocal));
            Assert.Throws<ArgumentNullException>(() => tracer.StartSpan(null, SpanContext.BlankLocal, SpanKind.Client));
            Assert.Throws<ArgumentNullException>(() => tracer.StartSpan(null, SpanContext.BlankLocal, SpanKind.Client, null));

            Assert.Throws<ArgumentNullException>(() => tracer.StartSpanFromActivity(null, new Activity("foo").Start()));

            Assert.Throws<ArgumentNullException>(() => tracer.StartSpanFromActivity("foo", null));

            Assert.Throws<ArgumentException>(() => tracer.StartSpanFromActivity("foo", new Activity("foo")));

            Assert.Throws<ArgumentException>(() => tracer.StartSpanFromActivity(
                    "foo",
                    new Activity("foo").SetIdFormat(ActivityIdFormat.Hierarchical).Start()));
        }

        [Fact]
        public void GetCurrentSpanBlank()
        {
            Assert.Same(BlankSpan.Instance, tracer.CurrentSpan);
        }

        [Fact]
        public void GetCurrentSpan()
        {
            var span = tracer.StartSpan("foo");
            using (tracer.WithSpan(span))
            {
                Assert.Same(span, tracer.CurrentSpan);
            }
            Assert.Same(BlankSpan.Instance, tracer.CurrentSpan);
        }

        [Fact]
        public void CreateSpan_Sampled()
        {
            var span = tracer.StartSpan("foo");
            Assert.True(span.IsRecording);
        }

        [Fact]
        public void CreateSpan_NotSampled()
        {
            var tracer = TracerFactory.Create(b => b
                    .SetSampler(Samplers.NeverSample)
                    .AddProcessorPipeline(p => p.AddProcessor(n => spanProcessor)))
                .GetTracer(null);

            var span = tracer.StartSpan("foo");
            Assert.False(span.IsRecording);
        }

        [Fact]
        public void CreateSpan_ByTracerWithResource()
        {

            var tracer = (Tracer)tracerFactory.GetTracer("foo", "semver:1.0.0");
            var span = (Span)tracer.StartSpan("some span");
            Assert.Equal(tracer.LibraryResource, span.LibraryResource);
        }

        [Fact]
        public void WithSpanNull()
        {
            Assert.Throws<ArgumentNullException>(() => tracer.WithSpan(null));
        }

        [Fact]
        public void GetTextFormat()
        {
            Assert.NotNull(tracer.TextFormat);
        }

        [Fact]
        public void GetBinaryFormat()
        {
            Assert.NotNull(tracer.BinaryFormat);
        }

        [Fact]
        public void DroppingAndAddingAttributes()
        {
            var maxNumberOfAttributes = 8;
            var traceConfig = new TracerConfiguration(maxNumberOfAttributes, 128, 32);
            var tracer = TracerFactory.Create(b => b
                    .AddProcessorPipeline(p => p.AddProcessor(n => spanProcessor))
                    .SetTracerOptions(traceConfig)
                    .SetSampler(Samplers.AlwaysSample))
                .GetTracer(null);

            var span = (Span)tracer.StartRootSpan(SpanName);

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
                    i + maxNumberOfAttributes * 3 / 2,
                    span
                        .Attributes
                        .GetValue("MyStringAttributeKey" + (i + maxNumberOfAttributes * 3 / 2)));
            }

            // Test that we have the newest re-added initial entries.
            for (long i = 0; i < maxNumberOfAttributes / 2; i++)
            {
                Assert.Equal(i,
                    span.Attributes.GetValue("MyStringAttributeKey" + i));
            }
        }

        [Fact]
        public async Task DroppingEvents()
        {
            var maxNumberOfEvents = 8;
            var traceConfig = new TracerConfiguration(32, maxNumberOfEvents, 32);
            var tracer = TracerFactory.Create(b => b
                    .AddProcessorPipeline(p => p.AddProcessor(n => spanProcessor))
                    .SetTracerOptions(traceConfig)
                    .SetSampler(Samplers.AlwaysSample))
                .GetTracer(null);

            var span = (Span)tracer.StartRootSpan(SpanName);

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
            var contextLink = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(),
                ActivityTraceFlags.None);

            var maxNumberOfLinks = 8;
            var traceConfig = new TracerConfiguration(32, 128, maxNumberOfLinks);
            var tracer = TracerFactory.Create(b => b
                    .AddProcessorPipeline(p => p.AddProcessor(n => spanProcessor))
                    .SetTracerOptions(traceConfig)
                    .SetSampler(Samplers.AlwaysSample))
                .GetTracer(null);

            var overflowedLinks = new List<Link>();
            var link = new Link(contextLink);
            for (var i = 0; i < 2 * maxNumberOfLinks; i++)
            {
                overflowedLinks.Add(link);
            }

            var span = (Span)tracer.StartSpan(SpanName, SpanKind.Client, new SpanCreationOptions
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
            var contextLink = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(),
                ActivityTraceFlags.None);

            var maxNumberOfLinks = 8;
            var traceConfig = new TracerConfiguration(32, 128, maxNumberOfLinks);
            var tracer = TracerFactory.Create(b => b
                    .AddProcessorPipeline(p => p.AddProcessor(n => spanProcessor))
                    .SetTracerOptions(traceConfig)
                    .SetSampler(Samplers.AlwaysSample))
                .GetTracer(null);

            var overflowedLinks = new List<Link>();
            var link = new Link(contextLink);
            for (var i = 0; i < 2 * maxNumberOfLinks; i++)
            {
                overflowedLinks.Add(link);
            }

            var span = (Span)tracer.StartSpan(SpanName, SpanKind.Client, new SpanCreationOptions
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
                    .SetSampler(Samplers.AlwaysSample))
                .GetTracer(null);

            var span = (Span)tracer.StartRootSpan(SpanName);
            for (var i = 0; i < 2 * maxNumberOfAttributes; i++)
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
    }
}

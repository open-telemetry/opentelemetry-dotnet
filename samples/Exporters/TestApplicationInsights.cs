// <copyright file="TestApplicationInsights.cs" company="OpenTelemetry Authors">
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

namespace Samples
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using Microsoft.ApplicationInsights.Extensibility;
    using OpenTelemetry.Exporter.ApplicationInsights;
    using OpenTelemetry.Internal;
    using OpenTelemetry.Stats;
    using OpenTelemetry.Stats.Aggregations;
    using OpenTelemetry.Stats.Measures;
    using OpenTelemetry.Tags;
    using OpenTelemetry.Trace;
    using OpenTelemetry.Trace.Export;
    using OpenTelemetry.Trace.Sampler;

    internal class TestApplicationInsights
    {
        private static readonly ITracer Tracer = Tracing.Tracer;
        private static readonly ITagger Tagger = Tags.Tagger;

        private static readonly IStatsRecorder StatsRecorder = Stats.StatsRecorder;
        private static readonly IMeasureLong VideoSize = MeasureLong.Create("my.org/measure/video_size", "size of processed videos", "By");
        private static readonly TagKey FrontendKey = TagKey.Create("my.org/keys/frontend");

        private static readonly long MiB = 1 << 20;

        private static readonly IViewName VideoSizeViewName = ViewName.Create("my.org/views/video_size");

        private static readonly IView VideoSizeView = View.Create(
            VideoSizeViewName,
            "processed video size over time",
            VideoSize,
            Distribution.Create(BucketBoundaries.Create(new List<double>() { 0.0, 16.0 * MiB, 256.0 * MiB })),
            new List<TagKey>() { FrontendKey });

        internal static object Run()
        {
            SimpleEventQueue eventQueue = new SimpleEventQueue();
            ExportComponent exportComponent = ExportComponent.CreateWithInProcessStores(eventQueue);

            TelemetryConfiguration.Active.InstrumentationKey = "instrumentation-key";
            var exporter = new ApplicationInsightsExporter(exportComponent, Stats.ViewManager, TelemetryConfiguration.Active);
            exporter.Start();

            var tagContextBuilder = Tagger.CurrentBuilder.Put(FrontendKey, TagValue.Create("mobile-ios9.3.5"));

            var spanBuilder = Tracer
                .SpanBuilder("incoming request")
                .SetRecordEvents(true)
                .SetSampler(Samplers.AlwaysSample);

            Stats.ViewManager.RegisterView(VideoSizeView);

            using (tagContextBuilder.BuildScoped())
            {
                using (Tracer.WithSpan(spanBuilder.StartSpan()))
                {
                    Tracer.CurrentSpan.AddEvent("Start processing video.");
                    Thread.Sleep(TimeSpan.FromMilliseconds(10));
                    StatsRecorder.NewMeasureMap().Put(VideoSize, 25 * MiB).Record();
                    Tracer.CurrentSpan.AddEvent("Finished processing video.");
                }
            }

            Thread.Sleep(TimeSpan.FromMilliseconds(5100));

            var viewData = Stats.ViewManager.GetView(VideoSizeViewName);

            Console.WriteLine(viewData);

            Console.WriteLine("Done... wait for events to arrive to backend!");
            Console.ReadLine();

            exportComponent.SpanExporter.Dispose();

            return null;
        }
    }
}

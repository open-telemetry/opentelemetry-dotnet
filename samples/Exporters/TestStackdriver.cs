// <copyright file="TestStackdriver.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Threading;
using OpenTelemetry.Exporter.Stackdriver;
using OpenTelemetry.Stats;
using OpenTelemetry.Stats.Aggregations;
using OpenTelemetry.Stats.Measures;
using OpenTelemetry.Tags;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Configuration;

namespace Samples
{
    internal class TestStackdriver
    {
        private static readonly ITagger Tagger = Tags.Tagger;

        private static readonly IStatsRecorder StatsRecorder = Stats.StatsRecorder;
        private static readonly IMeasureDouble VideoSize = MeasureDouble.Create("my_org/measure/video_size", "size of processed videos", "MiB");
        private static readonly TagKey FrontendKey = TagKey.Create("my_org/keys/frontend");

        private static readonly long MiB = 1 << 20;

        private static readonly IViewName VideoSizeViewName = ViewName.Create("my_org/views/video_size");

        private static readonly IView VideoSizeView = View.Create(
            name: VideoSizeViewName,
            description: "processed video size over time",
            measure: VideoSize,
            aggregation: Sum.Create(),
            columns: new List<TagKey>() { FrontendKey });

        internal static object Run(string projectId)
        {
            var spanExporter = new StackdriverTraceExporter(projectId);

            var metricExporter = new StackdriverMetricExporter(
                projectId,
                Stats.ViewManager);
            metricExporter.Start();

            using (var tracerFactory = TracerFactory.Create(builder => builder.SetExporter(spanExporter)))
            {
                var tracer = tracerFactory.GetTracer("stackdriver-test");

                var tagContextBuilder = Tagger.CurrentBuilder.Put(FrontendKey, TagValue.Create("mobile-ios9.3.5"));

                Stats.ViewManager.RegisterView(VideoSizeView);

                using (tagContextBuilder.BuildScoped())
                {
                    using (tracer.WithSpan(tracer.StartSpan("incoming request")))
                    {
                        tracer.CurrentSpan.AddEvent("Processing video.");
                        Thread.Sleep(TimeSpan.FromMilliseconds(10));

                        StatsRecorder.NewMeasureMap()
                            .Put(VideoSize, 25 * MiB)
                            .Record();
                    }
                }

                Thread.Sleep(TimeSpan.FromMilliseconds(5100));

                var viewData = Stats.ViewManager.GetView(VideoSizeViewName);

                Console.WriteLine(viewData);

                Console.WriteLine("Done... wait for events to arrive to backend!");
                Console.ReadLine();

                metricExporter.Stop();
                return null;
            }
        }
    }
}

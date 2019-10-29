// <copyright file="TestPrometheus.cs" company="OpenTelemetry Authors">
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
using System.Threading.Tasks;
using OpenTelemetry.Exporter.Prometheus;
using OpenTelemetry.Stats;
using OpenTelemetry.Stats.Aggregations;
using OpenTelemetry.Stats.Measures;
using OpenTelemetry.Tags;

namespace Samples
{
    internal class TestPrometheus
    {
        private static readonly ITagger Tagger = Tags.Tagger;

        private static readonly IStatsRecorder StatsRecorder = Stats.StatsRecorder;
        private static readonly IMeasureLong VideoSize = MeasureLong.Create("my.org/measure/video_size", "size of processed videos", "By");
        private static readonly string FrontendKey = "my.org/keys/frontend";

        private static readonly long MiB = 1 << 20;

        private static readonly IViewName VideoSizeViewName = ViewName.Create("my.org/views/video_size");

        private static readonly IView VideoSizeView = View.Create(
            VideoSizeViewName,
            "processed video size over time",
            VideoSize,
            Distribution.Create(BucketBoundaries.Create(new List<double>() { 0.0, 16.0 * MiB, 256.0 * MiB })),
            new List<string>() { FrontendKey });

        internal static object Run()
        {
            var exporter = new PrometheusExporter(
                new PrometheusExporterOptions()
                {
                    Url = "http://+:9184/metrics/",  // "+" is a wildcard used to listen to all hostnames
                },
                Stats.ViewManager);

            exporter.Start();

            try
            {
                var tagContextBuilder = Tagger.CurrentBuilder.Put(FrontendKey, "mobile-ios9.3.5");

                Stats.ViewManager.RegisterView(VideoSizeView);

                var t = new Task(() =>
                {
                    var r = new Random();
                    var values = new byte[1];

                    while (true)
                    {
                        using (var scopedTags = tagContextBuilder.BuildScoped())
                        {
                            r.NextBytes(values);
                            StatsRecorder.NewMeasureMap().Put(VideoSize, values[0] * MiB).Record();
                            Thread.Sleep(TimeSpan.FromSeconds(1));
                        }
                    }
                });
                t.Start();

                Console.WriteLine("Look at metrics in Prometetheus console!");
                Console.ReadLine();
            }
            finally
            {
                exporter.Stop();
            }

            return null;
        }
    }
}

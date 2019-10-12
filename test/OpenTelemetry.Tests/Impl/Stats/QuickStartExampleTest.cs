﻿// <copyright file="QuickStartExampleTest.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Linq;
using OpenTelemetry.Stats.Aggregations;
using OpenTelemetry.Stats.Measures;
using OpenTelemetry.Tags;
using Xunit;
using Xunit.Abstractions;

namespace OpenTelemetry.Stats.Test
{
    public class QuickStartExampleTest
    {
        readonly ITestOutputHelper output;

        public QuickStartExampleTest(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void Main()
        {
            var viewManager = Stats.ViewManager;
            var statsRecorder = Stats.StatsRecorder;

            var state = new CurrentTaggingState();
            var tagger = new Tagger(state);

            TagKey FRONTEND_KEY = TagKey.Create("my.org/keys/frontend");
            TagKey FRONTEND_OS_KEY = TagKey.Create("my.org/keys/frontend/os");
            TagKey FRONTEND_OS_VERSION_KEY = TagKey.Create("my.org/keys/frontend/os/version");

            IMeasureLong VIDEO_SIZE = MeasureLong.Create("my.org/measure/video_size", "size of processed videos", "MBy");

            IViewName VIDEO_SIZE_BY_FRONTEND_VIEW_NAME = ViewName.Create("my.org/views/video_size_byfrontend/main1");
            IView VIDEO_SIZE_BY_FRONTEND_VIEW = View.Create(
                                        VIDEO_SIZE_BY_FRONTEND_VIEW_NAME,
                                        "processed video size over time",
                                        VIDEO_SIZE,
                                        Distribution.Create(BucketBoundaries.Create(new List<double>() { 0.0, 256.0, 65536.0 })),
                                        new List<TagKey>() { FRONTEND_KEY});

            IViewName VIDEO_SIZE_ALL_VIEW_NAME = ViewName.Create("my.org/views/video_size_all");
            IView VIDEO_SIZE_VIEW_ALL = View.Create(
                            VIDEO_SIZE_ALL_VIEW_NAME,
                            "processed video size over time",
                            VIDEO_SIZE,
                            Distribution.Create(BucketBoundaries.Create(new List<double>() { 0.0, 256.0, 65536.0 })),
                            new List<TagKey>() { });


            IViewName VIDEO_SIZE_TOTAL_VIEW_NAME = ViewName.Create("my.org/views/video_size_total");
            IView VIDEO_SIZE_TOTAL = View.Create(
                                  VIDEO_SIZE_TOTAL_VIEW_NAME,
                                  "total video size over time",
                                  VIDEO_SIZE,
                                  Sum.Create(),
                                  new List<TagKey>() { FRONTEND_KEY});

            IViewName VIDEOS_PROCESSED_VIEW_NAME = ViewName.Create("my.org/views/videos_processed");
            IView VIDEOS_PROCESSED = View.Create(
                                  VIDEOS_PROCESSED_VIEW_NAME,
                                  "total video processed",
                                  VIDEO_SIZE,
                                  Count.Create(),
                                  new List<TagKey>() { FRONTEND_KEY });

            viewManager.RegisterView(VIDEO_SIZE_VIEW_ALL);
            viewManager.RegisterView(VIDEO_SIZE_BY_FRONTEND_VIEW);
            viewManager.RegisterView(VIDEO_SIZE_TOTAL);
            viewManager.RegisterView(VIDEOS_PROCESSED);

            ITagContext context1 = tagger
                .EmptyBuilder
                .Put(FRONTEND_KEY, TagValue.Create("front1"))
                .Build();
            ITagContext context2 = tagger
                .EmptyBuilder
                .Put(FRONTEND_KEY, TagValue.Create("front2"))
                .Build();

            long sum = 0;
            for (int i = 0; i < 10; i++)
            {
                sum = sum + (25648 * i);
                if (i % 2 == 0)
                {
                    statsRecorder.NewMeasureMap().Put(VIDEO_SIZE, 25648 * i).Record(context1);
                } else
                {
                    statsRecorder.NewMeasureMap().Put(VIDEO_SIZE, 25648 * i).Record(context2);
                }
            }

            IViewData viewDataByFrontend = viewManager.GetView(VIDEO_SIZE_BY_FRONTEND_VIEW_NAME);
            var viewDataAggMap = viewDataByFrontend.AggregationMap.ToList();
            output.WriteLine(viewDataByFrontend.ToString());

            IViewData viewDataAll = viewManager.GetView(VIDEO_SIZE_ALL_VIEW_NAME);
            var viewDataAggMapAll = viewDataAll.AggregationMap.ToList();
            output.WriteLine(viewDataAll.ToString());

            IViewData viewData1 = viewManager.GetView(VIDEO_SIZE_TOTAL_VIEW_NAME);
            var viewData1AggMap = viewData1.AggregationMap.ToList();
            output.WriteLine(viewData1.ToString());

            IViewData viewData2 = viewManager.GetView(VIDEOS_PROCESSED_VIEW_NAME);
            var viewData2AggMap = viewData2.AggregationMap.ToList();
            output.WriteLine(viewData2.ToString());

            output.WriteLine(sum.ToString());
        }

        [Fact]
        public void Main2()
        {
            var viewManager = Stats.ViewManager;
            var statsRecorder = Stats.StatsRecorder;

            var state = new CurrentTaggingState();
            var tagger = new Tagger(state);

            TagKey FRONTEND_KEY = TagKey.Create("my.org/keys/frontend");
            TagKey FRONTEND_OS_KEY = TagKey.Create("my.org/keys/frontend/os");
            TagKey FRONTEND_OS_VERSION_KEY = TagKey.Create("my.org/keys/frontend/os/version");

            IMeasureLong VIDEO_SIZE = MeasureLong.Create("my.org/measure/video_size", "size of processed videos", "MBy");

            IViewName VIDEO_SIZE_VIEW_NAME = ViewName.Create("my.org/views/video_size_byfrontend/main2");
            IView VIDEO_SIZE_VIEW = View.Create(
                                        VIDEO_SIZE_VIEW_NAME,
                                        "processed video size over time",
                                        VIDEO_SIZE,
                                        Distribution.Create(BucketBoundaries.Create(new List<double>() { 0.0, 256.0, 65536.0 })),
                                        new List<TagKey>() { FRONTEND_KEY, FRONTEND_OS_KEY, FRONTEND_OS_VERSION_KEY });

            viewManager.RegisterView(VIDEO_SIZE_VIEW);

            ITagContext context1 = tagger
                .EmptyBuilder
                .Put(FRONTEND_KEY, TagValue.Create("front1"))
                .Put(FRONTEND_OS_KEY, TagValue.Create("windows"))
                .Build();
            ITagContext context2 = tagger
                .EmptyBuilder
                .Put(FRONTEND_KEY, TagValue.Create("front2"))
                .Put(FRONTEND_OS_VERSION_KEY, TagValue.Create("1.1.1"))
                .Build();

            long sum = 0;
            for (int i = 0; i < 10; i++)
            {
                sum = sum + (25648 * i);
                if (i % 2 == 0)
                {
                    statsRecorder.NewMeasureMap().Put(VIDEO_SIZE, 25648 * i).Record(context1);
                }
                else
                {
                    statsRecorder.NewMeasureMap().Put(VIDEO_SIZE, 25648 * i).Record(context2);
                }
            }

            IViewData videoSizeView = viewManager.GetView(VIDEO_SIZE_VIEW_NAME);
            var viewDataAggMap = videoSizeView.AggregationMap.ToList();
            var view = viewManager.AllExportedViews.ToList()[0];
            for (int i = 0; i < view.Columns.Count; i++)
            {
                output.WriteLine(view.Columns[i] + "=" + GetTagValues(i, viewDataAggMap));
            }

            var keys = new List<TagValue>() { TagValue.Create("1.1.1") };

            var results = videoSizeView.AggregationMap.Where((kvp) =>
            {
                foreach (var key in keys)
                {
                    if (!kvp.Key.Values.Contains(key))
                    {
                        return false;
                    }
                }
                return true;
            });

            output.WriteLine(videoSizeView.ToString());

            output.WriteLine(sum.ToString());
        }

        private string GetTagValues(int i, List<KeyValuePair<TagValues, IAggregationData>> viewDataAggMap)
        {
            string result = string.Empty;
            foreach (var kvp in viewDataAggMap)
            {
                var val = kvp.Key.Values[i];
                if (val != null)
                {
                    result += val.AsString;
                }
            }
            return result;
        }

    }
}

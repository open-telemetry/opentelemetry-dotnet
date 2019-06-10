namespace Samples
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using OpenTelemetry.Exporter.Stackdriver;
    using OpenTelemetry.Stats;
    using OpenTelemetry.Stats.Aggregations;
    using OpenTelemetry.Stats.Measures;
    using OpenTelemetry.Tags;
    using OpenTelemetry.Trace;
    using OpenTelemetry.Trace.Sampler;

    internal class TestStackdriver
    {
        private static ITracer tracer = Tracing.Tracer;
        private static ITagger tagger = Tags.Tagger;

        private static IStatsRecorder statsRecorder = Stats.StatsRecorder;
        private static readonly IMeasureDouble VideoSize = MeasureDouble.Create("my_org/measure/video_size", "size of processed videos", "MiB");
        private static readonly TagKey FrontendKey = TagKey.Create("my_org/keys/frontend");

        private static long MiB = 1 << 20;

        private static readonly IViewName VideoSizeViewName = ViewName.Create("my_org/views/video_size");

        private static readonly IView VideoSizeView = View.Create(
            name: VideoSizeViewName,
            description: "processed video size over time",
            measure: VideoSize,
            aggregation: Sum.Create(),
            columns: new List<TagKey>() { FrontendKey });

        internal static object Run(string projectId)
        {
            var exporter = new StackdriverExporter(
                projectId, 
                Tracing.ExportComponent,
                Stats.ViewManager);
            exporter.Start();

            ITagContextBuilder tagContextBuilder = tagger.CurrentBuilder.Put(FrontendKey, TagValue.Create("mobile-ios9.3.5"));

            var spanBuilder = tracer
                .SpanBuilder("incoming request")
                .SetRecordEvents(true)
                .SetSampler(Samplers.AlwaysSample);

            Stats.ViewManager.RegisterView(VideoSizeView);

            using (var scopedTags = tagContextBuilder.BuildScoped())
            {
                using (var scopedSpan = spanBuilder.StartScopedSpan())
                {
                    tracer.CurrentSpan.AddEvent("Processing video.");
                    Thread.Sleep(TimeSpan.FromMilliseconds(10));

                    statsRecorder.NewMeasureMap()
                        .Put(VideoSize, 25 * MiB)
                        .Record();
                }
            }

            Thread.Sleep(TimeSpan.FromMilliseconds(5100));

            var viewData = Stats.ViewManager.GetView(VideoSizeViewName);

            Console.WriteLine(viewData);

            Console.WriteLine("Done... wait for events to arrive to backend!");
            Console.ReadLine();

            return null;
        }
    }
}

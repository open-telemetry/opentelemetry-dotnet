namespace Samples
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using Microsoft.ApplicationInsights.Extensibility;
    using OpenTelemetry.Exporter.ApplicationInsights;
    using OpenTelemetry.Stats;
    using OpenTelemetry.Stats.Aggregations;
    using OpenTelemetry.Stats.Measures;
    using OpenTelemetry.Tags;
    using OpenTelemetry.Trace;
    using OpenTelemetry.Trace.Sampler;

    internal class TestApplicationInsights
    {
        private static ITracer tracer = Tracing.Tracer;
        private static ITagger tagger = Tags.Tagger;

        private static IStatsRecorder statsRecorder = Stats.StatsRecorder;
        private static readonly IMeasureLong VideoSize = MeasureLong.Create("my.org/measure/video_size", "size of processed videos", "By");
        private static readonly TagKey FrontendKey = TagKey.Create("my.org/keys/frontend");

        private static long MiB = 1 << 20;

        private static readonly IViewName VideoSizeViewName = ViewName.Create("my.org/views/video_size");

        private static readonly IView VideoSizeView = View.Create(
            VideoSizeViewName, 
            "processed video size over time",
            VideoSize,
            Distribution.Create(BucketBoundaries.Create(new List<double>() { 0.0, 16.0 * MiB, 256.0 * MiB })),
            new List<TagKey>() { FrontendKey });

        internal static object Run()
        {
            TelemetryConfiguration.Active.InstrumentationKey = "instrumentation-key";
            var exporter = new ApplicationInsightsExporter(Tracing.ExportComponent, Stats.ViewManager, TelemetryConfiguration.Active);
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
                    tracer.CurrentSpan.AddEvent("Start processing video.");
                    Thread.Sleep(TimeSpan.FromMilliseconds(10));
                    statsRecorder.NewMeasureMap().Put(VideoSize, 25 * MiB).Record();
                    tracer.CurrentSpan.AddEvent("Finished processing video.");
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

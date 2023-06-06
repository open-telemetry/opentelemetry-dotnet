using System.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Exporter
{
    public class ConsoleActivityJsonExporter : ConsoleExporter<Activity>
    {
        public ConsoleActivityJsonExporter(ConsoleExporterOptions options)
            : base(options)
        {
        }

        public override ExportResult Export(in Batch<Activity> batch)
        {
            foreach (var activity in batch)
            {
                dynamic activityBuilder = new System.Dynamic.ExpandoObject();


                activityBuilder.TraceId = activity.TraceId;
                activityBuilder.SpanId = activity.SpanId;
                activityBuilder.ActivityTraceFlags = activity.ActivityTraceFlags;
                activityBuilder.ActivitySourceName = activity.Source.Name;
                activityBuilder.DisplayName = activity.DisplayName;
                activityBuilder.Kind = activity.Kind;
                activityBuilder.StartTime = activity.StartTimeUtc.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ");
                activityBuilder.Duration = activity.Duration;

                if (!string.IsNullOrEmpty(activity.TraceStateString))
                {
                    activityBuilder.TraceState = activity.TraceStateString;
                }

                if (activity.ParentSpanId != default)
                {
                    activityBuilder.ParentSpanId = activity.ParentSpanId;
                }

                var statusCode = string.Empty;
                var statusDesc = string.Empty;

                if (activity.TagObjects.Any())
                {
                    foreach (ref readonly var tag in activity.EnumerateTagObjects())
                    {
                        if (tag.Key == SpanAttributeConstants.StatusCodeKey)
                        {
                            statusCode = tag.Value as string;
                            continue;
                        }

                        if (tag.Key == SpanAttributeConstants.StatusDescriptionKey)
                        {
                            statusDesc = tag.Value as string;
                            continue;
                        }

                        if (ConsoleTagTransformer.Instance.TryTransformTag(tag, out var result))
                        {
                            activityBuilder.Tags = result;
                        }
                    }
                }

                if (activity.Status != ActivityStatusCode.Unset)
                {
                    activityBuilder.StatusCode = activity.Status;

                    if (!string.IsNullOrEmpty(activity.StatusDescription))
                    {
                        activityBuilder.StatusDescription = activity.StatusDescription;
                    }
                }
                else if (!string.IsNullOrEmpty(statusCode))
                {
                    activityBuilder.StatusCode = statusCode;
                    if (!string.IsNullOrEmpty(statusDesc))
                    {
                        activityBuilder.StatusDescription = statusDesc;
                    }
                }

                if (activity.Events.Any())
                {
                    this.WriteLine("Activity.Events:");
                    activityBuilder.Events = new List<dynamic>();

                    foreach (ref readonly var activityEvent in activity.EnumerateEvents())
                    {
                        dynamic activityEventBuilder = new System.Dynamic.ExpandoObject();

                        activityEventBuilder.Name = activityEvent.Name;
                        activityEventBuilder.Timestamp = activityEvent.Timestamp;

                        foreach (ref readonly var attribute in activityEvent.EnumerateTagObjects())
                        {
                            if (ConsoleTagTransformer.Instance.TryTransformTag(attribute, out var result))
                            {
                                activityEventBuilder.Tags = result;
                            }
                        }

                        activityBuilder.Events.add(activityEventBuilder);
                    }
                }

                if (activity.Links.Any())
                {
                    activityBuilder.Links = new List<dynamic>();

                    foreach (ref readonly var activityLink in activity.EnumerateLinks())
                    {
                        dynamic activityLinkBuilder = new System.Dynamic.ExpandoObject();

                        activityLinkBuilder.TraceId = activityLink.Context.TraceId;
                        activityLinkBuilder.SpanId = activityLink.Context.SpanId;

                        foreach (ref readonly var attribute in activityLink.EnumerateTagObjects())
                        {
                            if (ConsoleTagTransformer.Instance.TryTransformTag(attribute, out var result))
                            {
                                activityLinkBuilder.Tags = result;
                            }
                        }

                        activityBuilder.Links.add(activityLinkBuilder);
                    }
                }

                var resource = this.ParentProvider.GetResource();
                if (resource != Resource.Empty)
                {

                    foreach (var resourceAttribute in resource.Attributes)
                    {
                        if (ConsoleTagTransformer.Instance.TryTransformTag(resourceAttribute, out var result))
                        {
                            activityBuilder.Attributes = result;
                        }
                    }
                }

                var json = JsonConvert.SerializeObject(activityBuilder, Formatting.None, new JsonSerializerSettings
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                    ContractResolver = new CamelCasePropertyNamesContractResolver(),
                });

                this.WriteLine(json);
            }

            return ExportResult.Success;
        }
    }
}

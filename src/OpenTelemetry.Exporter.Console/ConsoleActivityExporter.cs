// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter;

public class ConsoleActivityExporter : ConsoleExporter<Activity>
{
    public ConsoleActivityExporter(ConsoleExporterOptions options)
        : base(options)
    {
    }

    public ExportResult ExportJson(in Batch<Activity> batch)
    {
        foreach (var activity in batch)
        {
            var output = new
            {
                Activity = new
                {
                    TraceId = activity.TraceId.ToString(),
                    SpanId = activity.SpanId.ToString(),
                    TraceFlags = activity.ActivityTraceFlags,
                    TraceState = string.IsNullOrEmpty(activity.TraceStateString) ? null : activity.TraceStateString,
                    ParentSpanId = activity.ParentSpanId == default ? default : activity.ParentSpanId.ToString(),
                    ActivitySourceName = activity.Source.Name,
                    ActivitySourceVersion =
                        string.IsNullOrEmpty(activity.Source.Version) ? null : activity.Source.Version,
                    DisplayName = activity.DisplayName,
                    Kind = activity.Kind,
                    StartTime = activity.StartTimeUtc.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ"),
                    Duration = activity.Duration,
                    Tags = activity.TagObjects.Select(tag => new { Key = tag.Key, Value = tag.Value }).ToList(),
                    StatusCode = activity.Status != ActivityStatusCode.Unset ? activity.Status.ToString() : null,
                    StatusDescription =
                        !string.IsNullOrEmpty(activity.StatusDescription) ? activity.StatusDescription : null,
                    Events = activity.Events.Any()
                        ? null
                        : activity.Events.Select(e => new
                        {
                            Name = e.Name,
                            Timestamp = e.Timestamp,
                            Attributes = e.Tags.Select(a => new { Key = a.Key, Value = a.Value }).ToList(),
                        }).ToList(),
                    Links = activity.Links.Any()
                        ? null
                        : activity.Links.Select(l =>
                        {
                            if (l.Tags != null)
                            {
                                return new
                                {
                                    TraceId = l.Context.TraceId,
                                    SpanId = l.Context.SpanId,
                                    Attributes = l.Tags.Select(a => new { Key = a.Key, Value = a.Value }).ToList(),
                                };
                            }

                            return null;
                        }).ToList(),
                    Resource = this.ParentProvider.GetResource() != Resource.Empty
                        ? this.ParentProvider.GetResource().Attributes.Select(a => new { Key = a.Key, Value = a.Value })
                           .ToList()
                        : null,
                },
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true, IgnoreNullValues = true, Converters =
                {
                    new JsonStringEnumConverter(),
                },
            };

            var json = JsonSerializer.Serialize(output, options);
            this.WriteLine(json);
        }

        return ExportResult.Success;
    }

    public override ExportResult Export(in Batch<Activity> batch)
    {
        return this.ExportJson(batch);

        // foreach (var activity in batch)
        // {

        //     this.WriteLine($"Activity.TraceId:            {activity.TraceId}");
        //     this.WriteLine($"Activity.SpanId:             {activity.SpanId}");
        //     this.WriteLine($"Activity.TraceFlags:         {activity.ActivityTraceFlags}");
        //     if (!string.IsNullOrEmpty(activity.TraceStateString))
        //     {
        //         this.WriteLine($"Activity.TraceState:         {activity.TraceStateString}");
        //     }

        //     if (activity.ParentSpanId != default)
        //     {
        //         this.WriteLine($"Activity.ParentSpanId:       {activity.ParentSpanId}");
        //     }

        //     this.WriteLine($"Activity.ActivitySourceName: {activity.Source.Name}");
        //     if (!string.IsNullOrEmpty(activity.Source.Version))
        //     {
        //         this.WriteLine($"Activity.ActivitySourceVersion: {activity.Source.Version}");
        //     }

        //     this.WriteLine($"Activity.DisplayName:        {activity.DisplayName}");
        //     this.WriteLine($"Activity.Kind:               {activity.Kind}");
        //     this.WriteLine($"Activity.StartTime:          {activity.StartTimeUtc:yyyy-MM-ddTHH:mm:ss.fffffffZ}");
        //     this.WriteLine($"Activity.Duration:           {activity.Duration}");
        //     var statusCode = string.Empty;
        //     var statusDesc = string.Empty;

        //     if (activity.TagObjects.Any())
        //     {
        //         this.WriteLine("Activity.Tags:");
        //         foreach (ref readonly var tag in activity.EnumerateTagObjects())
        //         {
        //             if (tag.Key == SpanAttributeConstants.StatusCodeKey)
        //             {
        //                 statusCode = tag.Value as string;
        //                 continue;
        //             }

        //             if (tag.Key == SpanAttributeConstants.StatusDescriptionKey)
        //             {
        //                 statusDesc = tag.Value as string;
        //                 continue;
        //             }

        //             if (this.TagTransformer.TryTransformTag(tag, out var result))
        //             {
        //                 this.WriteLine($"    {result}");
        //             }
        //         }
        //     }

        //     if (activity.Status != ActivityStatusCode.Unset)
        //     {
        //         this.WriteLine($"StatusCode: {activity.Status}");
        //         if (!string.IsNullOrEmpty(activity.StatusDescription))
        //         {
        //             this.WriteLine($"Activity.StatusDescription:  {activity.StatusDescription}");
        //         }
        //     }
        //     else if (!string.IsNullOrEmpty(statusCode))
        //     {
        //         this.WriteLine($"    StatusCode: {statusCode}");
        //         if (!string.IsNullOrEmpty(statusDesc))
        //         {
        //             this.WriteLine($"    Activity.StatusDescription: {statusDesc}");
        //         }
        //     }

        //     if (activity.Events.Any())
        //     {
        //         this.WriteLine("Activity.Events:");
        //         foreach (ref readonly var activityEvent in activity.EnumerateEvents())
        //         {
        //             this.WriteLine($"    {activityEvent.Name} [{activityEvent.Timestamp}]");
        //             foreach (ref readonly var attribute in activityEvent.EnumerateTagObjects())
        //             {
        //                 if (this.TagTransformer.TryTransformTag(attribute, out var result))
        //                 {
        //                     this.WriteLine($"        {result}");
        //                 }
        //             }
        //         }
        //     }

        //     if (activity.Links.Any())
        //     {
        //         this.WriteLine("Activity.Links:");
        //         foreach (ref readonly var activityLink in activity.EnumerateLinks())
        //         {
        //             this.WriteLine($"    {activityLink.Context.TraceId} {activityLink.Context.SpanId}");
        //             foreach (ref readonly var attribute in activityLink.EnumerateTagObjects())
        //             {
        //                 if (this.TagTransformer.TryTransformTag(attribute, out var result))
        //                 {
        //                     this.WriteLine($"        {result}");
        //                 }
        //             }
        //         }
        //     }

        //     var resource = this.ParentProvider.GetResource();
        //     if (resource != Resource.Empty)
        //     {
        //         this.WriteLine("Resource associated with Activity:");
        //         foreach (var resourceAttribute in resource.Attributes)
        //         {
        //             if (this.TagTransformer.TryTransformTag(resourceAttribute, out var result))
        //             {
        //                 this.WriteLine($"    {result}");
        //             }
        //         }
        //     }

        //     this.WriteLine(string.Empty);
        // }

        // return ExportResult.Success;
    }
}

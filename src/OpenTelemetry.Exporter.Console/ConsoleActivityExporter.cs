// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Exporter;

public class ConsoleActivityExporter : ConsoleExporter<Activity>
{
    public ConsoleActivityExporter(ConsoleExporterOptions options)
        : base(options)
    {
    }

    public override ExportResult Export(in Batch<Activity> batch)
    {
        foreach (var activity in batch)
        {
            this.WriteLine($"Activity.TraceId:            {activity.TraceId}");
            this.WriteLine($"Activity.SpanId:             {activity.SpanId}");
            this.WriteLine($"Activity.TraceFlags:         {activity.ActivityTraceFlags}");
            if (!string.IsNullOrEmpty(activity.TraceStateString))
            {
                this.WriteLine($"Activity.TraceState:         {activity.TraceStateString}");
            }

            if (activity.ParentSpanId != default)
            {
                this.WriteLine($"Activity.ParentSpanId:       {activity.ParentSpanId}");
            }

            this.WriteLine($"Activity.DisplayName:        {activity.DisplayName}");
            this.WriteLine($"Activity.Kind:               {activity.Kind}");
            this.WriteLine($"Activity.StartTime:          {activity.StartTimeUtc:yyyy-MM-ddTHH:mm:ss.fffffffZ}");
            this.WriteLine($"Activity.Duration:           {activity.Duration}");
            var statusCode = string.Empty;
            var statusDesc = string.Empty;

            if (activity.TagObjects.Any())
            {
                this.WriteLine("Activity.Tags:");
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

                    if (this.TagWriter.TryTransformTag(tag, out var result))
                    {
                        this.WriteLine($"    {result.Key}: {result.Value}");
                    }
                }
            }

            if (activity.Status != ActivityStatusCode.Unset)
            {
                this.WriteLine($"StatusCode: {activity.Status}");
                if (!string.IsNullOrEmpty(activity.StatusDescription))
                {
                    this.WriteLine($"Activity.StatusDescription:  {activity.StatusDescription}");
                }
            }
            else if (!string.IsNullOrEmpty(statusCode))
            {
                this.WriteLine($"    StatusCode: {statusCode}");
                if (!string.IsNullOrEmpty(statusDesc))
                {
                    this.WriteLine($"    Activity.StatusDescription: {statusDesc}");
                }
            }

            if (activity.Events.Any())
            {
                this.WriteLine("Activity.Events:");
                foreach (ref readonly var activityEvent in activity.EnumerateEvents())
                {
                    this.WriteLine($"    {activityEvent.Name} [{activityEvent.Timestamp}]");
                    foreach (ref readonly var attribute in activityEvent.EnumerateTagObjects())
                    {
                        if (this.TagWriter.TryTransformTag(attribute, out var result))
                        {
                            this.WriteLine($"        {result.Key}: {result.Value}");
                        }
                    }
                }
            }

            if (activity.Links.Any())
            {
                this.WriteLine("Activity.Links:");
                foreach (ref readonly var activityLink in activity.EnumerateLinks())
                {
                    this.WriteLine($"    {activityLink.Context.TraceId} {activityLink.Context.SpanId}");
                    foreach (ref readonly var attribute in activityLink.EnumerateTagObjects())
                    {
                        if (this.TagWriter.TryTransformTag(attribute, out var result))
                        {
                            this.WriteLine($"        {result.Key}: {result.Value}");
                        }
                    }
                }
            }

            this.WriteLine("Instrumentation scope (ActivitySource):");
            this.WriteLine($"    Name: {activity.Source.Name}");
            if (!string.IsNullOrEmpty(activity.Source.Version))
            {
                this.WriteLine($"    Version: {activity.Source.Version}");
            }

            if (!string.IsNullOrEmpty(activity.Source.TelemetrySchemaUrl))
            {
                this.WriteLine($"    Schema URL: {activity.Source.TelemetrySchemaUrl}");
            }

            if (activity.Source.Tags?.Any() == true)
            {
                this.WriteLine("    Tags:");
                foreach (var activitySourceTag in activity.Source.Tags)
                {
                    if (this.TagWriter.TryTransformTag(activitySourceTag, out var result))
                    {
                        this.WriteLine($"        {result.Key}: {result.Value}");
                    }
                }
            }

            var resource = this.ParentProvider.GetResource();
            if (resource != Resource.Empty)
            {
                this.WriteLine("Resource associated with Activity:");
                foreach (var resourceAttribute in resource.Attributes)
                {
                    if (this.TagWriter.TryTransformTag(resourceAttribute.Key, resourceAttribute.Value, out var result))
                    {
                        this.WriteLine($"    {result.Key}: {result.Value}");
                    }
                }
            }

            this.WriteLine(string.Empty);
        }

        return ExportResult.Success;
    }
}

// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Exporter;

public class ConsoleActivityExporter : ConsoleExporter<Activity>
{
    // Note: The default limits are applied for now. Configured limits are read once the SDK binds
    // the limit options to configuration.
    private readonly SpanLimiter spanLimiter = new(new SpanLimitOptions(), new AttributeLimitOptions(), nameof(ConsoleActivityExporter));

    public ConsoleActivityExporter(ConsoleExporterOptions options)
        : base(options)
    {
    }

    public override ExportResult Export(in Batch<Activity> batch)
    {
        using var limits = this.spanLimiter.BeginBatch();

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
            var attributes = limits.CreateAttributeReader(activity);
            if (activity.TagObjects.Any())
            {
                this.WriteLine("Activity.Tags:");
                while (attributes.TryRead(out var tag))
                {
                    if (this.TagWriter.TryTransformTag(tag, attributes.ValueLengthLimit, out var result))
                    {
                        this.WriteLine($"    {result.Key}: {result.Value}");
                    }
                    else
                    {
                        attributes.MarkDropped();
                    }
                }

                this.WriteDroppedCount("    ", attributes.DroppedCount);
            }

            var status = attributes.Status;
            if (status != ActivityStatusCode.Unset)
            {
                this.WriteLine($"StatusCode: {status}");
                var statusDescription = attributes.StatusDescription;
                if (!string.IsNullOrEmpty(statusDescription))
                {
                    this.WriteLine($"Activity.StatusDescription:  {statusDescription}");
                }
            }

            if (activity.Events.Any())
            {
                this.WriteLine("Activity.Events:");
                var events = limits.CreateEventReader(activity);
                while (events.TryRead(out var activityEvent))
                {
                    this.WriteLine($"    {activityEvent.Name} [{activityEvent.Timestamp}]");
                    var eventAttributes = events.CreateAttributeReader();
                    while (eventAttributes.TryRead(out var attribute))
                    {
                        if (this.TagWriter.TryTransformTag(attribute, eventAttributes.ValueLengthLimit, out var result))
                        {
                            this.WriteLine($"        {result.Key}: {result.Value}");
                        }
                        else
                        {
                            eventAttributes.MarkDropped();
                        }
                    }

                    this.WriteDroppedCount("        ", eventAttributes.DroppedCount);
                }

                this.WriteDroppedCount("    ", events.DroppedCount);
            }

            if (activity.Links.Any())
            {
                this.WriteLine("Activity.Links:");
                var links = limits.CreateLinkReader(activity);
                while (links.TryRead(out var activityLink))
                {
                    this.WriteLine($"    {activityLink.Context.TraceId} {activityLink.Context.SpanId}");
                    var linkAttributes = links.CreateAttributeReader();
                    while (linkAttributes.TryRead(out var attribute))
                    {
                        if (this.TagWriter.TryTransformTag(attribute, linkAttributes.ValueLengthLimit, out var result))
                        {
                            this.WriteLine($"        {result.Key}: {result.Value}");
                        }
                        else
                        {
                            linkAttributes.MarkDropped();
                        }
                    }

                    this.WriteDroppedCount("        ", linkAttributes.DroppedCount);
                }

                this.WriteDroppedCount("    ", links.DroppedCount);
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

                if (!string.IsNullOrEmpty(resource.SchemaUrl))
                {
                    this.WriteLine($"    Schema URL: {resource.SchemaUrl}");
                }
            }

            this.WriteLine(string.Empty);
        }

        return ExportResult.Success;
    }

    private void WriteDroppedCount(string indent, int droppedCount)
    {
        if (droppedCount > 0)
        {
            this.WriteLine($"{indent}({droppedCount} dropped)");
        }
    }
}

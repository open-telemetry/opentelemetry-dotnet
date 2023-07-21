// <copyright file="ConsoleActivityExporter.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
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

            this.WriteLine($"Activity.ActivitySourceName: {activity.Source.Name}");
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

                    if (ConsoleTagTransformer.Instance.TryTransformTag(tag, out var result))
                    {
                        this.WriteLine($"    {result}");
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
                        if (ConsoleTagTransformer.Instance.TryTransformTag(attribute, out var result))
                        {
                            this.WriteLine($"        {result}");
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
                        if (ConsoleTagTransformer.Instance.TryTransformTag(attribute, out var result))
                        {
                            this.WriteLine($"        {result}");
                        }
                    }
                }
            }

            var resource = this.ParentProvider.GetResource();
            if (resource != Resource.Empty)
            {
                this.WriteLine("Resource associated with Activity:");
                foreach (var resourceAttribute in resource.Attributes)
                {
                    if (ConsoleTagTransformer.Instance.TryTransformTag(resourceAttribute, out var result))
                    {
                        this.WriteLine($"    {result}");
                    }
                }
            }

            this.WriteLine(string.Empty);
        }

        return ExportResult.Success;
    }
}

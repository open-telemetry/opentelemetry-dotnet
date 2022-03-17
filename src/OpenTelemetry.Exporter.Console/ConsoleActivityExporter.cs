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

using System;
using System.Diagnostics;
using System.Linq;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Exporter
{
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
                this.WriteLine($"Activity.TraceId:          {activity.TraceId}");
                this.WriteLine($"Activity.SpanId:           {activity.SpanId}");
                this.WriteLine($"Activity.TraceFlags:           {activity.ActivityTraceFlags}");
                if (!string.IsNullOrEmpty(activity.TraceStateString))
                {
                    this.WriteLine($"Activity.TraceState:    {activity.TraceStateString}");
                }

                if (activity.ParentSpanId != default)
                {
                    this.WriteLine($"Activity.ParentSpanId:    {activity.ParentSpanId}");
                }

                this.WriteLine($"Activity.ActivitySourceName: {activity.Source.Name}");
                this.WriteLine($"Activity.DisplayName: {activity.DisplayName}");
                this.WriteLine($"Activity.Kind:        {activity.Kind}");
                this.WriteLine($"Activity.StartTime:   {activity.StartTimeUtc:yyyy-MM-ddTHH:mm:ss.fffffffZ}");
                this.WriteLine($"Activity.Duration:    {activity.Duration}");
                var statusCode = string.Empty;
                var statusDesc = string.Empty;

                if (activity.TagObjects.Any())
                {
                    this.WriteLine("Activity.Tags:");
                    foreach (var tag in activity.TagObjects)
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

                        if (tag.Value is not Array array)
                        {
                            this.WriteLine($"    {tag.Key}: {tag.Value}");
                            continue;
                        }

                        this.WriteLine($"    {tag.Key}: [{string.Join(", ", array.Cast<object>())}]");
                    }
                }

                if (activity.Status != ActivityStatusCode.Unset)
                {
                    this.WriteLine($"StatusCode : {activity.Status}");
                    if (!string.IsNullOrEmpty(activity.StatusDescription))
                    {
                        this.WriteLine($"Error : {activity.StatusDescription}");
                    }
                }
                else if (!string.IsNullOrEmpty(statusCode))
                {
                    this.WriteLine($"   StatusCode : {statusCode}");
                    if (!string.IsNullOrEmpty(statusDesc))
                    {
                        this.WriteLine($"   Error : {statusDesc}");
                    }
                }

                if (activity.Events.Any())
                {
                    this.WriteLine("Activity.Events:");
                    foreach (var activityEvent in activity.Events)
                    {
                        this.WriteLine($"    {activityEvent.Name} [{activityEvent.Timestamp}]");
                        foreach (var attribute in activityEvent.Tags)
                        {
                            this.WriteLine($"        {attribute.Key}: {attribute.Value}");
                        }
                    }
                }

                if (activity.Links.Any())
                {
                    this.WriteLine("Activity.Links:");
                    foreach (var activityLink in activity.Links)
                    {
                        this.WriteLine($"    {activityLink.Context.TraceId} {activityLink.Context.SpanId}");
                    }
                }

                var resource = this.ParentProvider.GetResource();
                if (resource != Resource.Empty)
                {
                    this.WriteLine("Resource associated with Activity:");
                    foreach (var resourceAttribute in resource.Attributes)
                    {
                        this.WriteLine($"    {resourceAttribute.Key}: {resourceAttribute.Value}");
                    }
                }

                this.WriteLine(string.Empty);
            }

            return ExportResult.Success;
        }
    }
}

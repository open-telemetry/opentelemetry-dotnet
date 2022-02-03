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
                this.WriteLine($"Activity.Id:          {activity.Id}");
                if (!string.IsNullOrEmpty(activity.ParentId))
                {
                    this.WriteLine($"Activity.ParentId:    {activity.ParentId}");
                }

                this.WriteLine($"Activity.ActivitySourceName: {activity.Source.Name}");
                this.WriteLine($"Activity.DisplayName: {activity.DisplayName}");
                this.WriteLine($"Activity.Kind:        {activity.Kind}");
                this.WriteLine($"Activity.StartTime:   {activity.StartTimeUtc:yyyy-MM-ddTHH:mm:ss.fffffffZ}");
                this.WriteLine($"Activity.Duration:    {activity.Duration}");
                if (activity.TagObjects.Any())
                {
                    this.WriteLine("Activity.TagObjects:");
                    foreach (var tag in activity.TagObjects)
                    {
                        var array = tag.Value as Array;

                        if (array == null)
                        {
                            this.WriteLine($"    {tag.Key}: {tag.Value}");
                            continue;
                        }

                        this.WriteLine($"    {tag.Key}: [{string.Join(", ", array.Cast<object>())}]");
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

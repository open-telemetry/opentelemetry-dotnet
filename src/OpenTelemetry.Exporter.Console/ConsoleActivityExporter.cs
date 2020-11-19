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

namespace OpenTelemetry.Exporter.Console
{
    internal class ConsoleActivityExporter : ConsoleExporter<Activity>
    {
        public ConsoleActivityExporter(ConsoleExporterOptions options)
            : base(options) => this.Init((activity, writeTo) => this.ExportActivity(activity, writeTo));

        private void ExportActivity(Activity activity, Action<string> writeLine)
        {
                writeLine($"Activity.Id:          {activity.Id}");
                if (!string.IsNullOrEmpty(activity.ParentId))
                {
                    writeLine($"Activity.ParentId:    {activity.ParentId}");
                }

                writeLine($"Activity.DisplayName: {activity.DisplayName}");
                writeLine($"Activity.Kind:        {activity.Kind}");
                writeLine($"Activity.StartTime:   {activity.StartTimeUtc:yyyy-MM-ddTHH:mm:ss.fffffffZ}");
                writeLine($"Activity.Duration:    {activity.Duration}");
                if (activity.TagObjects.Any())
                {
                    writeLine("Activity.TagObjects:");
                    foreach (var tag in activity.TagObjects)
                    {
                        var array = tag.Value as Array;

                        if (array == null)
                        {
                            writeLine($"    {tag.Key}: {tag.Value}");
                            continue;
                        }

                        writeLine($"    {tag.Key}: [{string.Join(", ", array.Cast<object>())}]");
                    }
                }

                if (activity.Events.Any())
                {
                    writeLine("Activity.Events:");
                    foreach (var activityEvent in activity.Events)
                    {
                        writeLine($"    {activityEvent.Name} [{activityEvent.Timestamp}]");
                        foreach (var attribute in activityEvent.Tags)
                        {
                            writeLine($"        {attribute.Key}: {attribute.Value}");
                        }
                    }
                }

                if (activity.Baggage.Any())
                {
                    writeLine("Activity.Baggage:");
                    foreach (var baggage in activity.Baggage)
                    {
                        writeLine($"    {baggage.Key}: {baggage.Value}");
                    }
                }

                var resource = this.ParentProvider.GetResource();
                if (resource != Resource.Empty)
                {
                    writeLine("Resource associated with Activity:");
                    foreach (var resourceAttribute in resource.Attributes)
                    {
                        writeLine($"    {resourceAttribute.Key}: {resourceAttribute.Value}");
                    }
                }

                writeLine(string.Empty);
        }
    }
}

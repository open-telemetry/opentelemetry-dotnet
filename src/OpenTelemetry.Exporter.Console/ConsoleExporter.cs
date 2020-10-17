// <copyright file="ConsoleExporter.cs" company="OpenTelemetry Authors">
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
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Exporter
{
    public class ConsoleExporter : BaseExporter<Activity>
    {
        private readonly JsonSerializerOptions serializerOptions;
        private readonly bool displayAsJson;

        public ConsoleExporter(ConsoleExporterOptions options)
        {
            this.serializerOptions = new JsonSerializerOptions()
            {
                WriteIndented = true,
            };

            this.displayAsJson = options?.DisplayAsJson ?? false;

            this.serializerOptions.Converters.Add(new JsonStringEnumConverter());
            this.serializerOptions.Converters.Add(new ActivitySpanIdConverter());
            this.serializerOptions.Converters.Add(new ActivityTraceIdConverter());
        }

        public override ExportResult Export(in Batch<Activity> batch)
        {
            foreach (var activity in batch)
            {
                if (this.displayAsJson)
                {
                    Console.WriteLine(JsonSerializer.Serialize(activity, this.serializerOptions));
                }
                else
                {
                    Console.WriteLine($"Activity.Id:          {activity.Id}");
                    if (!string.IsNullOrEmpty(activity.ParentId))
                    {
                        Console.WriteLine($"Activity.ParentId:    {activity.ParentId}");
                    }

                    Console.WriteLine($"Activity.DisplayName: {activity.DisplayName}");
                    Console.WriteLine($"Activity.Kind:        {activity.Kind}");
                    Console.WriteLine($"Activity.StartTime:   {activity.StartTimeUtc:yyyy-MM-ddTHH:mm:ss.fffffffZ}");
                    Console.WriteLine($"Activity.Duration:    {activity.Duration}");
                    if (activity.TagObjects.Any())
                    {
                        Console.WriteLine("Activity.TagObjects:");
                        foreach (var tag in activity.TagObjects)
                        {
                            var array = tag.Value as Array;

                            if (array == null)
                            {
                                Console.WriteLine($"    {tag.Key}: {tag.Value}");
                                continue;
                            }

                            Console.Write($"    {tag.Key}: [");

                            for (int i = 0; i < array.Length; i++)
                            {
                                Console.Write(i != 0 ? ", " : string.Empty);
                                Console.Write($"{array.GetValue(i)}");
                            }

                            Console.WriteLine($"]");
                        }
                    }

                    if (activity.Events.Any())
                    {
                        Console.WriteLine("Activity.Events:");
                        foreach (var activityEvent in activity.Events)
                        {
                            Console.WriteLine($"    {activityEvent.Name} [{activityEvent.Timestamp}]");
                            foreach (var attribute in activityEvent.Tags)
                            {
                                Console.WriteLine($"        {attribute.Key}: {attribute.Value}");
                            }
                        }
                    }

                    if (activity.Baggage.Any())
                    {
                        Console.WriteLine("Activity.Baggage:");
                        foreach (var baggage in activity.Baggage)
                        {
                            Console.WriteLine($"    {baggage.Key}: {baggage.Value}");
                        }
                    }

                    var resource = activity.GetResource();
                    if (resource != Resource.Empty)
                    {
                        Console.WriteLine("Resource associated with Activity:");
                        foreach (var resourceAttribute in resource.Attributes)
                        {
                            Console.WriteLine($"    {resourceAttribute.Key}: {resourceAttribute.Value}");
                        }
                    }

                    Console.WriteLine();
                }
            }

            return ExportResult.Success;
        }
    }
}

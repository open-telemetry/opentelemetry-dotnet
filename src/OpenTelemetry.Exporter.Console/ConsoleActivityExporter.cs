﻿// <copyright file="ConsoleActivityExporter.cs" company="OpenTelemetry Authors">
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

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Trace.Export;

namespace OpenTelemetry.Exporter.Console
{
    public class ConsoleActivityExporter : ActivityExporter
    {
        private readonly JsonSerializerOptions serializerOptions;
        private bool displayAsJson;

        public ConsoleActivityExporter(ConsoleActivityExporterOptions options)
        {
            this.serializerOptions = new JsonSerializerOptions()
            {
                WriteIndented = true,
            };

            this.displayAsJson = options.DisplayAsJson;

            this.serializerOptions.Converters.Add(new JsonStringEnumConverter());
            this.serializerOptions.Converters.Add(new ActivitySpanIdConverter());
            this.serializerOptions.Converters.Add(new ActivityTraceIdConverter());
        }

        public override Task<ExportResult> ExportAsync(IEnumerable<Activity> activityBatch, CancellationToken cancellationToken)
        {
            foreach (var activity in activityBatch)
            {
                if (this.displayAsJson)
                {
                    System.Console.WriteLine(JsonSerializer.Serialize(activity, this.serializerOptions));
                }
                else
                {
                    System.Console.WriteLine("Activity ID - " + activity.Id);
                    if (!string.IsNullOrEmpty(activity.ParentId))
                    {
                        System.Console.WriteLine("Activity ParentId - " + activity.ParentId);
                    }

                    System.Console.WriteLine("Activity OperationName - " + activity.OperationName);
                    System.Console.WriteLine("Activity DisplayName - " + activity.DisplayName);
                    System.Console.WriteLine("Activity Kind - " + activity.Kind);
                    System.Console.WriteLine("Activity StartTime - " + activity.StartTimeUtc);
                    System.Console.WriteLine("Activity Duration - " + activity.Duration);
                    if (activity.Tags.Count() > 0)
                    {
                        System.Console.WriteLine("Activity Tags");
                        foreach (var tag in activity.Tags)
                        {
                            System.Console.WriteLine($"\t {tag.Key} : {tag.Value}");
                        }
                    }

                    if (activity.Events.Any())
                    {
                        System.Console.WriteLine("Activity Events");
                        foreach (var activityEvent in activity.Events)
                        {
                            System.Console.WriteLine($"Event Name: {activityEvent.Name} TimeStamp: {activityEvent.Timestamp}");
                            foreach (var attribute in activityEvent.Attributes)
                            {
                                System.Console.WriteLine($"\t {attribute.Key} : {attribute.Value}");
                            }
                        }
                    }

                    if (activity.Baggage.Any())
                    {
                        System.Console.WriteLine("Activity Baggage");
                        foreach (var baggage in activity.Baggage)
                        {
                            System.Console.WriteLine($"\t {baggage.Key} : {baggage.Value}");
                        }
                    }

                    System.Console.WriteLine("\n");
                }
            }

            return Task.FromResult(ExportResult.Success);
        }

        public override Task ShutdownAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}

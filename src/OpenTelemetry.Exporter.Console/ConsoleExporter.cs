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
#if NET461 || NETSTANDARD2_0
using OpenTelemetry.Logs;
#endif
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Exporter
{
    public class ConsoleExporter<T> : BaseExporter<T>
        where T : class
    {
        public ConsoleExporter(ConsoleExporterOptions options)
        {
        }

        public override ExportResult Export(in Batch<T> batch)
        {
            if (typeof(T) == typeof(Activity))
            {
                foreach (var item in batch)
                {
                    var activity = item as Activity;
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
#if NET461 || NETSTANDARD2_0
            else if (typeof(T) == typeof(LogRecord))
            {
                var rightPaddingLength = 30;
                foreach (var item in batch)
                {
                    var logRecord = item as LogRecord;
                    Console.WriteLine($"{"LogRecord.TraceId:".PadRight(rightPaddingLength)}{logRecord.TraceId}");
                    Console.WriteLine($"{"LogRecord.SpanId:".PadRight(rightPaddingLength)}{logRecord.SpanId}");
                    Console.WriteLine($"{"LogRecord.Timestamp:".PadRight(rightPaddingLength)}{logRecord.Timestamp:yyyy-MM-ddTHH:mm:ss.fffffffZ}");
                    Console.WriteLine($"{"LogRecord.EventId:".PadRight(rightPaddingLength)}{logRecord.EventId}");
                    Console.WriteLine($"{"LogRecord.CategoryName:".PadRight(rightPaddingLength)}{logRecord.CategoryName}");
                    Console.WriteLine($"{"LogRecord.LogLevel:".PadRight(rightPaddingLength)}{logRecord.LogLevel}");
                    Console.WriteLine($"{"LogRecord.TraceFlags:".PadRight(rightPaddingLength)}{logRecord.TraceFlags}");
                    Console.WriteLine($"{"LogRecord.State:".PadRight(rightPaddingLength)}{logRecord.State}");
                    if (logRecord.Exception is { })
                    {
                        Console.WriteLine($"{"LogRecord.Exception:".PadRight(rightPaddingLength)}{logRecord.Exception?.Message}");
                    }

                    Console.WriteLine();
                }
            }
#endif

            return ExportResult.Success;
        }
    }
}

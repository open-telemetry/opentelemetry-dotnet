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

namespace OpenTelemetry.Exporter
{
    public class ConsoleExporter<T> : BaseExporter<T>
        where T : class
    {
        private readonly ConsoleExporterOptions options;

        public ConsoleExporter(ConsoleExporterOptions options)
        {
            this.options = options ?? new ConsoleExporterOptions();
        }

        public override ExportResult Export(in Batch<T> batch)
        {
            if (typeof(T) == typeof(Activity))
            {
                foreach (var item in batch)
                {
                    var activity = item as Activity;
                    this.WriteLine($"Activity.Id:          {activity.Id}");
                    if (!string.IsNullOrEmpty(activity.ParentId))
                    {
                        this.WriteLine($"Activity.ParentId:    {activity.ParentId}");
                    }

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

                            Console.Write($"    {tag.Key}: [");

                            for (int i = 0; i < array.Length; i++)
                            {
                                Console.Write(i != 0 ? ", " : string.Empty);
                                Console.Write($"{array.GetValue(i)}");
                            }

                            this.WriteLine($"]");
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

                    if (activity.Baggage.Any())
                    {
                        this.WriteLine("Activity.Baggage:");
                        foreach (var baggage in activity.Baggage)
                        {
                            this.WriteLine($"    {baggage.Key}: {baggage.Value}");
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
            }
#if NET461 || NETSTANDARD2_0
            else if (typeof(T) == typeof(LogRecord))
            {
                var rightPaddingLength = 30;
                foreach (var item in batch)
                {
                    var logRecord = item as LogRecord;
                    this.WriteLine($"{"LogRecord.TraceId:".PadRight(rightPaddingLength)}{logRecord.TraceId}");
                    this.WriteLine($"{"LogRecord.SpanId:".PadRight(rightPaddingLength)}{logRecord.SpanId}");
                    this.WriteLine($"{"LogRecord.Timestamp:".PadRight(rightPaddingLength)}{logRecord.Timestamp:yyyy-MM-ddTHH:mm:ss.fffffffZ}");
                    this.WriteLine($"{"LogRecord.EventId:".PadRight(rightPaddingLength)}{logRecord.EventId}");
                    this.WriteLine($"{"LogRecord.CategoryName:".PadRight(rightPaddingLength)}{logRecord.CategoryName}");
                    this.WriteLine($"{"LogRecord.LogLevel:".PadRight(rightPaddingLength)}{logRecord.LogLevel}");
                    this.WriteLine($"{"LogRecord.TraceFlags:".PadRight(rightPaddingLength)}{logRecord.TraceFlags}");
                    this.WriteLine($"{"LogRecord.State:".PadRight(rightPaddingLength)}{logRecord.State}");
                    if (logRecord.Exception is { })
                    {
                        this.WriteLine($"{"LogRecord.Exception:".PadRight(rightPaddingLength)}{logRecord.Exception?.Message}");
                    }

                    this.WriteLine(string.Empty);
                }
            }
#endif

            return ExportResult.Success;
        }

        private void WriteLine(string message)
        {
            if (this.options.Targets.HasFlag(ConsoleExporterOutputTargets.Console))
            {
                Console.WriteLine(message);
            }

            if (this.options.Targets.HasFlag(ConsoleExporterOutputTargets.Debug))
            {
                Debug.WriteLine(message);
            }
        }
    }
}

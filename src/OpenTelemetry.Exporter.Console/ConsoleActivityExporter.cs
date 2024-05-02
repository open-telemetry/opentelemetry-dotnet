// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenTelemetry.Resources;

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
            var output = new
            {
                Activity = new
                {
                    TraceId = activity.TraceId.ToString(),
                    SpanId = activity.SpanId.ToString(),
                    TraceFlags = activity.ActivityTraceFlags,
                    TraceState = string.IsNullOrEmpty(activity.TraceStateString) ? null : activity.TraceStateString,
                    ParentSpanId = activity.ParentSpanId == default ? default : activity.ParentSpanId.ToString(),
                    ActivitySourceName = activity.Source.Name,
                    ActivitySourceVersion =
                        string.IsNullOrEmpty(activity.Source.Version) ? null : activity.Source.Version,
                    DisplayName = activity.DisplayName,
                    Kind = activity.Kind,
                    StartTime = activity.StartTimeUtc.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ"),
                    Duration = activity.Duration,
                    Tags = activity.TagObjects.Select(tag => new { Key = tag.Key, Value = tag.Value }).ToList(),
                    StatusCode = activity.Status != ActivityStatusCode.Unset ? activity.Status.ToString() : null,
                    StatusDescription =
                        !string.IsNullOrEmpty(activity.StatusDescription) ? activity.StatusDescription : null,
                    Events = activity.Events.Any()
                        ? null
                        : activity.Events.Select(e => new
                        {
                            Name = e.Name,
                            Timestamp = e.Timestamp,
                            Attributes = e.Tags.Select(a => new { Key = a.Key, Value = a.Value }).ToList(),
                        }).ToList(),
                    Links = activity.Links.Any()
                        ? null
                        : activity.Links.Select(l =>
                        {
                            if (l.Tags != null)
                            {
                                return new
                                {
                                    TraceId = l.Context.TraceId,
                                    SpanId = l.Context.SpanId,
                                    Attributes = l.Tags.Select(a => new { Key = a.Key, Value = a.Value }).ToList(),
                                };
                            }

                            return null;
                        }).ToList(),
                    Resource = this.ParentProvider.GetResource() != Resource.Empty
                        ? this.ParentProvider.GetResource().Attributes.Select(a => new { Key = a.Key, Value = a.Value })
                           .ToList()
                        : null,
                },
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
#if NETSTANDARD || NETFRAMEWORK
                IgnoreNullValues = true,
#else
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
#endif
                Converters = { new JsonStringEnumConverter(), },
            };

            var json = JsonSerializer.Serialize(output, options);
            this.WriteLine(json);
        }

        return ExportResult.Success;
    }
}

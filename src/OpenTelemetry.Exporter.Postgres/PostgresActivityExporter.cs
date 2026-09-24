// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text.Json;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter;

/// <summary>Exports activities to PostgreSQL.</summary>
public sealed class PostgresActivityExporter : PostgresExporter<Activity>
{
    /// <summary>Initializes a new instance of the <see cref="PostgresActivityExporter"/> class.</summary>
    public PostgresActivityExporter(PostgresExporterOptions options)
        : base(options)
    {
    }

    /// <inheritdoc />
    public override ExportResult Export(in Batch<Activity> batch)
    {
        try
        {
            using var connection = this.OpenConnection();
            using var transaction = connection.BeginTransaction();
            foreach (var activity in batch)
            {
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = $"INSERT INTO \"{this.SchemaName}\".spans (trace_id, span_id, parent_span_id, trace_state, name, kind, start_time, duration, status, status_description, flags, instrumentation_scope_name, instrumentation_scope_version, attributes, resource, data) VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14::jsonb, $15::jsonb, $16::jsonb)";
                command.Parameters.AddWithValue(activity.TraceId.ToHexString());
                command.Parameters.AddWithValue(activity.SpanId.ToHexString());
                command.Parameters.AddWithValue(activity.ParentSpanId == default ? (object)DBNull.Value : activity.ParentSpanId.ToHexString());
                command.Parameters.AddWithValue((object?)activity.TraceStateString ?? DBNull.Value);
                command.Parameters.AddWithValue(activity.DisplayName);
                command.Parameters.AddWithValue(activity.Kind.ToString());
                command.Parameters.AddWithValue(activity.StartTimeUtc);
                command.Parameters.AddWithValue(activity.Duration);
                command.Parameters.AddWithValue(activity.Status.ToString());
                command.Parameters.AddWithValue((object?)activity.StatusDescription ?? DBNull.Value);
                command.Parameters.AddWithValue((int)activity.ActivityTraceFlags);
                command.Parameters.AddWithValue((object?)activity.Source.Name ?? DBNull.Value);
                command.Parameters.AddWithValue((object?)activity.Source.Version ?? DBNull.Value);
                command.Parameters.AddWithValue(PostgresExporter<Activity>.SerializeAttributes(activity.TagObjects));
                command.Parameters.AddWithValue(this.SerializeResource());
                command.Parameters.AddWithValue(SerializeActivity(activity, PostgresOtlpJson.SerializeResource(this.ParentProvider.GetResource())));
                command.ExecuteNonQuery();
            }

            transaction.Commit();
            return ExportResult.Success;
        }
        catch
        {
            return ExportResult.Failure;
        }
    }

    private static string SerializeActivity(Activity activity, string resource)
    {
        var events = new List<object>();
        foreach (ref readonly var activityEvent in activity.EnumerateEvents())
        {
            events.Add(new
            {
                name = activityEvent.Name,
                timeUnixNano = PostgresOtlpJson.ToUnixTimeNanoseconds(activityEvent.Timestamp),
                attributes = PostgresOtlpJson.ToJsonElement(SerializeAttributes(activityEvent.EnumerateTagObjects())),
            });
        }

        var links = new List<object>();
        foreach (ref readonly var link in activity.EnumerateLinks())
        {
            links.Add(new
            {
                traceId = link.Context.TraceId.ToHexString(),
                spanId = link.Context.SpanId.ToHexString(),
                traceState = link.Context.TraceState,
                attributes = PostgresOtlpJson.ToJsonElement(SerializeAttributes(link.EnumerateTagObjects())),
            });
        }

        return JsonSerializer.Serialize(new
        {
            traceId = activity.TraceId.ToHexString(),
            spanId = activity.SpanId.ToHexString(),
            parentSpanId = activity.ParentSpanId == default ? null : activity.ParentSpanId.ToHexString(),
            traceState = activity.TraceStateString,
            flags = (int)activity.ActivityTraceFlags,
            name = activity.DisplayName,
            kind = activity.Kind.ToString(),
            startTimeUnixNano = PostgresOtlpJson.ToUnixTimeNanoseconds(activity.StartTimeUtc),
            endTimeUnixNano = PostgresOtlpJson.ToUnixTimeNanoseconds(activity.StartTimeUtc + activity.Duration),
            status = new { code = activity.Status.ToString(), message = activity.StatusDescription },
            attributes = PostgresOtlpJson.ToJsonElement(PostgresOtlpJson.SerializeAttributes(activity.TagObjects)),
            events,
            links,
            resource = JsonSerializer.Deserialize<JsonElement>(resource),
            scope = new { name = activity.Source.Name, version = activity.Source.Version },
        });
    }

    private static string SerializeAttributes(Activity.Enumerator<KeyValuePair<string, object?>> attributes)
    {
        var values = new List<KeyValuePair<string, object?>>();
        while (attributes.MoveNext())
        {
            values.Add(attributes.Current);
        }

        return PostgresOtlpJson.SerializeAttributes(values);
    }
}

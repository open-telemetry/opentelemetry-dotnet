// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Logs;
using System.Collections;
using System.Globalization;
using System.Text.Json;

namespace OpenTelemetry.Exporter;

/// <summary>Exports log records to PostgreSQL.</summary>
public sealed class PostgresLogRecordExporter : PostgresExporter<LogRecord>
{
    /// <summary>Initializes a new instance of the <see cref="PostgresLogRecordExporter"/> class.</summary>
    public PostgresLogRecordExporter(PostgresExporterOptions options)
        : base(options)
    {
    }

    /// <inheritdoc />
    public override ExportResult Export(in Batch<LogRecord> batch)
    {
        try
        {
            using var connection = this.OpenConnection();
            using var transaction = connection.BeginTransaction();
            foreach (var logRecord in batch)
            {
                var timeUnixNano = ToUnixTimeNanoseconds(logRecord.Timestamp);
                var observedTimeUnixNano = ToUnixTimeNanoseconds(logRecord.ObservedTimestamp);
                var attributes = GetOtlpAttributes(logRecord);
                var body = SerializeAnyValue(GetBody(logRecord));
                var scope = logRecord.Logger;

                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = $"INSERT INTO \"{this.SchemaName}\".logs (timestamp, observed_timestamp, time_unix_nano, observed_time_unix_nano, trace_id, span_id, flags, severity, severity_number, severity_text, category, event_name, instrumentation_scope_name, instrumentation_scope_version, schema_url, dropped_attributes_count, body, body_json, attributes, resource, data) VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18::jsonb, $19::jsonb, $20::jsonb, $21::jsonb)";
                command.Parameters.AddWithValue(logRecord.Timestamp == DateTime.MinValue ? logRecord.ObservedTimestamp : logRecord.Timestamp);
                command.Parameters.AddWithValue(logRecord.ObservedTimestamp);
                command.Parameters.AddWithValue(timeUnixNano);
                command.Parameters.AddWithValue(observedTimeUnixNano);
                command.Parameters.AddWithValue(logRecord.TraceId == default ? (object)DBNull.Value : logRecord.TraceId.ToHexString());
                command.Parameters.AddWithValue(logRecord.SpanId == default ? (object)DBNull.Value : logRecord.SpanId.ToHexString());
                command.Parameters.AddWithValue((int)logRecord.TraceFlags);
                command.Parameters.AddWithValue(logRecord.Severity?.ToString() ?? (object)DBNull.Value);
                command.Parameters.AddWithValue(logRecord.Severity.HasValue ? (object)(int)logRecord.Severity.Value : DBNull.Value);
                command.Parameters.AddWithValue((object?)logRecord.SeverityText ?? DBNull.Value);
                command.Parameters.AddWithValue(logRecord.CategoryName ?? (object)DBNull.Value);
                command.Parameters.AddWithValue((object?)logRecord.EventId.Name ?? DBNull.Value);
                command.Parameters.AddWithValue((object?)scope.Name ?? DBNull.Value);
                command.Parameters.AddWithValue((object?)scope.Version ?? DBNull.Value);
                command.Parameters.AddWithValue(this.ParentProvider.GetResource().SchemaUrl ?? (object)DBNull.Value);
                command.Parameters.AddWithValue(0);
                command.Parameters.AddWithValue(GetBody(logRecord)?.ToString() ?? (object)DBNull.Value);
                command.Parameters.AddWithValue(body ?? "null");
                command.Parameters.AddWithValue(PostgresExporter<LogRecord>.SerializeAttributes(attributes));
                var resource = this.SerializeResource();
                command.Parameters.AddWithValue(resource);
                command.Parameters.AddWithValue(SerializeLogRecord(logRecord, timeUnixNano, observedTimeUnixNano, (scope.Name, scope.Version), resource, this.ParentProvider.GetResource().SchemaUrl, attributes, body));
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

    private static string SerializeLogRecord(
        LogRecord logRecord,
        long timeUnixNano,
        long observedTimeUnixNano,
        (string Name, string? Version) scope,
        string resource,
        string? resourceSchemaUrl,
        IReadOnlyList<KeyValuePair<string, object?>> attributes,
        string? body)
        => JsonSerializer.Serialize(new
        {
            timeUnixNano = timeUnixNano.ToString(CultureInfo.InvariantCulture),
            observedTimeUnixNano = observedTimeUnixNano.ToString(CultureInfo.InvariantCulture),
            severityNumber = logRecord.Severity,
            severityText = logRecord.SeverityText,
            body = body is null ? (object?)null : JsonSerializer.Deserialize<JsonElement>(body),
            traceId = logRecord.TraceId == default ? null : logRecord.TraceId.ToHexString(),
            spanId = logRecord.SpanId == default ? null : logRecord.SpanId.ToHexString(),
            flags = logRecord.TraceId == default || logRecord.SpanId == default ? null : (int?)logRecord.TraceFlags,
            eventName = logRecord.EventId.Name,
            droppedAttributesCount = 0,
            attributes = attributes.Select(static attribute => new
            {
                key = attribute.Key,
                value = JsonSerializer.Deserialize<JsonElement>(SerializeAnyValue(attribute.Value)),
            }),
            resource = new
            {
                attributes = JsonSerializer.Deserialize<JsonElement>(resource),
                schemaUrl = resourceSchemaUrl,
            },
            scope = new { name = scope.Name, version = scope.Version },
        });

    private static object? GetBody(LogRecord logRecord)
    {
        if (logRecord.FormattedMessage != null)
        {
            return logRecord.FormattedMessage;
        }

        var originalFormat = (logRecord.Attributes ?? Array.Empty<KeyValuePair<string, object?>>())
            .FirstOrDefault(static attribute => string.Equals(attribute.Key, "{OriginalFormat}", StringComparison.Ordinal)).Value;
        if (originalFormat is string)
        {
            return originalFormat;
        }

        return logRecord.Body;
    }

    private static List<KeyValuePair<string, object?>> GetOtlpAttributes(LogRecord logRecord)
    {
        var attributes = (logRecord.Attributes ?? Array.Empty<KeyValuePair<string, object?>>()).ToList();
        logRecord.ForEachScope(static (scope, state) =>
        {
            foreach (var item in scope)
            {
                if (!string.IsNullOrEmpty(item.Key) && !string.Equals(item.Key, "{OriginalFormat}", StringComparison.Ordinal))
                {
                    state.Add(item);
                }
            }
        }, attributes);

        if (logRecord.Exception != null)
        {
            attributes.Add(new("exception.type", logRecord.Exception.GetType().Name));
            attributes.Add(new("exception.message", logRecord.Exception.Message));
            attributes.Add(new("exception.stacktrace", logRecord.Exception.ToString()));
        }

        return attributes;
    }

    private static string? SerializeAnyValue(object? value)
    {
        if (value is null)
        {
            return null;
        }

        object result = value switch
        {
            string text => new { stringValue = text },
            bool boolean => new { boolValue = boolean },
            byte[] bytes => new { bytesValue = Convert.ToBase64String(bytes) },
            byte or sbyte or short or ushort or int or uint or long or ulong => new { intValue = Convert.ToString(value, CultureInfo.InvariantCulture) },
            float or double or decimal => new { doubleValue = Convert.ToDouble(value, CultureInfo.InvariantCulture) },
            IEnumerable enumerable when value is not string => new { arrayValue = new { values = enumerable.Cast<object?>().Select(static item => SerializeAnyValue(item)).Select(static item => item is null ? (JsonElement?)null : JsonSerializer.Deserialize<JsonElement>(item)) } },
            _ => new { stringValue = value.ToString() },
        };

        return JsonSerializer.Serialize(result);
    }

    private static long ToUnixTimeNanoseconds(DateTime timestamp)
        => timestamp == DateTime.MinValue
            ? 0
            : checked((timestamp.ToUniversalTime().Ticks - DateTime.UnixEpoch.Ticks) * 100);
}

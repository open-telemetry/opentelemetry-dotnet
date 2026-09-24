// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Npgsql;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter;

public abstract class PostgresExporter<T> : BaseExporter<T>
    where T : class
{
    protected PostgresExporter(PostgresExporterOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            throw new ArgumentException("A PostgreSQL connection string is required.", nameof(options));
        }

        this.Options = options;
        this.SchemaName = ValidateIdentifier(options.SchemaName);
    }

    protected PostgresExporterOptions Options { get; }

    protected string SchemaName { get; }

    protected NpgsqlConnection OpenConnection()
    {
        var connection = new NpgsqlConnection(this.Options.ConnectionString);
        connection.Open();
        if (this.Options.CreateSchema)
        {
            this.EnsureSchema(connection);
        }

        return connection;
    }

    protected static string SerializeAttributes(IEnumerable<KeyValuePair<string, object?>> attributes)
        => JsonSerializer.Serialize(attributes.ToDictionary(static x => x.Key, static x => x.Value));

    protected string SerializeResource()
    {
        var resource = this.ParentProvider.GetResource();
        return resource == Resource.Empty
            ? "{}"
            : JsonSerializer.Serialize(resource.Attributes.ToDictionary(static x => x.Key, static x => x.Value));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
    }

    private void EnsureSchema(NpgsqlConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            CREATE SCHEMA IF NOT EXISTS "{this.SchemaName}";
            CREATE TABLE IF NOT EXISTS "{this.SchemaName}".spans (
                id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                trace_id TEXT NOT NULL,
                span_id TEXT NOT NULL,
                parent_span_id TEXT,
                trace_state TEXT,
                name TEXT NOT NULL,
                kind TEXT NOT NULL,
                start_time TIMESTAMPTZ NOT NULL,
                duration INTERVAL NOT NULL,
                status TEXT NOT NULL,
                status_description TEXT,
                flags INTEGER NOT NULL DEFAULT 0,
                instrumentation_scope_name TEXT,
                instrumentation_scope_version TEXT,
                attributes JSONB NOT NULL,
                resource JSONB NOT NULL,
                data JSONB NOT NULL DEFAULT jsonb_build_object()
            );
            CREATE TABLE IF NOT EXISTS "{this.SchemaName}".logs (
                id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                timestamp TIMESTAMPTZ NOT NULL,
                observed_timestamp TIMESTAMPTZ NOT NULL,
                time_unix_nano BIGINT NOT NULL DEFAULT 0,
                observed_time_unix_nano BIGINT NOT NULL DEFAULT 0,
                trace_id TEXT,
                span_id TEXT,
                flags INTEGER NOT NULL DEFAULT 0,
                severity TEXT,
                severity_number SMALLINT,
                severity_text TEXT,
                category TEXT,
                event_name TEXT,
                instrumentation_scope_name TEXT,
                instrumentation_scope_version TEXT,
                schema_url TEXT,
                dropped_attributes_count INTEGER NOT NULL DEFAULT 0,
                body TEXT,
                body_json JSONB,
                attributes JSONB NOT NULL,
                resource JSONB NOT NULL,
                data JSONB NOT NULL DEFAULT jsonb_build_object()
            );
            CREATE TABLE IF NOT EXISTS "{this.SchemaName}".metrics (
                id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                name TEXT NOT NULL,
                metric_type TEXT NOT NULL,
                aggregation_temporality TEXT NOT NULL,
                instrumentation_scope_name TEXT,
                instrumentation_scope_version TEXT,
                start_time TIMESTAMPTZ NOT NULL,
                end_time TIMESTAMPTZ NOT NULL,
                value DOUBLE PRECISION,
                attributes JSONB NOT NULL,
                resource JSONB NOT NULL,
                data JSONB NOT NULL DEFAULT jsonb_build_object()
            );
            ALTER TABLE "{this.SchemaName}".spans ADD COLUMN IF NOT EXISTS trace_state TEXT;
            ALTER TABLE "{this.SchemaName}".spans ADD COLUMN IF NOT EXISTS status_description TEXT;
            ALTER TABLE "{this.SchemaName}".spans ADD COLUMN IF NOT EXISTS flags INTEGER NOT NULL DEFAULT 0;
            ALTER TABLE "{this.SchemaName}".spans ADD COLUMN IF NOT EXISTS instrumentation_scope_name TEXT;
            ALTER TABLE "{this.SchemaName}".spans ADD COLUMN IF NOT EXISTS instrumentation_scope_version TEXT;
            ALTER TABLE "{this.SchemaName}".spans ADD COLUMN IF NOT EXISTS data JSONB NOT NULL DEFAULT jsonb_build_object();
            ALTER TABLE "{this.SchemaName}".logs ADD COLUMN IF NOT EXISTS severity_text TEXT;
            ALTER TABLE "{this.SchemaName}".logs ADD COLUMN IF NOT EXISTS event_name TEXT;
            ALTER TABLE "{this.SchemaName}".logs ADD COLUMN IF NOT EXISTS time_unix_nano BIGINT NOT NULL DEFAULT 0;
            ALTER TABLE "{this.SchemaName}".logs ADD COLUMN IF NOT EXISTS observed_time_unix_nano BIGINT NOT NULL DEFAULT 0;
            ALTER TABLE "{this.SchemaName}".logs ADD COLUMN IF NOT EXISTS flags INTEGER NOT NULL DEFAULT 0;
            ALTER TABLE "{this.SchemaName}".logs ADD COLUMN IF NOT EXISTS severity_number SMALLINT;
            ALTER TABLE "{this.SchemaName}".logs ADD COLUMN IF NOT EXISTS instrumentation_scope_name TEXT;
            ALTER TABLE "{this.SchemaName}".logs ADD COLUMN IF NOT EXISTS instrumentation_scope_version TEXT;
            ALTER TABLE "{this.SchemaName}".logs ADD COLUMN IF NOT EXISTS schema_url TEXT;
            ALTER TABLE "{this.SchemaName}".logs ADD COLUMN IF NOT EXISTS dropped_attributes_count INTEGER NOT NULL DEFAULT 0;
            ALTER TABLE "{this.SchemaName}".logs ADD COLUMN IF NOT EXISTS body_json JSONB;
            ALTER TABLE "{this.SchemaName}".logs ADD COLUMN IF NOT EXISTS data JSONB NOT NULL DEFAULT jsonb_build_object();
            ALTER TABLE "{this.SchemaName}".metrics ADD COLUMN IF NOT EXISTS aggregation_temporality TEXT NOT NULL DEFAULT 'Cumulative';
            ALTER TABLE "{this.SchemaName}".metrics ADD COLUMN IF NOT EXISTS instrumentation_scope_name TEXT;
            ALTER TABLE "{this.SchemaName}".metrics ADD COLUMN IF NOT EXISTS instrumentation_scope_version TEXT;
            ALTER TABLE "{this.SchemaName}".metrics ADD COLUMN IF NOT EXISTS data JSONB NOT NULL DEFAULT jsonb_build_object();
            CREATE INDEX IF NOT EXISTS "{this.SchemaName}_logs_observed_timestamp_idx"
                ON "{this.SchemaName}".logs (observed_timestamp DESC);
            CREATE INDEX IF NOT EXISTS "{this.SchemaName}_logs_trace_id_idx"
                ON "{this.SchemaName}".logs (trace_id)
                WHERE trace_id IS NOT NULL;
            CREATE INDEX IF NOT EXISTS "{this.SchemaName}_logs_severity_idx"
                ON "{this.SchemaName}".logs (severity_number, observed_timestamp DESC)
                WHERE severity_number IS NOT NULL;
            CREATE INDEX IF NOT EXISTS "{this.SchemaName}_logs_service_name_idx"
                ON "{this.SchemaName}".logs ((resource->>'service.name'), observed_timestamp DESC);
            CREATE INDEX IF NOT EXISTS "{this.SchemaName}_logs_scope_idx"
                ON "{this.SchemaName}".logs (instrumentation_scope_name, instrumentation_scope_version);
            CREATE INDEX IF NOT EXISTS "{this.SchemaName}_logs_event_name_idx"
                ON "{this.SchemaName}".logs (event_name)
                WHERE event_name IS NOT NULL;
            """;
        command.ExecuteNonQuery();
    }

    private static string ValidateIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier) || identifier.Any(static c => !(char.IsLetterOrDigit(c) || c == '_')))
        {
            throw new ArgumentException("SchemaName must contain only letters, digits, and underscores.", nameof(identifier));
        }

        return identifier;
    }
}

// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using Npgsql;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Exporter.Postgres.Tests;

public sealed class PostgresExporterIntegrationTests
{
    private const string SchemaName = "otel_test";
    private static readonly string? ConnectionString = Environment.GetEnvironmentVariable("OTEL_POSTGRES_CONNECTION_STRING");

    [SkipUnlessEnvVarFoundFact("OTEL_POSTGRES_CONNECTION_STRING")]
    public void ExportsTrace()
    {
        var sourceName = nameof(this.ExportsTrace);
        using var source = new ActivitySource(sourceName);
        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddSource(sourceName)
            .SetResourceBuilder(ResourceBuilder.CreateEmpty().AddService("postgres.integration", serviceVersion: "1.0.0"))
            .AddPostgresExporter(options => Configure(options))
            .Build();

        using (var activity = source.StartActivity("postgres.integration.trace"))
        {
            activity!.SetTag("test.key", "test.value");
            activity.AddEvent(new ActivityEvent("postgres.integration.event", tags: new ActivityTagsCollection
            {
                ["event.key"] = "event.value",
            }));
        }

        Assert.True(provider.ForceFlush());
        Assert.Equal(1, ExecuteScalar("SELECT count(*) FROM otel_test.spans WHERE name = 'postgres.integration.trace'"));
        Assert.Equal("179", ExecuteString("SELECT left(data->>'startTimeUnixNano', 3) FROM otel_test.spans WHERE name = 'postgres.integration.trace'"));
        Assert.Equal("postgres.integration", ExecuteString("SELECT data->'resource'->'attributes'->>'service.name' FROM otel_test.spans WHERE name = 'postgres.integration.trace'"));
        Assert.Equal(sourceName, ExecuteString("SELECT data->'scope'->>'name' FROM otel_test.spans WHERE name = 'postgres.integration.trace'"));
        Assert.Equal("test.value", ExecuteString("SELECT value->>'stringValue' FROM otel_test.spans, jsonb_array_elements(data->'attributes') AS attribute WHERE name = 'postgres.integration.trace' AND attribute->>'key' = 'test.key'"));
        Assert.Equal("postgres.integration.event", ExecuteString("SELECT data->'events'->0->>'name' FROM otel_test.spans WHERE name = 'postgres.integration.trace'"));
        Assert.Equal("event.value", ExecuteString("SELECT value->>'stringValue' FROM otel_test.spans, jsonb_array_elements(data->'events'->0->'attributes') AS attribute WHERE name = 'postgres.integration.trace' AND attribute->>'key' = 'event.key'"));
    }

    [SkipUnlessEnvVarFoundFact("OTEL_POSTGRES_CONNECTION_STRING")]
    public void ExportsLog()
    {
        using (var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddOpenTelemetry(options =>
            {
                options.SetResourceBuilder(ResourceBuilder.CreateEmpty().AddService("postgres.integration", serviceVersion: "1.0.0"));
                options.AddPostgresExporter(Configure);
            });
        }))
        {
            var logger = loggerFactory.CreateLogger<PostgresExporterIntegrationTests>();
            using (logger.BeginScope(new Dictionary<string, object?> { ["request.id"] = "request-123" }))
            {
                logger.Log(
                    LogLevel.Information,
                    new EventId(42, "BookingCreated"),
                    (Exception?)null,
                    "postgres.integration.log {BookingId}",
                    "booking-123");
            }
        }

        Assert.Equal(1, ExecuteScalar("SELECT count(*) FROM otel_test.logs WHERE body = 'postgres.integration.log booking-123'"));
        Assert.Equal("postgres.integration.log booking-123", ExecuteString("SELECT data->'body'->>'stringValue' FROM otel_test.logs WHERE body = 'postgres.integration.log booking-123'"));
        Assert.Equal("postgres.integration", ExecuteString("SELECT data->'resource'->'attributes'->>'service.name' FROM otel_test.logs WHERE body = 'postgres.integration.log booking-123'"));
        Assert.Equal("PostgresExporterIntegrationTests", ExecuteString("SELECT data->'scope'->>'name' FROM otel_test.logs WHERE body = 'postgres.integration.log booking-123'"));
        Assert.Equal("BookingCreated", ExecuteString("SELECT data->>'eventName' FROM otel_test.logs WHERE body = 'postgres.integration.log booking-123'"));
        Assert.Equal("booking-123", ExecuteString("SELECT value->>'stringValue' FROM otel_test.logs, jsonb_array_elements(data->'attributes') AS attribute WHERE body = 'postgres.integration.log booking-123' AND attribute->>'key' = 'BookingId'"));
        Assert.Equal("request-123", ExecuteString("SELECT value->>'stringValue' FROM otel_test.logs, jsonb_array_elements(data->'attributes') AS attribute WHERE body = 'postgres.integration.log booking-123' AND attribute->>'key' = 'request.id'"));
        Assert.Equal(6, ExecuteScalar("SELECT count(*) FROM pg_indexes WHERE schemaname = 'otel_test' AND indexname IN ('otel_test_logs_observed_timestamp_idx', 'otel_test_logs_trace_id_idx', 'otel_test_logs_severity_idx', 'otel_test_logs_service_name_idx', 'otel_test_logs_scope_idx', 'otel_test_logs_event_name_idx')"));
    }

    [SkipUnlessEnvVarFoundFact("OTEL_POSTGRES_CONNECTION_STRING")]
    public void ExportsMetric()
    {
        var meterName = nameof(this.ExportsMetric);
        using var meter = new Meter(meterName);
        var counter = meter.CreateCounter<long>("postgres.integration.metric");
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meterName)
            .SetResourceBuilder(ResourceBuilder.CreateEmpty().AddService("postgres.integration", serviceVersion: "1.0.0"))
            .AddPostgresExporter(options => Configure(options))
            .Build();

        counter.Add(7);

        Assert.True(provider.ForceFlush());
        Assert.Equal(1, ExecuteScalar("SELECT count(*) FROM otel_test.metrics WHERE name = 'postgres.integration.metric'"));
        Assert.Equal("7", ExecuteString("SELECT data->'dataPoints'->0->>'asInt' FROM otel_test.metrics WHERE name = 'postgres.integration.metric'"));
        Assert.Equal("179", ExecuteString("SELECT left(data->'dataPoints'->0->>'timeUnixNano', 3) FROM otel_test.metrics WHERE name = 'postgres.integration.metric'"));
        Assert.Equal(meterName, ExecuteString("SELECT data->'scope'->>'name' FROM otel_test.metrics WHERE name = 'postgres.integration.metric'"));
        Assert.Equal("postgres.integration", ExecuteString("SELECT data->'resource'->'attributes'->>'service.name' FROM otel_test.metrics WHERE name = 'postgres.integration.metric'"));
    }

    private static void Configure(PostgresExporterOptions options)
    {
        options.ConnectionString = ConnectionString!;
        options.SchemaName = SchemaName;
    }

    private static long ExecuteScalar(string sql)
    {
        using var connection = new NpgsqlConnection(ConnectionString);
        connection.Open();
        using var command = new NpgsqlCommand(sql, connection);
        return (long)command.ExecuteScalar()!;
    }

    private static string ExecuteString(string sql)
    {
        using var connection = new NpgsqlConnection(ConnectionString);
        connection.Open();
        using var command = new NpgsqlCommand(sql, connection);
        return (string)command.ExecuteScalar()!;
    }
}

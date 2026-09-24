# PostgreSQL Exporter for OpenTelemetry .NET

PostgreSQL exporter for OpenTelemetry .NET. It stores traces, logs, and metrics
in PostgreSQL tables using queryable columns and OTLP-shaped `JSONB` documents.

## Prerequisite

* A PostgreSQL database reachable by the application.

## Installation

```shell
dotnet add package OpenTelemetry.Exporter.Postgres
```

The exporter depends on [Npgsql](https://www.npgsql.org/) for PostgreSQL
connectivity.

## Enable the exporter

The exporter can be enabled independently for each signal. The following
example enables all three signals with the same database configuration:

```csharp
var connectionString =
    "Host=localhost;Port=5432;Database=opentelemetry;Username=postgres;Password=postgres";

services.AddOpenTelemetry()
    .WithLogging(builder => builder.AddPostgresExporter(options =>
    {
        options.ConnectionString = connectionString;
        options.SchemaName = "otel";
    }))
    .WithMetrics(builder => builder.AddPostgresExporter(options =>
    {
        options.ConnectionString = connectionString;
        options.SchemaName = "otel";
    }))
    .WithTracing(builder => builder.AddPostgresExporter(options =>
    {
        options.ConnectionString = connectionString;
        options.SchemaName = "otel";
    }));
```

### Logs

```csharp
LoggerFactory.Create(builder =>
{
    builder.AddOpenTelemetry(options =>
    {
        options.AddPostgresExporter(exporterOptions =>
            exporterOptions.ConnectionString = connectionString);
    });
});
```

The `logs` table stores one row per log record.

### Metrics

```csharp
Sdk.CreateMeterProviderBuilder()
    .AddMeter("MyService")
    .AddPostgresExporter(options => options.ConnectionString = connectionString)
    .Build();
```

The `metrics` table stores one row per metric data point.

### Traces

```csharp
Sdk.CreateTracerProviderBuilder()
    .AddSource("MyService")
    .AddPostgresExporter(options => options.ConnectionString = connectionString)
    .Build();
```

The `spans` table stores one row per span. Spans belonging to the same trace
are correlated using `trace_id`.

## Configuration

Configure the exporter with `PostgresExporterOptions`:

* `ConnectionString`: PostgreSQL connection string. The default is
  `Host=localhost;Port=5432;Database=opentelemetry;Username=postgres;Password=postgres`.
* `SchemaName`: Schema containing exporter tables. The default is `otel`.
* `CreateSchema`: Creates the schema, tables, and indexes when `true`. The
  default is `true`. Set it to `false` when migrations are managed separately.

The schema name is validated as a PostgreSQL identifier before it is used in
SQL statements.

## PostgreSQL storage model

The exporter creates these tables by default:

| Table | Stored data |
| --- | --- |
| `otel.logs` | One row per log record |
| `otel.metrics` | One row per metric data point |
| `otel.spans` | One row per span |

Each table contains scalar columns for common filters and a `data` `JSONB`
column containing the signal-specific OTLP logical structure. The documents
preserve typed values, attributes, resource information, instrumentation scope,
and signal-specific fields such as span events, span links, log bodies, and
metric data points.

The exporter stores the OTLP logical model, not OTLP protobuf wire bytes. It
also omits transport-level wrappers such as `resourceSpans`, `scopeLogs`, and
`resourceMetrics`; resource and scope information remains on each row so rows
can be queried independently.

## Indexes

The exporter creates indexes for common log queries, including timestamp, trace
correlation, severity, service name, instrumentation scope, and event name.
Use scalar columns for frequent filters. For known production query dimensions,
add targeted PostgreSQL expression indexes over attributes in `data`.

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
* [OpenTelemetry Protocol](https://opentelemetry.io/docs/specs/otlp/)
* [Npgsql](https://www.npgsql.org/)

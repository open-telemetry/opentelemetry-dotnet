// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter;

/// <summary>Options for the PostgreSQL exporter.</summary>
public sealed class PostgresExporterOptions
{
    /// <summary>Gets or sets the PostgreSQL connection string.</summary>
    public string ConnectionString { get; set; } = "Host=localhost;Port=5432;Database=opentelemetry;Username=postgres;Password=postgres";

    /// <summary>Gets or sets the PostgreSQL schema used for exporter tables.</summary>
    public string SchemaName { get; set; } = "otel";

    /// <summary>Gets or sets whether tables are created automatically.</summary>
    public bool CreateSchema { get; set; } = true;
}

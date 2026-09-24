// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Npgsql;
using System.Text.Json;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Exporter;

/// <summary>Exports metric points to PostgreSQL.</summary>
public sealed class PostgresMetricExporter : PostgresExporter<Metric>
{
    /// <summary>Initializes a new instance of the <see cref="PostgresMetricExporter"/> class.</summary>
    public PostgresMetricExporter(PostgresExporterOptions options)
        : base(options)
    {
    }

    /// <inheritdoc />
    public override ExportResult Export(in Batch<Metric> batch)
    {
        try
        {
            using var connection = this.OpenConnection();
            using var transaction = connection.BeginTransaction();
            foreach (var metric in batch)
            {
                foreach (ref readonly var point in metric.GetMetricPoints())
                {
                    using var command = connection.CreateCommand();
                    command.Transaction = transaction;
                    command.CommandText = $"INSERT INTO \"{this.SchemaName}\".metrics (name, metric_type, aggregation_temporality, instrumentation_scope_name, instrumentation_scope_version, start_time, end_time, value, attributes, resource, data) VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9::jsonb, $10::jsonb, $11::jsonb)";
                    command.Parameters.AddWithValue(metric.Name);
                    command.Parameters.AddWithValue(metric.MetricType.ToString());
                    command.Parameters.AddWithValue(metric.Temporality.ToString());
                    command.Parameters.AddWithValue((object?)metric.MeterName ?? DBNull.Value);
                    command.Parameters.AddWithValue((object?)metric.MeterVersion ?? DBNull.Value);
                    command.Parameters.AddWithValue(point.StartTime);
                    command.Parameters.AddWithValue(point.EndTime);
                    command.Parameters.AddWithValue(GetValue(metric.MetricType, in point) ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue(SerializeTags(point.Tags));
                    command.Parameters.AddWithValue(this.SerializeResource());
                    command.Parameters.AddWithValue(SerializeMetric(metric, in point, PostgresOtlpJson.SerializeResource(this.ParentProvider.GetResource())));
                    command.ExecuteNonQuery();
                }
            }

            transaction.Commit();
            return ExportResult.Success;
        }
        catch
        {
            return ExportResult.Failure;
        }
    }

    private static double? GetValue(MetricType metricType, in MetricPoint point)
    {
        if (metricType.IsDouble())
        {
            return metricType.IsSum() ? point.GetSumDouble() : point.GetGaugeLastValueDouble();
        }

        if (metricType.IsLong())
        {
            return metricType.IsSum() ? point.GetSumLong() : point.GetGaugeLastValueLong();
        }

        return metricType is MetricType.Histogram or MetricType.ExponentialHistogram
            ? point.GetHistogramSum()
            : null;
    }

    private static string SerializeTags(ReadOnlyTagCollection tags)
    {
        var values = new List<KeyValuePair<string, object?>>();
        foreach (var tag in tags)
        {
            values.Add(tag);
        }

        return PostgresOtlpJson.SerializeAttributes(values);
    }

    private static string SerializeMetric(Metric metric, in MetricPoint point, string resource)
    {
        var dataPoint = new Dictionary<string, object?>
        {
            ["startTimeUnixNano"] = PostgresOtlpJson.ToUnixTimeNanoseconds(point.StartTime).ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["timeUnixNano"] = PostgresOtlpJson.ToUnixTimeNanoseconds(point.EndTime).ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["attributes"] = PostgresOtlpJson.ToJsonElement(SerializeTags(point.Tags)),
        };

        if (metric.MetricType.IsDouble())
        {
            dataPoint["asDouble"] = metric.MetricType.IsSum() ? point.GetSumDouble() : point.GetGaugeLastValueDouble();
        }
        else if (metric.MetricType.IsLong())
        {
            dataPoint["asInt"] = (metric.MetricType.IsSum() ? point.GetSumLong() : point.GetGaugeLastValueLong()).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        else if (metric.MetricType is MetricType.Histogram or MetricType.ExponentialHistogram)
        {
            dataPoint["count"] = point.GetHistogramCount().ToString(System.Globalization.CultureInfo.InvariantCulture);
            dataPoint["sum"] = point.GetHistogramSum();
            if (point.TryGetHistogramMinMaxValues(out var min, out var max))
            {
                dataPoint["min"] = min;
                dataPoint["max"] = max;
            }

            if (metric.MetricType == MetricType.Histogram)
            {
                var bucketCounts = new List<string>();
                var explicitBounds = new List<double>();
                foreach (var bucket in point.GetHistogramBuckets())
                {
                    explicitBounds.Add(bucket.ExplicitBound);
                    bucketCounts.Add(bucket.BucketCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
                }

                dataPoint["bucketCounts"] = bucketCounts;
                dataPoint["explicitBounds"] = explicitBounds;
            }
            else
            {
                var exponentialData = point.GetExponentialHistogramData();
                dataPoint["scale"] = exponentialData.Scale;
                dataPoint["zeroCount"] = exponentialData.ZeroCount;
                dataPoint["positive"] = new
                {
                    offset = exponentialData.PositiveBuckets.Offset,
                    bucketCounts = GetBucketCounts(exponentialData.PositiveBuckets),
                };
            }
        }

        return JsonSerializer.Serialize(new
        {
            name = metric.Name,
            description = metric.Description,
            unit = metric.Unit,
            type = metric.MetricType.ToString(),
            aggregationTemporality = metric.Temporality.ToString(),
            scope = new { name = metric.MeterName, version = metric.MeterVersion },
            schemaUrl = metric.MeterSchemaUrl,
            resource = JsonSerializer.Deserialize<JsonElement>(resource),
            dataPoints = new[] { dataPoint },
        });
    }

    private static List<long> GetBucketCounts(ExponentialHistogramBuckets buckets)
    {
        var counts = new List<long>();
        foreach (var count in buckets)
        {
            counts.Add(count);
        }

        return counts;
    }
}

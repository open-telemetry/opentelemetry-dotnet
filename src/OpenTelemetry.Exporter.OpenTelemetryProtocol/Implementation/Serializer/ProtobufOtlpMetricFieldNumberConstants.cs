// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;

/// <summary>
/// Defines field number constants for fields defined in
/// <see href="https://github.com/open-telemetry/opentelemetry-proto/blob/v1.2.0/opentelemetry/proto/metrics/v1/metrics.proto"/>.
/// </summary>
internal static class ProtobufOtlpMetricFieldNumberConstants
{
    // Metrics Data
    internal const int MetricsData_Resource_Metrics = 1;

    // Resource Metrics
    internal const int ResourceMetrics_Resource = 1;
    internal const int ResourceMetrics_Scope_Metrics = 2;
    internal const int ResourceMetrics_Schema_Url = 3;

    // Scope Metrics
    internal const int ScopeMetrics_Scope = 1;
    internal const int ScopeMetrics_Metrics = 2;
    internal const int ScopeMetrics_Schema_Url = 3;

    // Metric
    internal const int Metric_Name = 1;
    internal const int Metric_Description = 2;
    internal const int Metric_Unit = 3;
    internal const int Metric_Data_Gauge = 5;
    internal const int Metric_Data_Sum = 7;
    internal const int Metric_Data_Histogram = 9;
    internal const int Metric_Data_Exponential_Histogram = 10;
    internal const int Metric_Data_Summary = 11;
    internal const int Metric_Metadata = 12;

    // Gauge
    internal const int Gauge_Data_Points = 1;

    // Sum
    internal const int Sum_Data_Points = 1;
    internal const int Sum_Aggregation_Temporality = 2;
    internal const int Sum_Is_Monotonic = 3;

    // Histogram
    internal const int Histogram_Data_Points = 1;
    internal const int Histogram_Aggregation_Temporality = 2;

    // Exponential Histogram
    internal const int ExponentialHistogram_Data_Points = 1;
    internal const int ExponentialHistogram_Aggregation_Temporality = 2;

    // Summary
    internal const int Summary_Data_Points = 1;

    // Aggregation Temporality (Enum)
    internal const int Aggregation_Temporality_Unknown = 0;
    internal const int Aggregation_Temporality_Delta = 1;
    internal const int Aggregation_Temporality_Cumulative = 2;

    // Data Point Flags (Enum)
    internal const int Data_Point_Flags_Do_Not_Use = 0;
    internal const int Data_Point_Flags_No_Recorded_Value_Mask = 1;

    // Number Data Point
    internal const int NumberDataPoint_Attributes = 7;
    internal const int NumberDataPoint_Start_Time_Unix_Nano = 2;
    internal const int NumberDataPoint_Time_Unix_Nano = 3;
    internal const int NumberDataPoint_Value_As_Double = 4;
    internal const int NumberDataPoint_Value_As_Int = 6;
    internal const int NumberDataPoint_Exemplars = 5;
    internal const int NumberDataPoint_Flags = 8;

    // Histogram Data Point
    internal const int HistogramDataPoint_Attributes = 9;
    internal const int HistogramDataPoint_Start_Time_Unix_Nano = 2;
    internal const int HistogramDataPoint_Time_Unix_Nano = 3;
    internal const int HistogramDataPoint_Count = 4;
    internal const int HistogramDataPoint_Sum = 5;
    internal const int HistogramDataPoint_Bucket_Counts = 6;
    internal const int HistogramDataPoint_Explicit_Bounds = 7;
    internal const int HistogramDataPoint_Exemplars = 8;
    internal const int HistogramDataPoint_Flags = 10;
    internal const int HistogramDataPoint_Min = 11;
    internal const int HistogramDataPoint_Max = 12;

    // Exponential Histogram Data Point
    internal const int ExponentialHistogramDataPoint_Attributes = 1;
    internal const int ExponentialHistogramDataPoint_Start_Time_Unix_Nano = 2;
    internal const int ExponentialHistogramDataPoint_Time_Unix_Nano = 3;
    internal const int ExponentialHistogramDataPoint_Count = 4;
    internal const int ExponentialHistogramDataPoint_Sum = 5;
    internal const int ExponentialHistogramDataPoint_Scale = 6;
    internal const int ExponentialHistogramDataPoint_Zero_Count = 7;
    internal const int ExponentialHistogramDataPoint_Positive = 8;
    internal const int ExponentialHistogramDataPoint_Negative = 9;
    internal const int ExponentialHistogramDataPoint_Flags = 10;
    internal const int ExponentialHistogramDataPoint_Exemplars = 11;
    internal const int ExponentialHistogramDataPoint_Min = 12;
    internal const int ExponentialHistogramDataPoint_Max = 13;
    internal const int ExponentialHistogramDataPoint_Zero_Threshold = 14;

    // Exponential Histogram Data Point - Buckets (nested type)
    internal const int ExponentialHistogramDataPoint_Buckets_Offset = 1;
    internal const int ExponentialHistogramDataPoint_Buckets_Bucket_Counts = 2;

    // Summary Data Point
    internal const int SummaryDataPoint_Attributes = 7;
    internal const int SummaryDataPoint_Start_Time_Unix_Nano = 2;
    internal const int SummaryDataPoint_Time_Unix_Nano = 3;
    internal const int SummaryDataPoint_Count = 4;
    internal const int SummaryDataPoint_Sum = 5;
    internal const int SummaryDataPoint_Quantile_Values = 6;
    internal const int SummaryDataPoint_Flags = 8;

    // Summary Data Point - Value At Quantiles (nested type)
    internal const int SummaryDataPoint_ValueAtQuantiles_Quantile = 1;
    internal const int SummaryDataPoint_ValueAtQuantiles_Value = 2;

    // Exemplar
    internal const int Exemplar_Filtered_Attributes = 7;
    internal const int Exemplar_Time_Unix_Nano = 2;
    internal const int Exemplar_Value_As_Double = 3;
    internal const int Exemplar_Value_As_Int = 6;
    internal const int Exemplar_Span_Id = 4;
    internal const int Exemplar_Trace_Id = 5;
}

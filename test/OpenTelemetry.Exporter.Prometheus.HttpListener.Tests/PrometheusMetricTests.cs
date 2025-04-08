// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace OpenTelemetry.Exporter.Prometheus.Tests;

public sealed class PrometheusMetricTests
{
    [Fact]
    public void SanitizeMetricName_Valid()
    {
        AssertSanitizeMetricName("active_directory_ds_replication_network_io", "active_directory_ds_replication_network_io");
    }

    [Fact]
    public void SanitizeMetricName_RemoveConsecutiveUnderscores()
    {
        AssertSanitizeMetricName("cpu_sp__d_hertz", "cpu_sp_d_hertz");
    }

    [Fact]
    public void SanitizeMetricName_SupportLeadingAndTrailingUnderscores()
    {
        AssertSanitizeMetricName("_cpu_speed_hertz_", "_cpu_speed_hertz_");
    }

    [Fact]
    public void SanitizeMetricName_RemoveUnsupportedCharacters()
    {
        AssertSanitizeMetricName("metric_unit_$1000", "metric_unit_1000");
    }

    [Fact]
    public void SanitizeMetricName_RemoveWhitespace()
    {
        AssertSanitizeMetricName("unit include", "unit_include");
    }

    [Fact]
    public void SanitizeMetricName_RemoveMultipleUnsupportedChracters()
    {
        AssertSanitizeMetricName("sample_me%%$$$_count_ !!@unit include", "sample_me_count_unit_include");
    }

    [Fact]
    public void SanitizeMetricName_RemoveStartingNumber()
    {
        AssertSanitizeMetricName("1_some_metric_name", "_some_metric_name");
    }

    [Fact]
    public void SanitizeMetricName_SupportColon()
    {
        AssertSanitizeMetricName("sample_metric_name__:_per_meter", "sample_metric_name_:_per_meter");
    }

    [Fact]
    public void Unit_Annotation_None()
    {
        Assert.Equal("Test", PrometheusMetric.RemoveAnnotations("Test"));
    }

    [Fact]
    public void Unit_Annotation_RemoveLeading()
    {
        Assert.Equal("%", PrometheusMetric.RemoveAnnotations("%{percentage}"));
    }

    [Fact]
    public void Unit_Annotation_RemoveTrailing()
    {
        Assert.Equal("%", PrometheusMetric.RemoveAnnotations("{percentage}%"));
    }

    [Fact]
    public void Unit_Annotation_RemoveLeadingAndTrailing()
    {
        Assert.Equal("%", PrometheusMetric.RemoveAnnotations("{percentage}%{percentage}"));
    }

    [Fact]
    public void Unit_Annotation_RemoveMiddle()
    {
        Assert.Equal("startend", PrometheusMetric.RemoveAnnotations("start{percentage}end"));
    }

    [Fact]
    public void Unit_Annotation_RemoveEverything()
    {
        Assert.Equal(string.Empty, PrometheusMetric.RemoveAnnotations("{percentage}"));
    }

    [Fact]
    public void Unit_Annotation_Multiple_RemoveEverything()
    {
        Assert.Equal(string.Empty, PrometheusMetric.RemoveAnnotations("{one}{two}"));
    }

    [Fact]
    public void Unit_Annotation_NoClose()
    {
        Assert.Equal("{one", PrometheusMetric.RemoveAnnotations("{one"));
    }

    [Fact]
    public void Unit_AnnotationMismatch_NoClose()
    {
        Assert.Equal("}", PrometheusMetric.RemoveAnnotations("{{one}}"));
    }

    [Fact]
    public void Unit_AnnotationMismatch_Close()
    {
        Assert.Equal(string.Empty, PrometheusMetric.RemoveAnnotations("{{one}"));
    }

    [Fact]
    public void Name_GaugeWithUnit_NoAppendRatio()
    {
        AssertName("sample", "unit", PrometheusType.Gauge, false, false, "sample_unit");
    }

    [Fact]
    public void Name_GaugeWithUnit_NoAppendRatio_NoAppendUnitName()
    {
        AssertName("sample", "unit", PrometheusType.Gauge, false, true, "sample");
    }

    [Fact]
    public void Name_SpecialCaseCounter_AppendTotal()
    {
        AssertName("sample", "unit", PrometheusType.Counter, false, false, "sample_unit_total");
    }

    [Fact]
    public void Name_SpecialCaseCounter_AppendTotal_NoAppendUnitName()
    {
        AssertName("sample", "unit", PrometheusType.Counter, false, true, "sample_total");
    }

    [Fact]
    public void Name_SpecialCaseCounterWithoutUnit_DropUnitAppendTotal()
    {
        AssertName("sample", "1", PrometheusType.Counter, false, false, "sample_total");
    }

    [Fact]
    public void Name_SpecialCaseCounterWithoutUnit_DropUnitAppendTotal_NoAppendUnitName()
    {
        AssertName("sample", "1", PrometheusType.Counter, false, true, "sample_total");
    }

    [Fact]
    public void Name_DisableTotalSuffixAddition_TotalNotAppended()
    {
        AssertName("sample", "1", PrometheusType.Counter, true, false, "sample");
    }

    [Fact]
    public void Name_DisableTotalSuffixAddition_TotalNotAppended_NoAppendUnitName()
    {
        AssertName("sample", "1", PrometheusType.Counter, true, true, "sample");
    }

    [Fact]
    public void Name_TotalSuffixAlreadyPresent_DisableTotalSuffixAddition_TotalNotRemoved()
    {
        AssertName("sample_total", "1", PrometheusType.Counter, true, false, "sample_total");
    }

    [Fact]
    public void Name_TotalSuffixAlreadyPresent_DisableTotalSuffixAddition_TotalNotRemoved_NoAppendUnitName()
    {
        AssertName("sample_total", "1", PrometheusType.Counter, true, true, "sample_total");
    }

    [Fact]
    public void Name_SpecialCaseCounterWithNumber_AppendTotal()
    {
        AssertName("sample", "2", PrometheusType.Counter, false, false, "sample_2_total");
    }

    [Fact]
    public void Name_SpecialCaseCounterWithNumber_AppendTotal_NoAppendUnitName()
    {
        AssertName("sample", "2", PrometheusType.Counter, false, true, "sample_total");
    }

    [Fact]
    public void Name_UnsupportedMetricNameChars_Drop()
    {
        AssertName("s%%ple", "%/m", PrometheusType.Summary, false, false, "s_ple_percent_per_minute");
    }

    [Fact]
    public void Name_UnsupportedMetricNameChars_Drop_NoAppendUnitName()
    {
        AssertName("s%%ple", "%/m", PrometheusType.Summary, false, true, "s_ple");
    }

    [Fact]
    public void Name_UnitOtherThanOne_Normal()
    {
        AssertName("metric_name", "2", PrometheusType.Summary, false, false, "metric_name_2");
    }

    [Fact]
    public void Name_UnitOtherThanOne_Normal_NoAppendUnitName()
    {
        AssertName("metric_name", "2", PrometheusType.Summary, false, true, "metric_name");
    }

    [Fact]
    public void Name_UnitAlreadyPresentInName_NotAppended()
    {
        AssertName("metric_name_total", "total", PrometheusType.Counter, false, false, "metric_name_total");
    }

    [Fact]
    public void Name_UnitAlreadyPresentInName_NotAppended_NoAppendUnitName()
    {
        AssertName("metric_name_total", "total", PrometheusType.Counter, false, true, "metric_name_total");
    }

    [Fact]
    public void Name_UnitAlreadyPresentInName_TotalNonCounterType_NotAppended()
    {
        AssertName("metric_name_total", "total", PrometheusType.Summary, false, false, "metric_name_total");
    }

    [Fact]
    public void Name_UnitAlreadyPresentInName_TotalNonCounterType_NotAppended_NoAppendUnitName()
    {
        AssertName("metric_name_total", "total", PrometheusType.Summary, false, true, "metric_name_total");
    }

    [Fact]
    public void Name_UnitAlreadyPresentInName_CustomGauge_NotAppended()
    {
        AssertName("metric_hertz", "hertz", PrometheusType.Gauge, false, false, "metric_hertz");
    }

    [Fact]
    public void Name_UnitAlreadyPresentInName_CustomGauge_NotAppended_NoAppendUnitName()
    {
        AssertName("metric_hertz", "hertz", PrometheusType.Gauge, false, true, "metric_hertz");
    }

    [Fact]
    public void Name_UnitAlreadyPresentInName_CustomCounter_NotAppended()
    {
        AssertName("metric_hertz_total", "hertz_total", PrometheusType.Counter, false, false, "metric_hertz_total");
    }

    [Fact]
    public void Name_UnitAlreadyPresentInName_CustomCounter_NotAppended_NoAppendUnitName()
    {
        AssertName("metric_hertz_total", "hertz_total", PrometheusType.Counter, false, true, "metric_hertz_total");
    }

    [Fact]
    public void Name_UnitAlreadyPresentInName_OrderMatters_Appended()
    {
        AssertName("metric_total_hertz", "hertz_total", PrometheusType.Counter, false, false, "metric_total_hertz_hertz_total");
    }

    [Fact]
    public void Name_UnitAlreadyPresentInName_OrderMatters_Appended_NoAppendUnitName()
    {
        AssertName("metric_total_hertz", "hertz_total", PrometheusType.Counter, false, true, "metric_total_hertz_total");
    }

    [Fact]
    public void Name_StartWithNumber_UnderscoreStart()
    {
        AssertName("2_metric_name", "By", PrometheusType.Summary, false, false, "_metric_name_bytes");
    }

    [Fact]
    public void Name_StartWithNumber_UnderscoreStart_NoAppendUnitName()
    {
        AssertName("2_metric_name", "By", PrometheusType.Summary, false, true, "_metric_name");
    }

    [Fact]
    public void OpenMetricsName_UnitAlreadyPresentInName_Appended()
    {
        AssertOpenMetricsName("db_bytes_written", "By", PrometheusType.Gauge, false, false, "db_bytes_written_bytes");
    }

    [Fact]
    public void OpenMetricsName_UnitAlreadyPresentInName_Appended_NoAppendUnitName()
    {
        AssertOpenMetricsName("db_bytes_written", "By", PrometheusType.Gauge, false, true, "db_bytes_written");
    }

    [Fact]
    public void OpenMetricsName_SuffixedWithUnit_NotAppended()
    {
        AssertOpenMetricsName("db_written_bytes", "By", PrometheusType.Gauge, false, false, "db_written_bytes");
    }

    [Fact]
    public void OpenMetricsName_SuffixedWithUnit_NotAppended_NoAppendUnitName()
    {
        AssertOpenMetricsName("db_written_bytes", "By", PrometheusType.Gauge, false, true, "db_written_bytes");
    }

    [Fact]
    public void OpenMetricsName_Counter_AppendTotal()
    {
        AssertOpenMetricsName("db_bytes_written", "By", PrometheusType.Counter, false, false, "db_bytes_written_bytes_total");
    }

    [Fact]
    public void OpenMetricsName_Counter_AppendTotal_NoAppendUnitName()
    {
        AssertOpenMetricsName("db_bytes_written", "By", PrometheusType.Counter, false, true, "db_bytes_written_total");
    }

    [Fact]
    public void OpenMetricsName_Counter_DisableSuffixTotal_AppendTotal()
    {
        AssertOpenMetricsName("db_bytes_written", "By", PrometheusType.Counter, true, false, "db_bytes_written_bytes_total");
    }

    [Fact]
    public void OpenMetricsName_Counter_DisableSuffixTotal_AppendTotal_NoAppendUnitName()
    {
        AssertOpenMetricsName("db_bytes_written", "By", PrometheusType.Counter, true, true, "db_bytes_written_total");
    }

    [Fact]
    public void OpenMetricsName_CounterSuffixedWithTotal_AppendUnitAndTotal()
    {
        AssertOpenMetricsName("db_bytes_written_total", "By", PrometheusType.Counter, false, false, "db_bytes_written_bytes_total");
    }

    [Fact]
    public void OpenMetricsName_CounterSuffixedWithTotal_AppendUnitAndTotal_NoAppendUnitName()
    {
        AssertOpenMetricsName("db_bytes_written_total", "By", PrometheusType.Counter, false, true, "db_bytes_written_total");
    }

    [Fact]
    public void OpenMetricsName_CounterSuffixedWithTotal_DisableSuffixTotal_AppendTotal()
    {
        AssertOpenMetricsName("db_bytes_written_total", "By", PrometheusType.Counter, false, false, "db_bytes_written_bytes_total");
    }

    [Fact]
    public void OpenMetricsName_CounterSuffixedWithTotal_DisableSuffixTotal_AppendTotal_NoAppendUnitName()
    {
        AssertOpenMetricsName("db_bytes_written_total", "By", PrometheusType.Counter, false, true, "db_bytes_written_total");
    }

    [Fact]
    public void OpenMetricsMetadataName_Counter_NotAppendTotal()
    {
        AssertOpenMetricsMetadataName("db_bytes_written", "By", PrometheusType.Counter, false, false, "db_bytes_written_bytes");
    }

    [Fact]
    public void OpenMetricsMetadataName_Counter_NotAppendTotal_NoAppendUnitName()
    {
        AssertOpenMetricsMetadataName("db_bytes_written", "By", PrometheusType.Counter, false, true, "db_bytes_written");
    }

    [Fact]
    public void OpenMetricsMetadataName_Counter_DisableSuffixTotal_NotAppendTotal()
    {
        AssertOpenMetricsMetadataName("db_bytes_written", "By", PrometheusType.Counter, true, false, "db_bytes_written_bytes");
    }

    [Fact]
    public void OpenMetricsMetadataName_Counter_DisableSuffixTotal_NotAppendTotal_NoAppendUnitName()
    {
        AssertOpenMetricsMetadataName("db_bytes_written", "By", PrometheusType.Counter, true, true, "db_bytes_written");
    }

    private static void AssertName(
        string name, string unit, PrometheusType type, bool disableTotalNameSuffixForCounters, bool disableAppendingUnitName, string expected)
    {
        var prometheusMetric = new PrometheusMetric(name, unit, type, disableTotalNameSuffixForCounters, disableAppendingUnitName);
        Assert.Equal(expected, prometheusMetric.Name);
    }

    private static void AssertSanitizeMetricName(string name, string expected)
    {
        var sanatizedName = PrometheusMetric.SanitizeMetricName(name);
        Assert.Equal(expected, sanatizedName);
    }

    private static void AssertOpenMetricsName(
        string name, string unit, PrometheusType type, bool disableTotalNameSuffixForCounters, bool disableAppendingUnitName, string expected)
    {
        var prometheusMetric = new PrometheusMetric(name, unit, type, disableTotalNameSuffixForCounters, disableAppendingUnitName);
        Assert.Equal(expected, prometheusMetric.OpenMetricsName);
    }

    private static void AssertOpenMetricsMetadataName(
        string name, string unit, PrometheusType type, bool disableTotalNameSuffixForCounters, bool disableAppendingUnitName, string expected)
    {
        var prometheusMetric = new PrometheusMetric(name, unit, type, disableTotalNameSuffixForCounters, disableAppendingUnitName);
        Assert.Equal(expected, prometheusMetric.OpenMetricsMetadataName);
    }
}

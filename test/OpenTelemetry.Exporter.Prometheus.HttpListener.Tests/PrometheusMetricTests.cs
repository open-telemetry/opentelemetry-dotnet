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
    public void SanitizeMetricName_RemoveUnsupportedChracters()
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
    public void Name_SpecialCaseGuage_AppendRatio()
    {
        AssertName("sample", "1", PrometheusType.Gauge, "sample_ratio");
    }

    [Fact]
    public void Name_GuageWithUnit_NoAppendRatio()
    {
        AssertName("sample", "unit", PrometheusType.Gauge, "sample_unit");
    }

    [Fact]
    public void Name_SpecialCaseCounter_AppendTotal()
    {
        AssertName("sample", "unit", PrometheusType.Counter, "sample_unit_total");
    }

    [Fact]
    public void Name_SpecialCaseCounterWithoutUnit_DropUnitAppendTotal()
    {
        AssertName("sample", "1", PrometheusType.Counter, "sample_total");
    }

    [Fact]
    public void Name_SpecialCaseCounterWithNumber_AppendTotal()
    {
        AssertName("sample", "2", PrometheusType.Counter, "sample_2_total");
    }

    [Fact]
    public void Name_UnsupportedMetricNameChars_Drop()
    {
        AssertName("s%%ple", "%/m", PrometheusType.Summary, "s_ple_percent_per_minute");
    }

    [Fact]
    public void Name_UnitOtherThanOne_Normal()
    {
        AssertName("metric_name", "2", PrometheusType.Summary, "metric_name_2");
    }

    [Fact]
    public void Name_UnitAlreadyPresentInName_NotAppended()
    {
        AssertName("metric_name_total", "total", PrometheusType.Counter, "metric_name_total");
    }

    [Fact]
    public void Name_UnitAlreadyPresentInName_TotalNonCounterType_NotAppended()
    {
        AssertName("metric_name_total", "total", PrometheusType.Summary, "metric_name_total");
    }

    [Fact]
    public void Name_UnitAlreadyPresentInName_CustomGauge_NotAppended()
    {
        AssertName("metric_hertz", "hertz", PrometheusType.Gauge, "metric_hertz");
    }

    [Fact]
    public void Name_UnitAlreadyPresentInName_CustomCounter_NotAppended()
    {
        AssertName("metric_hertz_total", "hertz_total", PrometheusType.Counter, "metric_hertz_total");
    }

    [Fact]
    public void Name_UnitAlreadyPresentInName_OrderMatters_Appended()
    {
        AssertName("metric_total_hertz", "hertz_total", PrometheusType.Counter, "metric_total_hertz_hertz_total");
    }

    [Fact]
    public void Name_StartWithNumber_UnderscoreStart()
    {
        AssertName("2_metric_name", "By", PrometheusType.Summary, "_metric_name_bytes");
    }

    private static void AssertName(string name, string unit, PrometheusType type, string expected)
    {
        var prometheusMetric = new PrometheusMetric(name, unit, type);
        Assert.Equal(expected, prometheusMetric.Name);
    }

    private static void AssertSanitizeMetricName(string name, string expected)
    {
        var sanatizedName = PrometheusMetric.SanitizeMetricName(name);
        Assert.Equal(expected, sanatizedName);
    }
}

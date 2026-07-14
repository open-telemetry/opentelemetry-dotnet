// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text;

namespace OpenTelemetry.Exporter.Prometheus.Tests;

public static class PrometheusEscapingTests
{
    [Theory]
    [InlineData("metric.name.with.dots", "metric_dot_name_dot_with_dot_dots")]
    [InlineData("mysystem.prod.west.cpu.load", "mysystem_dot_prod_dot_west_dot_cpu_dot_load")]
    [InlineData("mysystem.prod.west.cpu.load_total", "mysystem_dot_prod_dot_west_dot_cpu_dot_load__total")]
    [InlineData("http.status:sum", "http_dot_status:sum")]
    [InlineData("no:escaping_required", "no:escaping__required")]
    [InlineData("a_b", "a__b")]
    [InlineData("a b", "a_b")]
    [InlineData("label with \U0001F631", "label_with__")]
    [InlineData("\u82B1\u706B", "__")]
    [InlineData("", "")]
    public static void EscapeName_Dots(string name, string expected)
        => Assert.Equal(expected, PrometheusEscaping.EscapeName(name, EscapingScheme.Dots));

    [Theory]
    [InlineData("metric.name", "U__metric_2e_name")]
    [InlineData("mysystem.prod.west.cpu.load", "U__mysystem_2e_prod_2e_west_2e_cpu_2e_load")]
    [InlineData("mysystem.prod.west.cpu.load_total", "U__mysystem_2e_prod_2e_west_2e_cpu_2e_load__total")]
    [InlineData("http.status:sum", "U__http_2e_status:sum")]
    [InlineData("no:escaping_required", "no:escaping_required")]
    [InlineData("foo.bar", "U__foo_2e_bar")]
    [InlineData("U__foo_2e_bar", "U___55_____foo__2e__bar")]
    [InlineData("label with \u0100", "U__label_20_with_20__100_")]
    [InlineData("", "")]
    public static void EscapeName_Values(string name, string expected)
        => Assert.Equal(expected, PrometheusEscaping.EscapeName(name, EscapingScheme.Values));

    [Fact]
    public static void EscapeName_Values_EncodesSpacesAndAstralCharacters()
    {
        // Input "label with \U0001F631" -> spaces become _20_ and the astral character its code point.
        string name = "label with \U0001F631";

        Assert.Equal(
            "U__label_20_with_20__1f631_",
            PrometheusEscaping.EscapeName(name, EscapingScheme.Values));
    }

    [Fact]
    public static void EscapeName_Values_EncodesNonAsciiCharacters()
    {
        // Input is the two characters U+82B1 and U+706B.
        string name = "\u82B1\u706B";

        Assert.Equal(
            "U___82b1__706b_",
            PrometheusEscaping.EscapeName(name, EscapingScheme.Values));
    }

    [Fact]
    public static void EscapeName_Values_EncodesUnpairedSurrogateAsReplacementCharacter()
    {
        // A lone high surrogate is not a valid Unicode scalar value and is encoded as
        // the literal _FFFD_ replacement marker used by the reference implementation.
        string name = "\uD800";

        Assert.Equal(
            "U___FFFD_",
            PrometheusEscaping.EscapeName(name, EscapingScheme.Values));
    }

    [Fact]
    public static void EscapeName_ToBuffer_Values_WithLegacyValidName_WritesNameUnchanged()
    {
        var buffer = new byte[32];

        var cursor = PrometheusEscaping.EscapeName(buffer, 0, "no:escaping_required", EscapingScheme.Values);

        Assert.Equal("no:escaping_required", Encoding.ASCII.GetString(buffer, 0, cursor));
    }

    [Theory]
    [InlineData("metric_name", true)]
    [InlineData("Avalid_23name", true)]
    [InlineData("colon:in:the:middle", true)]
    [InlineData("a\u00C5z", false)]
    [InlineData("MetricName", true)]
    [InlineData("_metric", true)]
    [InlineData(":metric", true)]
    [InlineData("http.server.requests", false)]
    [InlineData("0metric", false)]
    [InlineData("metric.name", false)]
    [InlineData("metric name", false)]
    [InlineData("", false)]
    public static void IsValidLegacyName_ValidatesMetricNames(string name, bool expected)
        => Assert.Equal(expected, PrometheusEscaping.IsValidLegacyName(name));

    [Theory]
    [InlineData("label_name", true)]
    [InlineData("LabelName", true)]
    [InlineData("_label", true)]
    [InlineData("la:bel", false)]
    [InlineData("http.method", false)]
    [InlineData("0label", false)]
    [InlineData("", false)]
    public static void IsValidLegacyLabelName_DisallowsColons(string name, bool expected)
        => Assert.Equal(expected, PrometheusEscaping.IsValidLegacyLabelName(name));

    [Theory]
    [InlineData("http:method", "http_method")]
    [InlineData("http.method:value", "http_dot_method_value")]
    public static void EscapeName_Dots_LabelNameEncodesColon(string name, string expected)
        => Assert.Equal(expected, PrometheusEscaping.EscapeName(name, EscapingScheme.Dots, isMetricName: false));

    [Theory]
    [InlineData("http:method", "U__http_3a_method")]
    [InlineData("http.method:value", "U__http_2e_method_3a_value")]
    public static void EscapeName_Values_LabelNameEncodesColon(string name, string expected)
        => Assert.Equal(expected, PrometheusEscaping.EscapeName(name, EscapingScheme.Values, isMetricName: false));

    [Theory]
    [InlineData(EscapingScheme.AllowUtf8, "metric.name", "metric.name")]
    [InlineData(EscapingScheme.AllowUtf8, "a_b", "a_b")]
    [InlineData(EscapingScheme.AllowUtf8, "", "")]
    [InlineData(EscapingScheme.Underscores, "metric.name", "metric.name")]
    [InlineData(EscapingScheme.Underscores, "a_b", "a_b")]
    [InlineData(EscapingScheme.Underscores, "", "")]
    internal static void EscapeName_LeavesNameUnchanged(EscapingScheme scheme, string name, string expected)
        => Assert.Equal(expected, PrometheusEscaping.EscapeName(name, scheme));

    [Theory]
    [InlineData(EscapingScheme.Dots, "http:method", "http:method")]
    [InlineData(EscapingScheme.Values, "http:method", "http:method")]
    internal static void EscapeName_MetricNameKeepsColon(EscapingScheme scheme, string name, string expected)
        => Assert.Equal(expected, PrometheusEscaping.EscapeName(name, scheme, isMetricName: true));

    [Theory]
    [InlineData(EscapingScheme.Dots)]
    [InlineData(EscapingScheme.Values)]
    internal static void EscapeName_ToBuffer_WithEmptyName_LeavesCursorUnchanged(EscapingScheme scheme)
    {
        var buffer = new byte[16];

        var cursor = PrometheusEscaping.EscapeName(buffer, 5, string.Empty, scheme);

        Assert.Equal(5, cursor);
    }

    [Theory]
    [InlineData(null, EscapingScheme.Underscores)]
    [InlineData("", EscapingScheme.Underscores)]
    [InlineData("allow-utf-8", EscapingScheme.AllowUtf8)]
    [InlineData("anything-else", EscapingScheme.Underscores)]
    [InlineData("dots", EscapingScheme.Dots)]
    [InlineData("underscores", EscapingScheme.Underscores)]
    [InlineData("values", EscapingScheme.Values)]
    internal static void FromString_MapsEscapingScheme(string? escaping, EscapingScheme expected)
        => Assert.Equal(expected, PrometheusEscaping.FromString(escaping));

    // The malformed inputs are built in code rather than passed via [InlineData]
    // because xUnit's theory-data serialization does not round-trip lone surrogates
    // (they are replaced with the U+FFFD replacement character).

    [Fact]
    internal static void EscapeName_Dots_EncodesUnpairedSurrogatesWithoutOverrun()
    {
        Assert.Equal("_", PrometheusEscaping.EscapeName("\uD800", EscapingScheme.Dots));
        Assert.Equal("abc_", PrometheusEscaping.EscapeName("abc\uD800", EscapingScheme.Dots));
        Assert.Equal("abc_", PrometheusEscaping.EscapeName("abc\uDC00", EscapingScheme.Dots));
        Assert.Equal("_abc", PrometheusEscaping.EscapeName("\uD800abc", EscapingScheme.Dots));
        Assert.Equal("___", PrometheusEscaping.EscapeName("\uD800\uD800\uD800", EscapingScheme.Dots));
    }

    [Fact]
    internal static void EscapeName_Values_EncodesUnpairedSurrogatesWithoutOverrun()
    {
        Assert.Equal("U___FFFD_", PrometheusEscaping.EscapeName("\uD800", EscapingScheme.Values));
        Assert.Equal("U__abc_FFFD_", PrometheusEscaping.EscapeName("abc\uD800", EscapingScheme.Values));
        Assert.Equal("U__abc_FFFD_", PrometheusEscaping.EscapeName("abc\uDC00", EscapingScheme.Values));
        Assert.Equal("U___FFFD_abc", PrometheusEscaping.EscapeName("\uD800abc", EscapingScheme.Values));

        // A valid astral pair (U+10000) followed by a trailing lone high surrogate.
        Assert.Equal("U___10000__FFFD_", PrometheusEscaping.EscapeName("\U00010000\uD800", EscapingScheme.Values));
    }
}

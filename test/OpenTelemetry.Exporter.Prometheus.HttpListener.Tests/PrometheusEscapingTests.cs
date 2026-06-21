// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter.Prometheus.Tests;

public static class PrometheusEscapingTests
{
    [Theory]
    [InlineData("metric.name.with.dots", "metric_dot_name_dot_with_dot_dots")]
    [InlineData("mysystem.prod.west.cpu.load", "mysystem_dot_prod_dot_west_dot_cpu_dot_load")]
    [InlineData("mysystem.prod.west.cpu.load_total", "mysystem_dot_prod_dot_west_dot_cpu_dot_load__total")]
    [InlineData("http.status:sum", "http_dot_status:sum")]
    [InlineData("no:escaping_required", "no:escaping__required")]
    [InlineData("", "")]
    public static void EscapeName_Dots(string name, string expected)
        => Assert.Equal(expected, PrometheusEscaping.EscapeName(name, EscapingScheme.Dots));

    [Theory]
    [InlineData("metric.name", "U__metric_2e_name")]
    [InlineData("mysystem.prod.west.cpu.load", "U__mysystem_2e_prod_2e_west_2e_cpu_2e_load")]
    [InlineData("mysystem.prod.west.cpu.load_total", "U__mysystem_2e_prod_2e_west_2e_cpu_2e_load__total")]
    [InlineData("http.status:sum", "U__http_2e_status:sum")]
    [InlineData("no:escaping_required", "no:escaping_required")]
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

    [Theory]
    [InlineData("metric.name", "metric.name")]
    [InlineData("a_b", "a_b")]
    [InlineData("", "")]
    public static void EscapeName_Underscores_IsHandledElsewhere(string name, string expected)
        => Assert.Equal(expected, PrometheusEscaping.EscapeName(name, EscapingScheme.Underscores));

    [Theory]
    [InlineData(null, EscapingScheme.Underscores)]
    [InlineData("underscores", EscapingScheme.Underscores)]
    [InlineData("dots", EscapingScheme.Dots)]
    [InlineData("values", EscapingScheme.Values)]
    [InlineData("allow-utf-8", EscapingScheme.Underscores)]
    [InlineData("anything-else", EscapingScheme.Underscores)]
    internal static void FromString_MapsEscapingScheme(string? escaping, EscapingScheme expected)
        => Assert.Equal(expected, PrometheusEscaping.FromString(escaping));
}

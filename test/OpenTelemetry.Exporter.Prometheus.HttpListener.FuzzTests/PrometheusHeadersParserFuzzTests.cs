// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace OpenTelemetry.Exporter.Prometheus.FuzzTests;

/// <summary>
/// Fuzz tests for <see cref="PrometheusHeadersParser"/>, which parses the unauthenticated
/// <c>Accept</c> header of every scrape request.
/// </summary>
public class PrometheusHeadersParserFuzzTests
{
    private const int MaxTests = 500;

    [Property(MaxTest = MaxTests)]
    public Property ArbitraryHeaderNeverThrowsAndReturnsAKnownProtocol() => Prop.ForAll(
        Generators.ArbitraryHeaderArbitrary(),
        static (header) => IsKnownProtocol(PrometheusHeadersParser.Negotiate(header)));

    [Property(MaxTest = MaxTests)]
    public Property TokenSoupHeaderNeverThrowsAndReturnsAKnownProtocol() => Prop.ForAll(
        Generators.TokenSoupHeaderArbitrary(),
        static (header) => IsKnownProtocol(PrometheusHeadersParser.Negotiate(header)));

    [Property(MaxTest = MaxTests)]
    public Property NegotiationMatchesReferenceModel() => Prop.ForAll(
        Generators.AcceptHeaderArbitrary(),
        static (header) =>
        {
            var actual = PrometheusHeadersParser.Negotiate(header.Text);

            return actual.Equals(header.Expected);
        });

    [Property(MaxTest = MaxTests)]
    public Property NegotiationIsIndependentOfEntryWhitespace() => Prop.ForAll(
        Generators.AcceptHeaderArbitrary(),
        static (header) =>
        {
            var compact = PrometheusHeadersParser.Negotiate(new string(header.Text.Where(static c => c is not (' ' or '\t')).ToArray()));
            var original = PrometheusHeadersParser.Negotiate(header.Text);

            return compact.Equals(original);
        });

    [Property(MaxTest = MaxTests)]
    public Property NegotiatedContentTypeRoundTripsThroughTheParser() => Prop.ForAll(
        Generators.AcceptHeaderArbitrary(),
        static (header) =>
        {
            var negotiated = PrometheusHeadersParser.Negotiate(header.Text);
            var contentType = PrometheusProtocol.GetContentType(negotiated);

            // The Content-Type the exporter responds with must itself negotiate to the same protocol.
            return PrometheusHeadersParser.Negotiate(contentType).Equals(negotiated);
        });

    private static bool IsKnownProtocol(PrometheusProtocol protocol)
    {
        var knownMediaType = protocol.MediaType is PrometheusProtocol.OpenMetricsMediaType or PrometheusProtocol.PrometheusTextMediaType;
        var knownEscaping = protocol.Escaping is null || PrometheusProtocol.SupportedEscapingSchemes.Contains(protocol.Escaping);

        var knownVersion = protocol.IsOpenMetrics
            ? PrometheusProtocol.SupportedOpenMetricsVersions.Contains(protocol.Version)
            : PrometheusProtocol.SupportedPrometheusVersions.Contains(protocol.Version);

        // Versions before 1.0.0 do not negotiate an escaping scheme; later versions always have one.
        var escapingMatchesVersion = (protocol.Escaping is null) == (protocol.Version.Major == 0);

        return knownMediaType && knownEscaping && knownVersion && escapingMatchesVersion;
    }

    private static class Generators
    {
        private static readonly Gen<string> Whitespace = Gen.Elements(string.Empty, " ", "\t", "  ", " \t");

        public static Arbitrary<string?> ArbitraryHeaderArbitrary()
        {
            var charGen = Gen.Frequency(
                (6, Gen.Choose(0x20, 0x7E).Select(static c => (char)c)),
                (1, Gen.Choose(0, 0xFFFF).Select(static c => (char)c)));

            var text = Gen.Sized(size =>
                from length in Gen.Choose(0, Math.Min((size * 4) + 1, 512))
                from chars in Gen.ArrayOf(charGen, length)
                select (string?)new string(chars));

            return Gen.OneOf(Gen.Constant((string?)null), Gen.Constant((string?)string.Empty), text).ToArbitrary();
        }

        public static Arbitrary<string?> TokenSoupHeaderArbitrary()
        {
            var vocabulary = new[]
            {
                "text/plain",
                "application/openmetrics-text",
                "application/json",
                "*/*",
                "text/*",
                ";",
                ",",
                "=",
                "\"",
                " ",
                "\t",
                "q",
                "q=",
                "q=0",
                "q=1",
                "q=0.5",
                "q=1.1",
                "q=-1",
                "q=NaN",
                "q=1e0",
                "q=.5",
                "version=",
                "version=0.0.4",
                "version=1.0.0",
                "version=0.0.1",
                "version=1.0",
                "version=\"1.0.0\"",
                "escaping=",
                "escaping=underscores",
                "escaping=dots",
                "escaping=values",
                "escaping=allow-utf-8",
                "escaping=\"values\"",
                "charset=utf-8",
                "\xE9",
                "\0",
                "\r\n",
            };

            var gen =
                from count in Gen.Choose(0, 24)
                from tokens in Gen.ArrayOf(Gen.Elements(vocabulary), count)
                select (string?)string.Concat(tokens);

            return gen.ToArbitrary();
        }

        public static Arbitrary<AcceptHeader> AcceptHeaderArbitrary()
        {
            var gen =
                from count in Gen.Choose(0, 6)
                from entries in Gen.ArrayOf(EntryGen(), count)
                from separators in Gen.ArrayOf(from before in Whitespace from after in Whitespace select before + "," + after, count)
                select Build(entries, separators);

            return gen.ToArbitrary();
        }

        private static AcceptHeader Build(Entry[] entries, string[] separators)
        {
            var text = new StringBuilder();
            PrometheusProtocol? best = null;
            var bestQuality = double.MinValue;

            for (var i = 0; i < entries.Length; i++)
            {
                if (i > 0)
                {
                    text.Append(separators[i]);
                }

                text.Append(entries[i].Text);

                // The first entry with the highest quality wins.
                if (entries[i].Protocol is { } protocol && entries[i].Quality > bestQuality)
                {
                    best = protocol;
                    bestQuality = entries[i].Quality;
                }
            }

            return new AcceptHeader
            {
                Text = text.ToString(),
                Expected = best ?? PrometheusProtocol.Fallback,
            };
        }

        private static Gen<Entry> EntryGen() => Gen.Frequency(
            (6, ValidEntry()),
            (1, InvalidEntry()));

        private static Gen<Entry> ValidEntry() =>
            from isOpenMetrics in Gen.Elements(true, false)
            from mediaType in isOpenMetrics
                ? Gen.Elements("application/openmetrics-text", "Application/OpenMetrics-Text", "APPLICATION/OPENMETRICS-TEXT")
                : Gen.Elements("text/plain", "TEXT/PLAIN", "Text/Plain")
            from version in isOpenMetrics
                ? Gen.Elements(null, "0.0.1", "1.0.0")
                : Gen.Elements(null, "0.0.4", "1.0.0")
            from quoteVersion in Gen.Elements(true, false)
            from escaping in Gen.Elements(null, "allow-utf-8", "dots", "underscores", "values")
            from quoteEscaping in Gen.Elements(true, false)
            from quality in Gen.Elements(null, "1", "1.0", "0.9", "0.75", "0.5", "0.5", "0.25", "0.001")
            from unknownParameter in Gen.Elements(null, null, "charset=utf-8", "foo=bar", "x")
            from leading in Whitespace
            from trailing in Whitespace
            from gap in Whitespace
            from parameters in Gen.Shuffle(
            new string?[]
            {
                version is null ? null : "version=" + Quote(version, quoteVersion),
                escaping is null ? null : "escaping=" + Quote(escaping, quoteEscaping),
                quality is null ? null : "q=" + quality,
                unknownParameter,
            })
            select CreateValidEntry(isOpenMetrics, mediaType, version, escaping, quality, parameters, leading, gap, trailing);

        private static Gen<Entry> InvalidEntry() =>
            from kind in Gen.Choose(0, 5)
            from isOpenMetrics in Gen.Elements(true, false)
            from leading in Whitespace
            from trailing in Whitespace
            select CreateInvalidEntry(kind, isOpenMetrics, leading, trailing);

        private static string Quote(string value, bool quote) => quote ? "\"" + value + "\"" : value;

        private static Entry CreateValidEntry(
            bool isOpenMetrics,
            string mediaType,
            string? version,
            string? escaping,
            string? quality,
            string?[] parameters,
            string leading,
            string gap,
            string trailing)
        {
            var text = leading + mediaType;

            foreach (var parameter in parameters)
            {
                if (parameter is not null)
                {
                    text += gap + ";" + gap + parameter;
                }
            }

            text += trailing;

            var resolvedVersion = version is null
                ? (isOpenMetrics ? PrometheusProtocol.OpenMetricsV1 : PrometheusProtocol.PrometheusV0)
                : Version.Parse(version);

            var resolvedEscaping = resolvedVersion.Major == 0 ? null : escaping ?? PrometheusProtocol.UnderscoresEscaping;

            var protocol = new PrometheusProtocol(
                isOpenMetrics ? PrometheusProtocol.OpenMetricsMediaType : PrometheusProtocol.PrometheusTextMediaType,
                resolvedEscaping,
                resolvedVersion,
                isOpenMetrics);

            return new Entry
            {
                Text = text,
                Protocol = protocol,
                Quality = quality is null ? 1.0 : double.Parse(quality, CultureInfo.InvariantCulture),
            };
        }

        private static Entry CreateInvalidEntry(int kind, bool isOpenMetrics, string leading, string trailing)
        {
            var mediaType = isOpenMetrics ? "application/openmetrics-text" : "text/plain";

            var text = kind switch
            {
                0 => "application/json",
                1 => "*/*",
                2 => mediaType + "; version=2.0.0",
                3 => mediaType + "; version=1.0",
                4 => mediaType + "; escaping=unknown",
                _ => mediaType + "; q=0",
            };

            return new Entry
            {
                Text = leading + text + trailing,
                Protocol = null,
                Quality = 0,
            };
        }
    }

    private sealed class AcceptHeader
    {
        public string Text { get; set; } = string.Empty;

        public PrometheusProtocol Expected { get; set; }
    }

    private sealed class Entry
    {
        public string Text { get; set; } = string.Empty;

        public PrometheusProtocol? Protocol { get; set; }

        public double Quality { get; set; }
    }
}

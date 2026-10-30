// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Text;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter.Prometheus.FuzzTests;

/// <summary>
/// Fuzz tests which run arbitrary metrics through the whole exporter, for every protocol and translation
/// strategy, and check that the exposition produced is valid for the format that was negotiated.
/// </summary>
/// <remarks>
/// A scrape which contains a single invalid line is rejected in its entirety by Prometheus, so one
/// badly named instrument or unusual tag value must never be able to break the exposition of all metrics.
/// </remarks>
public class PrometheusExpositionFuzzTests
{
    private const int MaxTests = 150;
    private const char Backslash = (char)92;

    private enum ErrorKind
    {
        Syntax,
        Structure,
        DuplicateLabels,
        DuplicateSeries,
        CounterSuffix,
        Unit,
        Histogram,
        Directive,
    }

    private enum InstrumentKind
    {
        CounterLong,
        CounterDouble,
        UpDownCounterLong,
        Gauge,
        Histogram,
        ObservableGauge,
    }

    [Property(MaxTest = MaxTests)]
    public Property ExpositionIsSyntacticallyValidForTheNegotiatedProtocol() => Prop.ForAll(
        Generators.ScenarioArbitrary(),
        static (scenario) => HasNoErrors(scenario, ErrorKind.Syntax));

    [Property(MaxTest = MaxTests)]
    public Property EachMetricFamilyIsDeclaredOnceAndItsSamplesAreContiguous() => Prop.ForAll(
        Generators.ScenarioArbitrary(),
        static (scenario) => HasNoErrors(scenario, ErrorKind.Structure));

    [Property(MaxTest = MaxTests)]
    public Property LabelNamesAreUniqueWithinEachSample() => Prop.ForAll(
        Generators.ScenarioArbitrary(),
        static (scenario) => HasNoErrors(scenario, ErrorKind.DuplicateLabels));

    [Property(MaxTest = MaxTests)]
    public Property EachSeriesAppearsOnlyOnce() => Prop.ForAll(
        Generators.ScenarioArbitrary(),
        static (scenario) => HasNoErrors(scenario, ErrorKind.DuplicateSeries));

    [Property(MaxTest = MaxTests)]
    public Property OpenMetricsCounterSamplesEndInTotal() => Prop.ForAll(
        Generators.ScenarioArbitrary(),
        static (scenario) =>
        {
            // Counters are only given the suffix when the translation strategy appends suffixes and the option which
            // disables the suffix for counters is not set. Otherwise counters are deliberately exposed under their own names.
            var appendsTotal = scenario.Strategy is PrometheusTranslationStrategy.UnderscoreEscapingWithSuffixes or PrometheusTranslationStrategy.NoUTF8EscapingWithSuffixes &&
                !scenario.DisableTotalNameSuffixForCounters;

            return !appendsTotal || HasNoErrors(scenario, ErrorKind.CounterSuffix);
        });

    [Property(MaxTest = MaxTests)]
    public Property OpenMetricsUnitIsASuffixOfTheMetricFamilyName() => Prop.ForAll(
        Generators.ScenarioArbitrary(),
        static (scenario) => HasNoErrors(scenario, ErrorKind.Unit));

    [Property(MaxTest = MaxTests)]
    public Property HistogramsAreWellFormed() => Prop.ForAll(
        Generators.ScenarioArbitrary(),
        static (scenario) => HasNoErrors(scenario, ErrorKind.Histogram));

    [Property(MaxTest = MaxTests)]
    public Property ClassicTextFormatDoesNotContainOpenMetricsOnlyDirectives() => Prop.ForAll(
        Generators.ScenarioArbitrary(),
        static (scenario) =>
        {
            if (scenario.EffectiveProtocol.IsOpenMetrics)
            {
                return true;
            }

            var lines = Scrape(scenario).Split('\n');

            // The Prometheus text format only defines HELP and TYPE metadata, and has no end of file marker.
            return !lines.Any(static l => l.StartsWith("# UNIT ", StringComparison.Ordinal) || l == "# EOF");
        });

    [Property(MaxTest = MaxTests)]
    public Property OpenMetricsExpositionIsTerminatedByExactlyOneEof() => Prop.ForAll(
        Generators.ScenarioArbitrary(),
        static (scenario) =>
        {
            if (!scenario.EffectiveProtocol.IsOpenMetrics)
            {
                return true;
            }

            var text = Scrape(scenario);
            var lines = text.Split('\n');

            return text.EndsWith("# EOF\n", StringComparison.Ordinal) && lines.Count(static l => l == "# EOF") == 1;
        });

    [Property(MaxTest = MaxTests)]
    public Property ExpositionIsIndependentOfRepeatedScrapes() => Prop.ForAll(
        Generators.ScenarioArbitrary(),
        static (scenario) =>
        {
            // With nothing recorded in between, every scrape of cumulative metrics must be identical.
            var scrapes = ScrapeAll(scenario, scrapes: 3);

            if (scrapes[0] != scrapes[1] || scrapes[1] != scrapes[2])
            {
                throw new InvalidOperationException("A later scrape differs from the first:" + Environment.NewLine + scrapes[0] + Environment.NewLine + "----" + Environment.NewLine + scrapes[1]);
            }

            return true;
        });

    [Property(MaxTest = 100)]
    public Property ValidatorFlagsKnownBadExpositions() => Prop.ForAll(
        Gen.Elements(
        new (string, bool, ErrorKind?)[]
        {
            ("# TYPE a gauge\n# TYPE a gauge\na 1\n", false, ErrorKind.Structure),
            ("# TYPE a gauge\na 1\n# TYPE b gauge\nb 1\na{x=\"1\"} 2\n", false, ErrorKind.Structure),
            ("# TYPE a gauge\na{x=\"1\",x=\"2\"} 1\n", false, ErrorKind.DuplicateLabels),
            ("# TYPE a gauge\na{x=\"1\"} 1\na{x=\"1\"} 2\n", false, ErrorKind.DuplicateSeries),
            ("# TYPE a gauge\na{x=\"1\",y=\"2\"} 1\na{y=\"2\",x=\"1\"} 2\n", false, ErrorKind.DuplicateSeries),
            ("# TYPE a gauge\na{k1=\"a,k2=b\"} 1\na{k1=\"a\",k2=\"b\"} 2\n", false, null),
            ("# TYPE h histogram\nh_bucket{le=\"+Inf\"} 1\nh_sum{} 1\nh_count{} 1\n# TYPE h_sum gauge\nh_sum{} 5\n", false, ErrorKind.DuplicateSeries),
            ("# TYPE a gauge\na-b 1\n", false, ErrorKind.Syntax),
            ("# TYPE a gauge\na{x=\"1\"} one\n", false, ErrorKind.Syntax),
            ("# TYPE a gauge\na 1\n", true, ErrorKind.Directive),
            ("# TYPE a gauge\na 1\n# EOF\n", false, ErrorKind.Directive),
            ("# TYPE a counter\na 1\n# EOF\n", true, ErrorKind.CounterSuffix),
            ("# TYPE a gauge\n# UNIT a seconds\na 1\n# EOF\n", true, ErrorKind.Unit),
            ("# TYPE h histogram\nh_bucket{le=\"1\"} 1\nh_count 1\nh_sum 1\n", false, ErrorKind.Histogram),
            ("# TYPE a gauge\n# HELP a help\na{x=\"1\"} 1\n", false, null),
            ("# TYPE a_seconds gauge\n# UNIT a_seconds seconds\na_seconds 1\n# EOF\n", true, null),
        }).ToArbitrary(),
        static (example) =>
        {
            var protocol = example.Item2
                ? new PrometheusProtocol(PrometheusProtocol.OpenMetricsMediaType, null, PrometheusProtocol.OpenMetricsV0, isOpenMetrics: true)
                : new PrometheusProtocol(PrometheusProtocol.PrometheusTextMediaType, null, PrometheusProtocol.PrometheusV0, isOpenMetrics: false);

            var errors = Validate(example.Item1, protocol);

            return example.Item3 is { } kind ? errors.Any(e => e.Kind == kind) : errors.Count == 0;
        });

    private static string Scrape(Scenario scenario) => ScrapeAll(scenario, scrapes: 1)[0];

    private static List<string> ScrapeAll(Scenario scenario, int scrapes)
    {
#pragma warning disable CA2000 // Dispose objects before losing scope
        var exporter = new PrometheusExporter(new PrometheusExporterOptions
        {
            ScopeInfoEnabled = scenario.ScopeInfoEnabled,
            TargetInfoEnabled = scenario.TargetInfoEnabled,
            DisableTotalNameSuffixForCounters = scenario.DisableTotalNameSuffixForCounters,
            ScrapeResponseCacheDurationMilliseconds = 0,
            TranslationStrategy = scenario.Strategy,
        });

        var reader = new BaseExportingMetricReader(exporter)
        {
            TemporalityPreference = MetricReaderTemporalityPreference.Cumulative,
        };

#pragma warning restore CA2000 // Dispose objects before losing scope

        var builder = Sdk.CreateMeterProviderBuilder()
            .ConfigureResource(resource => resource.AddAttributes(scenario.ResourceAttributes.Select(static a => new KeyValuePair<string, object>(a.Key, a.Value))))
            .AddReader(reader);

        foreach (var meterSpec in scenario.Meters)
        {
            builder.AddMeter(meterSpec.Name);
        }

        // A histogram without any buckets only has its sum and count.
        foreach (var name in scenario.Meters.SelectMany(static m => m.Instruments).Where(static i => i.Kind == InstrumentKind.Histogram && i.EmptyBuckets).Select(static i => i.Name).Distinct(StringComparer.Ordinal))
        {
            builder.AddView(name, new ExplicitBucketHistogramConfiguration { Boundaries = [] });
        }

        using var provider = builder.Build();

        var meters = new List<Meter>();

        try
        {
            foreach (var meterSpec in scenario.Meters)
            {
                var meter = new Meter(new MeterOptions(meterSpec.Name)
                {
                    Version = meterSpec.Version,
                    Tags = meterSpec.Tags.Select(static t => new KeyValuePair<string, object?>(t.Key, t.Value)).ToList(),
                });

                meters.Add(meter);

                foreach (var instrument in meterSpec.Instruments)
                {
                    Record(meter, instrument);
                }
            }

            var results = new List<string>();

            for (var i = 0; i < scrapes; i++)
            {
                results.Add(Collect(exporter, scenario.EffectiveProtocol));
            }

            return results;
        }
        finally
        {
            foreach (var meter in meters)
            {
                meter.Dispose();
            }
        }
    }

    private static string Collect(PrometheusExporter exporter, PrometheusProtocol protocol)
    {
#if NET
        var response = exporter.CollectionManager.EnterCollect(protocol).AsTask().GetAwaiter().GetResult();
#else
        var response = exporter.CollectionManager.EnterCollect(protocol).GetAwaiter().GetResult();
#endif

        try
        {
            // Strict decoding: the exposition must always be valid UTF-8, whatever the tag values were.
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                .GetString(response.View.Array!, response.View.Offset, response.View.Count);
        }
        finally
        {
            exporter.CollectionManager.ExitCollect(response);
        }
    }

    private static void Record(Meter meter, InstrumentSpec spec)
    {
        switch (spec.Kind)
        {
            case InstrumentKind.CounterLong:
                {
                    var instrument = meter.CreateCounter<long>(spec.Name, spec.Unit, spec.Description);
                    foreach (var m in spec.Measurements)
                    {
                        instrument.Add((long)Math.Abs(Math.Truncate(Clamp(m.Value))), ToTagList(m.Tags));
                    }

                    break;
                }

            case InstrumentKind.CounterDouble:
                {
                    var instrument = meter.CreateCounter<double>(spec.Name, spec.Unit, spec.Description);
                    foreach (var m in spec.Measurements)
                    {
                        instrument.Add(Math.Abs(m.Value), ToTagList(m.Tags));
                    }

                    break;
                }

            case InstrumentKind.UpDownCounterLong:
                {
                    var instrument = meter.CreateUpDownCounter<long>(spec.Name, spec.Unit, spec.Description);
                    foreach (var m in spec.Measurements)
                    {
                        instrument.Add((long)Math.Truncate(Clamp(m.Value)), ToTagList(m.Tags));
                    }

                    break;
                }

            case InstrumentKind.Gauge:
                {
                    var instrument = meter.CreateGauge<double>(spec.Name, spec.Unit, spec.Description);
                    foreach (var m in spec.Measurements)
                    {
                        instrument.Record(m.Value, ToTagList(m.Tags));
                    }

                    break;
                }

            case InstrumentKind.Histogram:
                {
                    var instrument = meter.CreateHistogram<double>(spec.Name, spec.Unit, spec.Description);
                    foreach (var m in spec.Measurements)
                    {
                        instrument.Record(m.Value, ToTagList(m.Tags));
                    }

                    break;
                }

            default:
                {
                    var measurements = spec.Measurements
                        .Select(static m => new Measurement<double>(m.Value, ToTagList(m.Tags)))
                        .ToList();

                    meter.CreateObservableGauge(spec.Name, () => measurements, spec.Unit, spec.Description);
                    break;
                }
        }
    }

    private static double Clamp(double value) => double.IsNaN(value) ? 0 : Math.Max(-1e15, Math.Min(1e15, value));

    private static TagList ToTagList(List<KeyValuePair<string, string>> tags)
    {
        var list = default(TagList);

        foreach (var tag in tags)
        {
            list.Add(tag.Key, tag.Value);
        }

        return list;
    }

    private static bool HasNoErrors(Scenario scenario, ErrorKind kind)
    {
        var text = Scrape(scenario);
        var errors = Validate(text, scenario.EffectiveProtocol).Where(e => e.Kind == kind).Select(static e => e.Message).ToList();

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Invalid exposition for " + PrometheusProtocol.GetContentType(scenario.EffectiveProtocol) +
                " (" + scenario.Strategy + "): " + string.Join("; ", errors.Take(5)) + Environment.NewLine +
                "Scenario: " + scenario + Environment.NewLine + text);
        }

        return true;
    }

    private static List<(ErrorKind Kind, string Message)> Validate(string text, PrometheusProtocol protocol)
    {
        var errors = new List<(ErrorKind Kind, string Message)>();
        var validator = new ExpositionValidator(protocol, errors);

        validator.Validate(text);

        return errors;
    }

    private static class Generators
    {
        // Keys that collide with each other once they have been escaped for the exposition format.
        private static readonly string[] TagKeys =
        [
            "a.b",
            "a_b",
            "a-b",
            "a b",
            "http.method",
            "http_method",
            "http.request.method",
            "key",
            "Key",
            "k1",
            "k2",
            "0starts_with_digit",
            "has:colon",
            "unicode_key_\x00E9",
            "x.y.z",
            "x_y_z",
            "ns/attr",
            "aZ",
            "a0",
        ];

        private static readonly string[] InstrumentNames =
        [
            "requests",
            "requests.total",
            "requests_total",
            "http.server.duration",
            "http.server.request.duration",
            "a.b",
            "a_b",
            "a-b",
            "queue.length",
            "queue_length",
            "temperature",
            "bytes",
            "size_bytes",
            "Count",
            "x",
            "total",
            "a/b",
            "latency",
            "latency_sum",
            "latency_count",
            "latency_bucket",
            "events",
            "events_created",
            "events_total",
        ];

        private static readonly string[] MeterNames =
        [
            "meter",
            "MyCompany.MyProduct",
            "my-meter",
            "my.meter.v2",
            "Meter With Spaces",
            "meter\x00E9",
        ];

        private static readonly string[] Units =
        [
            null!,
            string.Empty,
            "By",
            "s",
            "ms",
            "1",
            "%",
            "{request}",
            "Cel",
            "By/s",
            "a b",
            "unit.with.dots",
            "\x00E9",
            "bytes",
            "seconds",
            "ratio",
        ];

        private static readonly Gen<char> TextChar = Gen.Frequency(
            (8, Gen.Choose(0x20, 0x7E).Select(static c => (char)c)),
            (2, Gen.Elements('\n', '\r', '\t', '"', Backslash, '{', '}', ',', '=', '#')),
            (2, Gen.Choose(0xA0, 0xD7FF).Select(static c => (char)c)),
            (1, Gen.Choose(0xD800, 0xDFFF).Select(static c => (char)c)),
            (1, Gen.Choose(0x00, 0x1F).Select(static c => (char)c)));

        private static readonly Gen<string> TextGen = Gen.Sized(size =>
            from length in Gen.Choose(0, Math.Min(size + 2, 40))
            from chars in Gen.ArrayOf(TextChar, length)
            select new string(chars));

        private static readonly Gen<double> ValueGen = Gen.Frequency(
            (6, Gen.Choose(0, 1000).Select(static v => (double)v)),
            (3, Gen.Choose(-100000, 100000).Select(static v => v / 8.0)),
            (1, Gen.Elements(0.0, 1.0, -1.0, 0.005, 0.1, 1e-9, 1e15, 1e21, 1e300, double.Epsilon, double.MaxValue, double.MinValue, double.NaN, double.PositiveInfinity, double.NegativeInfinity)));

        public static Arbitrary<Scenario> ScenarioArbitrary() => ScenarioGen().ToArbitrary();

        private static Gen<Scenario> ScenarioGen() =>
            from strategy in Gen.Elements(
                PrometheusTranslationStrategy.UnderscoreEscapingWithSuffixes,
                PrometheusTranslationStrategy.UnderscoreEscapingWithoutSuffixes,
                PrometheusTranslationStrategy.NoUTF8EscapingWithSuffixes,
                PrometheusTranslationStrategy.NoTranslation)
            from scopeInfo in Gen.Elements(true, false)
            from targetInfo in Gen.Elements(true, false)
            from disableTotal in Gen.Elements(true, false)
            from protocol in ProtocolGen()
            from resourceCount in Gen.Choose(0, 3)
            from resource in Gen.ArrayOf(ResourceAttributeGen(), resourceCount)
            from meterCount in Gen.Choose(1, 3)
            from meters in Gen.ArrayOf(MeterGen(), meterCount)
            select CreateScenario(strategy, scopeInfo, targetInfo, disableTotal, protocol, resource, meters);

        private static Scenario CreateScenario(
            PrometheusTranslationStrategy strategy,
            bool scopeInfo,
            bool targetInfo,
            bool disableTotal,
            PrometheusProtocol protocol,
            KeyValuePair<string, string>[] resource,
            MeterSpec[] meters)
        {
            // Mirror what the HTTP listener does with the protocol it negotiated before it collects.
            var effective = PrometheusProtocol.ApplyTranslationStrategy(protocol, strategy);

            return new Scenario
            {
                Strategy = strategy,
                ScopeInfoEnabled = scopeInfo,
                TargetInfoEnabled = targetInfo,
                DisableTotalNameSuffixForCounters = disableTotal,
                EffectiveProtocol = effective,
                ResourceAttributes = resource
                    .GroupBy(static r => r.Key, StringComparer.Ordinal)
                    .Select(static g => g.First())
                    .ToList(),
                Meters = meters
                    .GroupBy(static m => m.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(static g => g.First())
                    .ToList(),
            };
        }

        private static Gen<PrometheusProtocol> ProtocolGen()
        {
            var escapings = new[]
            {
                PrometheusProtocol.UnderscoresEscaping,
                PrometheusProtocol.DotsEscaping,
                PrometheusProtocol.ValuesEscaping,
                PrometheusProtocol.AllowUtf8Escaping,
            };

            var text =
                from escaping in Gen.Elements(escapings)
                select new PrometheusProtocol(PrometheusProtocol.PrometheusTextMediaType, escaping, PrometheusProtocol.PrometheusV1, isOpenMetrics: false);

            var openMetrics =
                from escaping in Gen.Elements(escapings)
                select new PrometheusProtocol(PrometheusProtocol.OpenMetricsMediaType, escaping, PrometheusProtocol.OpenMetricsV1, isOpenMetrics: true);

            var legacyText = new PrometheusProtocol(PrometheusProtocol.PrometheusTextMediaType, null, PrometheusProtocol.PrometheusV0, isOpenMetrics: false);
            var legacyOpenMetrics = new PrometheusProtocol(PrometheusProtocol.OpenMetricsMediaType, null, PrometheusProtocol.OpenMetricsV0, isOpenMetrics: true);

            return Gen.OneOf(Gen.Constant(legacyText), Gen.Constant(legacyOpenMetrics), text, openMetrics);
        }

        private static Gen<KeyValuePair<string, string>> ResourceAttributeGen() =>
            from key in Gen.Elements("service.name", "service.namespace", "service.instance.id", "deployment.environment", "host_name", "a.b", "a_b")
            from value in TextGen
            select new KeyValuePair<string, string>(key, value);

        private static Gen<MeterSpec> MeterGen() =>
            from name in Gen.Elements(MeterNames)
            from version in Gen.Elements(null, "1.0.0", "2.0-beta")
            from tagCount in Gen.Choose(0, 2)
            from tags in Gen.ArrayOf(TagGen(), tagCount)
            from instrumentCount in Gen.Choose(1, 4)
            from instruments in Gen.ArrayOf(InstrumentGen(), instrumentCount)
            select new MeterSpec
            {
                Name = name,
                Version = version,
                Tags = tags.GroupBy(static t => t.Key, StringComparer.Ordinal).Select(static g => g.First()).ToList(),
                Instruments = instruments.GroupBy(static i => i.Name, StringComparer.OrdinalIgnoreCase).Select(static g => g.First()).ToList(),
            };

        private static Gen<KeyValuePair<string, string>> TagGen() =>
            from key in Gen.Elements(TagKeys)
            from value in TextGen
            select new KeyValuePair<string, string>(key, value);

        private static Gen<InstrumentSpec> InstrumentGen() =>
            from kind in Gen.Elements(
                InstrumentKind.CounterLong,
                InstrumentKind.CounterDouble,
                InstrumentKind.UpDownCounterLong,
                InstrumentKind.Gauge,
                InstrumentKind.Histogram,
                InstrumentKind.ObservableGauge)
            from name in Gen.Elements(InstrumentNames)
            from unit in Gen.Elements(Units)
            from description in Gen.OneOf(Gen.Constant<string?>(null), TextGen.Select(static t => (string?)t))
            from emptyBuckets in Gen.Frequency((3, Gen.Constant(false)), (1, Gen.Constant(true)))
            from measurementCount in Gen.Choose(1, 5)
            from measurements in Gen.ArrayOf(MeasurementGen(), measurementCount)
            select new InstrumentSpec
            {
                Kind = kind,
                Name = name,
                Unit = unit,
                Description = description,
                EmptyBuckets = emptyBuckets,
                Measurements = measurements.ToList(),
            };

        private static Gen<MeasurementSpec> MeasurementGen() =>
            from value in ValueGen
            from tagCount in Gen.Choose(0, 4)
            from tags in Gen.ArrayOf(TagGen(), tagCount)
            select new MeasurementSpec
            {
                Value = value,
                Tags = tags.GroupBy(static t => t.Key, StringComparer.Ordinal).Select(static g => g.First()).ToList(),
            };
    }

    private sealed class ExpositionValidator(PrometheusProtocol protocol, List<(ErrorKind Kind, string Message)> errors)
    {
        private static readonly string[] SampleSuffixes =
        [
            "_total",
            "_created",
            "_bucket",
            "_count",
            "_sum",
            "_gcount",
            "_gsum",
            "_info",
        ];

        private readonly bool openMetrics = protocol.IsOpenMetrics;
        private readonly bool allowUtf8Names = protocol.Escaping == PrometheusProtocol.AllowUtf8Escaping;
        private readonly Dictionary<string, Family> families = new(StringComparer.Ordinal);
        private readonly HashSet<string> series = new(StringComparer.Ordinal);
        private Family? current;
        private int lineNumber;

        public void Validate(string text)
        {
            if (text.Length == 0)
            {
                return;
            }

            if (text[text.Length - 1] != '\n')
            {
                this.Error("the exposition does not end with a line feed");
            }

            var lines = text.Split('\n');

            for (var i = 0; i < lines.Length - 1; i++)
            {
                this.lineNumber = i + 1;
                this.ValidateLine(lines[i]);
            }

            this.lineNumber = 0;

            if (this.openMetrics && (lines.Length < 2 || lines[lines.Length - 2] != "# EOF"))
            {
                this.Error("the OpenMetrics exposition does not end with # EOF");
            }

            foreach (var family in this.families.Values)
            {
                this.ValidateFamily(family);
            }
        }

        // The label names and values are not escaped in the key, so each is prefixed with its length so that the
        // key is different for every label set (otherwise {k1="a,k2=b"} and {k1="a",k2="b"} would have the same key).
        private static string GetLabelSetKey(IEnumerable<KeyValuePair<string, string>> labels)
            => string.Join(
                ",",
                labels
                    .OrderBy(static l => l.Key, StringComparer.Ordinal)
                    .Select(static l => l.Key.Length.ToString(CultureInfo.InvariantCulture) + ":" + l.Key + "=" + l.Value.Length.ToString(CultureInfo.InvariantCulture) + ":" + l.Value));

        private static bool BelongsTo(string family, string sample)
            => sample == family || (sample.StartsWith(family, StringComparison.Ordinal) && Array.IndexOf(SampleSuffixes, sample.Substring(family.Length)) >= 0);

        private static bool IsValidEscapedText(string text, bool openMetrics)
        {
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] == Backslash)
                {
                    if (i + 1 >= text.Length || (text[i + 1] != Backslash && text[i + 1] != 'n' && !(openMetrics && text[i + 1] == '"')))
                    {
                        return false;
                    }

                    i++;
                }
            }

            return true;
        }

        private static bool TryParseNumber(string text, out double value)
        {
            if (text is "+Inf" or "Inf")
            {
                value = double.PositiveInfinity;
                return true;
            }

            if (text == "-Inf")
            {
                value = double.NegativeInfinity;
                return true;
            }

            if (text == "NaN")
            {
                value = double.NaN;
                return true;
            }

            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && !(text.Length > 0 && text[0] == '+');
        }

        private static int CountRunes(string text)
        {
            var count = 0;

            for (var i = 0; i < text.Length; i++)
            {
                if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    i++;
                }

                count++;
            }

            return count;
        }

        private static bool TryReadQuoted(string text, ref int index, out string value)
        {
            value = string.Empty;

            if (index >= text.Length || text[index] != '"')
            {
                return false;
            }

            var builder = new StringBuilder();
            var i = index + 1;

            while (i < text.Length)
            {
                var c = text[i];

                if (c == '"')
                {
                    value = builder.ToString();
                    index = i + 1;
                    return true;
                }

                if (c == Backslash)
                {
                    if (i + 1 >= text.Length || (text[i + 1] != Backslash && text[i + 1] != '"' && text[i + 1] != 'n'))
                    {
                        return false;
                    }

                    builder.Append(text[i + 1] == 'n' ? '\n' : text[i + 1]);
                    i += 2;
                    continue;
                }

                builder.Append(c);
                i++;
            }

            return false;
        }

        private static bool IsLegacyName(string name, bool allowColon)
        {
            if (name.Length == 0)
            {
                return false;
            }

            for (var i = 0; i < name.Length; i++)
            {
                var c = name[i];
                var valid = (c is >= 'a' and <= 'z') || (c is >= 'A' and <= 'Z') || c == '_' || (allowColon && c == ':') || (i > 0 && c is >= '0' and <= '9');

                if (!valid)
                {
                    return false;
                }
            }

            return true;
        }

        private static ErrorKind Categorize(string message)
        {
            if (message.StartsWith("second TYPE", StringComparison.Ordinal) ||
                message.StartsWith("metric family", StringComparison.Ordinal) ||
                message.StartsWith("sample for a metric without", StringComparison.Ordinal))
            {
                return ErrorKind.Structure;
            }

            if (message.StartsWith("# ", StringComparison.Ordinal) || message.StartsWith("the OpenMetrics", StringComparison.Ordinal))
            {
                // Checked by the properties which are specific to the end of file marker and the OpenMetrics only directives.
                return ErrorKind.Directive;
            }

            if (message.StartsWith("duplicate label", StringComparison.Ordinal))
            {
                return ErrorKind.DuplicateLabels;
            }

            if (message.StartsWith("duplicate series", StringComparison.Ordinal))
            {
                return ErrorKind.DuplicateSeries;
            }

            if (message.StartsWith("counter sample", StringComparison.Ordinal))
            {
                return ErrorKind.CounterSuffix;
            }

            if (message.StartsWith("unit '", StringComparison.Ordinal))
            {
                return ErrorKind.Unit;
            }

            return message.StartsWith("histogram", StringComparison.Ordinal) ? ErrorKind.Histogram : ErrorKind.Syntax;
        }

        private void ValidateLine(string line)
        {
            if (line.Length == 0)
            {
                this.Error("empty line");
                return;
            }

            if (line == "# EOF")
            {
                if (!this.openMetrics)
                {
                    this.Error("# EOF in a format which does not define it");
                }

                return;
            }

            if (line[0] == '#')
            {
                this.ValidateMetadata(line);
                return;
            }

            this.ValidateSample(line);
        }

        private void ValidateMetadata(string line)
        {
            var parts = line.Split(' ');

            if (parts.Length < 3 || parts[0] != "#" || parts[1] is not ("HELP" or "TYPE" or "UNIT"))
            {
                this.Error("malformed metadata line: " + line);
                return;
            }

            var directive = parts[1];
            var index = 2 + directive.Length + 1;

            if (!this.TryReadName(line, ref index, out var name))
            {
                this.Error("invalid metric name in metadata line: " + line);
                return;
            }

            var rest = index < line.Length && line[index] == ' ' ? line.Substring(index + 1) : (index == line.Length ? string.Empty : null);

            if (rest is null)
            {
                this.Error("malformed metadata line: " + line);
                return;
            }

            var family = this.Enter(name);

            switch (directive)
            {
                case "TYPE":
                    if (family.Type is not null)
                    {
                        this.Error("second TYPE line for " + name);
                    }

                    family.Type = rest;

                    var known = this.openMetrics
                        ? rest is "counter" or "gauge" or "histogram" or "gaugehistogram" or "summary" or "info" or "stateset" or "unknown"
                        : rest is "counter" or "gauge" or "histogram" or "summary" or "untyped";

                    if (!known)
                    {
                        this.Error("unknown metric type '" + rest + "' for " + name);
                    }

                    break;
                case "UNIT":
                    if (!this.openMetrics)
                    {
                        this.Error("# UNIT in a format which does not define it");
                    }

                    family.Unit = rest;
                    break;
                default:
                    if (!IsValidEscapedText(rest, this.openMetrics))
                    {
                        this.Error("invalid escape sequence in HELP text for " + name);
                    }

                    break;
            }
        }

        private void ValidateSample(string line)
        {
            var index = 0;
            string name;
            var labels = new List<KeyValuePair<string, string>>();

            if (line[0] == '{')
            {
                // {"metric.name",label="value"}
                index = 1;

                if (!this.allowUtf8Names || !TryReadQuoted(line, ref index, out name))
                {
                    this.Error("invalid quoted metric name: " + line);
                    return;
                }

                if (index < line.Length && line[index] == ',')
                {
                    index++;
                }

                if (!this.TryReadLabels(line, ref index, labels, closing: '}'))
                {
                    return;
                }
            }
            else
            {
                if (!this.TryReadName(line, ref index, out name))
                {
                    this.Error("invalid metric name: " + line);
                    return;
                }

                if (index < line.Length && line[index] == '{')
                {
                    index++;

                    if (!this.TryReadLabels(line, ref index, labels, closing: '}'))
                    {
                        return;
                    }
                }
            }

            if (index >= line.Length || line[index] != ' ')
            {
                this.Error("missing value: " + line);
                return;
            }

            index++;

            var end = Array.IndexOf(line.ToCharArray(), ' ', index);
            var valueText = end < 0 ? line.Substring(index) : line.Substring(index, end - index);

            if (!TryParseNumber(valueText, out var value))
            {
                this.Error("invalid sample value '" + valueText + "': " + line);
                return;
            }

            if (end >= 0)
            {
                var tail = line.Substring(end + 1);

                if (this.openMetrics && tail.StartsWith("# ", StringComparison.Ordinal))
                {
                    this.ValidateExemplar(tail.Substring(2), line);
                }
                else if (!TryParseNumber(tail, out _))
                {
                    this.Error("invalid timestamp: " + line);
                }
            }

            this.RecordSample(name, labels, value, line);
        }

        private void ValidateExemplar(string text, string line)
        {
            var index = 0;
            var labels = new List<KeyValuePair<string, string>>();

            if (text.Length == 0 || text[0] != '{')
            {
                this.Error("invalid exemplar: " + line);
                return;
            }

            index++;

            if (!this.TryReadLabels(text, ref index, labels, closing: '}'))
            {
                return;
            }

            var runes = labels.Sum(static l => CountRunes(l.Key) + CountRunes(l.Value));

            if (runes > 128)
            {
                this.Error("exemplar label set is " + runes.ToString(CultureInfo.InvariantCulture) + " characters, which exceeds 128: " + line);
            }

            var parts = text.Substring(index).Split([' '], StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length is < 1 or > 2 || !parts.All(static p => TryParseNumber(p, out _)))
            {
                this.Error("invalid exemplar value or timestamp: " + line);
            }
        }

        private void RecordSample(string name, List<KeyValuePair<string, string>> labels, double value, string line)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var label in labels)
            {
                if (!seen.Add(label.Key))
                {
                    this.Error("duplicate label name '" + label.Key + "': " + line);
                }
            }

            var seriesKey = name.Length.ToString(CultureInfo.InvariantCulture) + ":" + name + "{" + GetLabelSetKey(labels) + "}";

            if (!this.series.Add(seriesKey))
            {
                this.Error("duplicate series, the same metric name and label set appears more than once: " + line);
            }

            var family = this.current;

            if (family is null || !BelongsTo(family.Name, name))
            {
                // The sample is not part of the family that is open, so the family it belongs to is new.
                family = this.Enter(name);

                if (family.Type is null)
                {
                    this.Error("sample for a metric without a TYPE line: " + line);
                }
            }

            if (this.openMetrics && family.Type == "counter" && !name.EndsWith("_total", StringComparison.Ordinal) && !name.EndsWith("_created", StringComparison.Ordinal))
            {
                this.Error("counter sample does not end in _total: " + line);
            }

            if (family.Type == "histogram")
            {
                family.Samples.Add((name, labels, value));
            }
        }

        private void ValidateFamily(Family family)
        {
            if (this.openMetrics && family.Unit is { Length: > 0 } unit && !family.Name.EndsWith("_" + unit, StringComparison.Ordinal))
            {
                this.Error("unit '" + unit + "' is not a suffix of metric family '" + family.Name + "'");
            }

            if (family.Type != "histogram")
            {
                return;
            }

            foreach (var group in family.Samples.GroupBy(static s => GetLabelSetKey(s.Labels.Where(static l => l.Key != "le"))))
            {
                var buckets = group.Where(static s => s.Name.EndsWith("_bucket", StringComparison.Ordinal)).ToList();
                var counts = group.Where(static s => s.Name.EndsWith("_count", StringComparison.Ordinal)).ToList();
                var sums = group.Where(static s => s.Name.EndsWith("_sum", StringComparison.Ordinal)).ToList();

                if (counts.Count > 1 || sums.Count > 1)
                {
                    // Reported as a duplicate series.
                    continue;
                }

                if (counts.Count != 1 || sums.Count != 1)
                {
                    this.Error("histogram '" + family.Name + "' does not have exactly one _count and one _sum");
                    continue;
                }

                if (buckets.Count == 0)
                {
                    // A histogram which is configured to have no buckets only has a sum and a count.
                    continue;
                }

                var previousLe = double.NegativeInfinity;
                var previousCount = 0.0;

                foreach (var bucket in buckets)
                {
                    var le = bucket.Labels.Where(static l => l.Key == "le").Select(static l => l.Value).FirstOrDefault();

                    if (le is null || !TryParseNumber(le, out var bound))
                    {
                        this.Error("histogram bucket in '" + family.Name + "' has no valid le label");
                        break;
                    }

                    if (bound <= previousLe || bucket.Value < previousCount)
                    {
                        this.Error("histogram '" + family.Name + "' buckets are not ordered and cumulative");
                        break;
                    }

                    previousLe = bound;
                    previousCount = bucket.Value;
                }

                if (!double.IsPositiveInfinity(previousLe))
                {
                    this.Error("histogram '" + family.Name + "' does not end with a +Inf bucket");
                }
                else if (previousCount != counts[0].Value)
                {
                    this.Error("histogram '" + family.Name + "' _count does not equal its +Inf bucket");
                }
            }
        }

        private bool TryReadName(string text, ref int index, out string name)
        {
            name = string.Empty;

            if (index < text.Length && text[index] == '"')
            {
                return this.allowUtf8Names && TryReadQuoted(text, ref index, out name);
            }

            var start = index;

            while (index < text.Length && text[index] is not (' ' or '{'))
            {
                index++;
            }

            name = text.Substring(start, index - start);

            return IsLegacyName(name, allowColon: true);
        }

        private bool TryReadLabels(string text, ref int index, List<KeyValuePair<string, string>> labels, char closing)
        {
            while (true)
            {
                if (index >= text.Length)
                {
                    this.Error("unterminated label set: " + text);
                    return false;
                }

                if (text[index] == closing)
                {
                    index++;
                    return true;
                }

                string labelName;

                if (text[index] == '"')
                {
                    if (!this.allowUtf8Names || !TryReadQuoted(text, ref index, out labelName))
                    {
                        this.Error("invalid quoted label name: " + text);
                        return false;
                    }
                }
                else
                {
                    var start = index;

                    while (index < text.Length && text[index] != '=')
                    {
                        index++;
                    }

                    labelName = text.Substring(start, index - start);

                    if (!IsLegacyName(labelName, allowColon: false))
                    {
                        this.Error("invalid label name '" + labelName + "': " + text);
                        return false;
                    }
                }

                if (index >= text.Length || text[index] != '=')
                {
                    this.Error("label without a value: " + text);
                    return false;
                }

                index++;

                if (!TryReadQuoted(text, ref index, out var labelValue))
                {
                    this.Error("invalid label value for '" + labelName + "': " + text);
                    return false;
                }

                labels.Add(new(labelName, labelValue));

                if (index < text.Length && text[index] == ',')
                {
                    index++;
                }
                else if (index >= text.Length || text[index] != closing)
                {
                    this.Error("expected ',' or '" + closing + "' after label '" + labelName + "': " + text);
                    return false;
                }
            }
        }

        private Family Enter(string name)
        {
            if (this.current?.Name == name)
            {
                return this.current;
            }

            if (this.families.TryGetValue(name, out var existing))
            {
                this.Error("metric family '" + name + "' appears more than once, or its lines are not contiguous");
                this.current = existing;
                return existing;
            }

            var family = new Family(name);

            this.families.Add(name, family);
            this.current = family;

            return family;
        }

        private void Error(string message)
            => errors.Add((Categorize(message), (this.lineNumber > 0 ? "line " + this.lineNumber.ToString(CultureInfo.InvariantCulture) + ": " : string.Empty) + message));

        private sealed class Family(string name)
        {
            public string Name { get; } = name;

            public string? Type { get; set; }

            public string? Unit { get; set; }

            public List<(string Name, List<KeyValuePair<string, string>> Labels, double Value)> Samples { get; } = [];
        }
    }

    private sealed class Scenario
    {
        public PrometheusTranslationStrategy Strategy { get; set; }

        public bool ScopeInfoEnabled { get; set; }

        public bool TargetInfoEnabled { get; set; }

        public bool DisableTotalNameSuffixForCounters { get; set; }

        public PrometheusProtocol EffectiveProtocol { get; set; }

        public List<KeyValuePair<string, string>> ResourceAttributes { get; set; } = [];

        public List<MeterSpec> Meters { get; set; } = [];

        public override string ToString()
        {
            var builder = new StringBuilder();

            builder.Append("scope_info=").Append(this.ScopeInfoEnabled)
                .Append(" target_info=").Append(this.TargetInfoEnabled)
                .Append(" disable_total=").Append(this.DisableTotalNameSuffixForCounters)
                .Append(" resource=[").Append(string.Join(", ", this.ResourceAttributes.Select(static r => Quote(r.Key) + "=" + Quote(r.Value)))).Append(']');

            foreach (var meter in this.Meters)
            {
                builder.AppendLine().Append("  meter ").Append(Quote(meter.Name)).Append(" tags=[").Append(string.Join(", ", meter.Tags.Select(static t => Quote(t.Key) + "=" + Quote(t.Value)))).Append(']');

                foreach (var instrument in meter.Instruments)
                {
                    builder.AppendLine().Append("    ").Append(instrument.Kind).Append(' ').Append(Quote(instrument.Name)).Append(" unit=").Append(instrument.Unit is null ? "null" : Quote(instrument.Unit));

                    foreach (var measurement in instrument.Measurements)
                    {
                        builder.AppendLine().Append("      ").Append(measurement.Value.ToString("R", CultureInfo.InvariantCulture)).Append(' ').Append(string.Join(", ", measurement.Tags.Select(static t => Quote(t.Key) + "=" + Quote(t.Value))));
                    }
                }
            }

            return builder.ToString();
        }

        private static string Quote(string value)
        {
            var builder = new StringBuilder("\"");

            foreach (var c in value)
            {
                if (c is < ' ' or > '~')
                {
                    builder.Append("<0x").Append(((int)c).ToString("X", CultureInfo.InvariantCulture)).Append('>');
                }
                else
                {
                    builder.Append(c);
                }
            }

            return builder.Append('"').ToString();
        }
    }

    private sealed class MeterSpec
    {
        public string Name { get; set; } = string.Empty;

        public string? Version { get; set; }

        public List<KeyValuePair<string, string>> Tags { get; set; } = [];

        public List<InstrumentSpec> Instruments { get; set; } = [];
    }

    private sealed class InstrumentSpec
    {
        public InstrumentKind Kind { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Unit { get; set; }

        public string? Description { get; set; }

        public bool EmptyBuckets { get; set; }

        public List<MeasurementSpec> Measurements { get; set; } = [];
    }

    private sealed class MeasurementSpec
    {
        public double Value { get; set; }

        public List<KeyValuePair<string, string>> Tags { get; set; } = [];
    }
}

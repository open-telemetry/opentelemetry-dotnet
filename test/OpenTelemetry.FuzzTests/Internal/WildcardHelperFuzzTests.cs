// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace OpenTelemetry.FuzzTests;

/// <summary>
/// Fuzz tests for <see cref="WildcardHelper"/>, which converts user supplied <c>AddSource()</c>/<c>AddMeter()</c>
/// wildcard patterns into regular expressions that are then run against arbitrary source and instrument names.
/// </summary>
public class WildcardHelperFuzzTests
{
    private const int MaxTests = 200;

    // Matching is allowed to be slow-ish for pathological inputs, but must never approach the regex timeout.
    private static readonly TimeSpan MaximumMatchDuration = TimeSpan.FromMilliseconds(500);

    [Property(MaxTest = MaxTests)]
    public Property SinglePatternMatchesReferenceGlobMatcher() => Prop.ForAll(
        Generators.PatternAndInputArbitrary(),
        static (scenario) =>
        {
            var regex = WildcardHelper.GetWildcardRegex([scenario.Pattern]);

            return regex.IsMatch(scenario.Input) == ReferenceGlobMatch(scenario.Pattern, scenario.Input);
        });

    [Property(MaxTest = MaxTests)]
    public Property MultiplePatternsMatchReferenceGlobMatcher() => Prop.ForAll(
        Generators.PatternsAndInputArbitrary(),
        static (scenario) =>
        {
            var regex = WildcardHelper.GetWildcardRegex(scenario.Patterns);
            var expected = scenario.Patterns.Any(p => ReferenceGlobMatch(p, scenario.Input));

            return regex.IsMatch(scenario.Input) == expected;
        });

    [Property(MaxTest = MaxTests)]
    public Property NameContainingLineTerminatorIsMatchedByStarWildcard() => Prop.ForAll(
        Generators.SegmentArbitrary(),
        Generators.SegmentArbitrary(),
        Generators.LineTerminatorArbitrary(),
        static (prefix, suffix, terminator) =>
        {
            var regex = WildcardHelper.GetWildcardRegex(["*"]);

            return regex.IsMatch(prefix + terminator + suffix);
        });

    [Property(MaxTest = MaxTests)]
    public Property LiteralPatternDoesNotMatchNameWithTrailingNewline() => Prop.ForAll(
        Generators.SegmentArbitrary(),
        static (name) =>
        {
            var regex = WildcardHelper.GetWildcardRegex([name]);

            return regex.IsMatch(name) && !regex.IsMatch(name + "\n");
        });

    [Property(MaxTest = MaxTests)]
    public Property WildcardPrefixAgreesWithRegexForTheSamePattern() => Prop.ForAll(
        Generators.PatternAndInputArbitrary(),
        static (scenario) =>
        {
            if (!WildcardHelper.TryGetWildcardPrefix(scenario.Pattern, out var prefix))
            {
                return true;
            }

            var regex = WildcardHelper.GetWildcardRegex([scenario.Pattern]);

            // The prefix fast path (StartsWith) and the regex path are used interchangeably
            // by the providers, depending on how many patterns are configured.
            var prefixMatch = scenario.Input.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

            return prefix + "*" == scenario.Pattern &&
                   !WildcardHelper.ContainsWildcard(prefix) &&
                   prefixMatch == regex.IsMatch(scenario.Input);
        });

    [Property(MaxTest = MaxTests)]
    public Property TrailingWildcardPatternsAreRecognisedAsPrefixes() => Prop.ForAll(
        Generators.SegmentArbitrary(),
        static (segment) =>
        {
            var literal = new string(segment.Where(static c => c is not ('*' or '?')).ToArray());

            return WildcardHelper.TryGetWildcardPrefix(literal + "*", out var prefix) && prefix == literal;
        });

    [Property(MaxTest = MaxTests)]
    public Property PathologicalPatternsAreMatchedWithinABoundedTime() => Prop.ForAll(
        Generators.PathologicalScenarioArbitrary(),
        static (scenario) =>
        {
            var regex = WildcardHelper.GetWildcardRegex(scenario.Patterns);

            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Deliberately not WildcardHelper.IsMatch(), which swallows timeouts and would hide them.
                var actual = regex.IsMatch(scenario.Input);
                var expected = scenario.Patterns.Any(p => ReferenceGlobMatch(p, scenario.Input));

                return actual == expected && stopwatch.Elapsed < MaximumMatchDuration;
            }
            catch (RegexMatchTimeoutException)
            {
                return false;
            }
        });

    [Property(MaxTest = MaxTests)]
    public Property IsMatchNeverThrowsForArbitraryInputs() => Prop.ForAll(
        Generators.PatternAndInputArbitrary(),
        static (scenario) =>
        {
            var regex = WildcardHelper.GetWildcardRegex([scenario.Pattern]);

            _ = WildcardHelper.IsMatch(regex, scenario.Input);

            return true;
        });

    private static string Escape(string value)
    {
        var builder = new StringBuilder("\"");

        foreach (var c in value)
        {
            if (c is < ' ' or > '~')
            {
                builder.Append('<').Append(((int)c).ToString("X", CultureInfo.InvariantCulture));
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.Append('"').ToString();
    }

    /// <summary>
    /// A straightforward dynamic programming glob matcher: <c>*</c> matches any (possibly empty) sequence of
    /// characters, <c>?</c> matches exactly one character, and everything else matches itself ignoring case.
    /// </summary>
    private static bool ReferenceGlobMatch(string pattern, string input)
    {
        var current = new bool[input.Length + 1];
        var next = new bool[input.Length + 1];

        current[0] = true;

        for (var p = 0; p < pattern.Length; p++)
        {
            var c = pattern[p];

            Array.Clear(next, 0, next.Length);

            if (c == '*')
            {
                // next[i] = current[i] (match empty) || next[i - 1] (consume one more character)
                next[0] = current[0];

                for (var i = 1; i <= input.Length; i++)
                {
                    next[i] = current[i] || next[i - 1];
                }
            }
            else
            {
                for (var i = 1; i <= input.Length; i++)
                {
                    next[i] = current[i - 1] && (c == '?' || char.ToUpperInvariant(c) == char.ToUpperInvariant(input[i - 1]));
                }
            }

            (current, next) = (next, current);
        }

        return current[input.Length];
    }

    private static class Generators
    {
        // Letters and digits plus the characters that are special to regular expressions and line terminators.
        private static readonly Gen<char> LiteralChar = Gen.Frequency(
            (6, Gen.Elements("abcABC12".ToCharArray())),
            (3, Gen.Elements(".()[]{}+^$|\\#-_/ \t".ToCharArray())),
            (2, Gen.Elements("\n\r\u0085\x2028\x2029".ToCharArray())));

        private static readonly Gen<char> PatternChar = Gen.Frequency(
            (6, LiteralChar),
            (2, Gen.Constant('*')),
            (2, Gen.Constant('?')));

        public static Arbitrary<string> SegmentArbitrary() => Gen.Sized(size =>
            from length in Gen.Choose(0, Math.Min(size + 1, 12))
            from chars in Gen.ArrayOf(LiteralChar, length)
            select new string(chars)).ToArbitrary();

        public static Arbitrary<string> LineTerminatorArbitrary() =>
            Gen.Elements("\n", "\r", "\r\n", "\u0085", "\x2028", "\x2029").ToArbitrary();

        public static Arbitrary<PatternAndInput> PatternAndInputArbitrary() => PatternAndInputGen().ToArbitrary();

        public static Arbitrary<PatternsAndInput> PatternsAndInputArbitrary()
        {
            var gen =
                from count in Gen.Choose(1, 4)
                from patterns in Gen.ArrayOf(PatternGen(), count)
                from input in InputGen(patterns)
                select new PatternsAndInput { Patterns = patterns, Input = input };

            return gen.ToArbitrary();
        }

        public static Arbitrary<PatternsAndInput> PathologicalScenarioArbitrary()
        {
            // Patterns made of many stars separated by repeated, overlapping segments, which are the shape
            // that causes catastrophic backtracking in naive wildcard to regex conversions.
            var segment = Gen.Elements("a", "aa", "ab", "b", "?", "a?", "?a");

            var pattern =
                from count in Gen.Choose(2, 12)
                from segments in Gen.ArrayOf(segment, count)
                from trailing in Gen.Elements(string.Empty, "b", "c", "*c", "?")
                select "*" + string.Join("*", segments) + trailing;

            var input =
                from length in Gen.Choose(0, 3000)
                from filler in Gen.Elements('a', 'b')
                from terminator in Gen.Elements(string.Empty, "b", "c")
                select new string(filler, length) + terminator;

            var gen =
                from count in Gen.Choose(1, 3)
                from patterns in Gen.ArrayOf(pattern, count)
                from value in input
                select new PatternsAndInput { Patterns = patterns, Input = value };

            return gen.ToArbitrary();
        }

        private static Gen<PatternAndInput> PatternAndInputGen() =>
            from pattern in PatternGen()
            from input in InputGen([pattern])
            select new PatternAndInput { Pattern = pattern, Input = input };

        private static Gen<string> PatternGen() => Gen.Sized(size =>
            from length in Gen.Choose(0, Math.Min((size * 2) + 1, 16))
            from chars in Gen.ArrayOf(PatternChar, length)
            select new string(chars));

        private static Gen<string> InputGen(string[] patterns) =>
            from derive in Gen.Elements(true, true, false)
            from pick in Gen.Choose(0, patterns.Length - 1)
            from filler in Gen.ArrayOf(LiteralChar, 4)
            from fillerLength in Gen.ArrayOf(Gen.Choose(0, 3), 4)
            from random in Gen.Sized(size =>
                from length in Gen.Choose(0, Math.Min((size * 2) + 1, 16))
                from chars in Gen.ArrayOf(LiteralChar, length)
                select new string(chars))
            from mutate in Gen.Elements(true, false, false)
            from mutation in LiteralChar
            select derive ? Derive(patterns[pick], filler, fillerLength, mutate, mutation) : random;

        // Builds an input that (before any mutation) matches the pattern, so that matches are common.
        private static string Derive(string pattern, char[] filler, int[] fillerLength, bool mutate, char mutation)
        {
            var builder = new StringBuilder();
            var fillerIndex = 0;

            foreach (var c in pattern)
            {
                switch (c)
                {
                    case '*':
                        builder.Append(filler[fillerIndex % filler.Length], fillerLength[fillerIndex % fillerLength.Length]);
                        fillerIndex++;
                        break;
                    case '?':
                        builder.Append(filler[fillerIndex % filler.Length]);
                        fillerIndex++;
                        break;
                    default:
                        builder.Append(c);
                        break;
                }
            }

            if (mutate)
            {
                builder.Append(mutation);
            }

            return builder.ToString();
        }
    }

    private sealed class PatternAndInput
    {
        public string Pattern { get; set; } = string.Empty;

        public string Input { get; set; } = string.Empty;

        public override string ToString() => $"Pattern: {Escape(this.Pattern)} Input: {Escape(this.Input)}";
    }

    private sealed class PatternsAndInput
    {
        public string[] Patterns { get; set; } = [];

        public string Input { get; set; } = string.Empty;

        public override string ToString() => $"Patterns: [{string.Join(", ", this.Patterns.Select(Escape))}] Input: {Escape(this.Input.Length > 80 ? this.Input.Substring(0, 80) + "...(" + this.Input.Length + ")" : this.Input)}";
    }
}

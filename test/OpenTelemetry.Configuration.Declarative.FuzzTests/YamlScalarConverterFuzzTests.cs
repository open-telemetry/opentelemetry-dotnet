// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Numerics;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace OpenTelemetry.Configuration.Declarative.FuzzTests;

public class YamlScalarConverterFuzzTests
{
    private const int MaxTests = 500;

    private const string DigitAlphabet = "0123456789abcdef";

    private static readonly BigInteger Two53 = BigInteger.One << 53;

    private static readonly Arbitrary<string> ScalarStringArbitrary = Gen.Sized(size =>
        Gen.ArrayOf(
            Gen.Choose(0, 127).Select(c => (char)c),
            Math.Min(size + 1, 256))
        .Select(chars => new string(chars))).ToArbitrary();

    private static readonly Arbitrary<string> HexDigitsArbitrary = DigitStringGen(16, Gen.Choose(1, 70)).ToArbitrary();

    private static readonly Arbitrary<string> OctalDigitsArbitrary = DigitStringGen(8, Gen.Choose(1, 90)).ToArbitrary();

    [Property(MaxTest = MaxTests)]
    public Property ResolveAndConvertNeverThrowsForArbitraryPlainScalar() =>
        Prop.ForAll(
            ScalarStringArbitrary,
            value =>
            {
                var node = new YamlScalarNode(value) { Style = ScalarStyle.Plain };
                var resolved = YamlScalarResolver.Resolve(node, value);
                _ = YamlScalarConverter.Convert(resolved);
            });

    [Property(MaxTest = MaxTests)]
    public Property HexadecimalIntegerNotationRoundsToNearestDouble() =>
        Prop.ForAll(
            HexDigitsArbitrary,
            digits => AssertRoundsToNearest("0x" + digits, ToBigInteger(digits, 16)));

    [Property(MaxTest = MaxTests)]
    public Property OctalIntegerNotationRoundsToNearestDouble() =>
        Prop.ForAll(
            OctalDigitsArbitrary,
            digits => AssertRoundsToNearest("0o" + digits, ToBigInteger(digits, 8)));

    /// <summary>
    /// Rounds the positive integer <paramref name="value"/> to the nearest binary64, ties to even.
    /// </summary>
    /// <remarks>
    /// This operates on the complete <see cref="BigInteger"/> rather than the converter's bounded
    /// significand and sticky bit, providing an independent oracle for the generated cases.
    /// </remarks>
    /// <param name="value">The non-negative integer to round.</param>
    /// <returns>The nearest <see cref="double"/>, or infinity when the value is out of range.</returns>
    private static double RoundToNearest(BigInteger value)
    {
        if (value.IsZero)
        {
            return 0.0;
        }

        var exponent = Math.Max(0, BitLength(value) - 53);
        var significand = value >> exponent;
        if (exponent > 0)
        {
            var remainder = value - (significand << exponent);
            var midpointComparison = remainder.CompareTo(BigInteger.One << (exponent - 1));
            if (midpointComparison > 0 || (midpointComparison == 0 && !significand.IsEven))
            {
                significand++;
            }
        }

        if (significand == Two53)
        {
            significand >>= 1;
            exponent++;
        }

        return exponent > 971
            ? double.PositiveInfinity
            : (double)significand * Math.Pow(2, exponent);
    }

    private static void AssertRoundsToNearest(string text, BigInteger value)
    {
        var expected = BitConverter.DoubleToInt64Bits(RoundToNearest(value));
        var actual = BitConverter.DoubleToInt64Bits(
            YamlScalarConverter.Convert(new(text, YamlScalarKind.Float)).AsDouble());

        if (expected != actual)
        {
            Assert.Fail($"'{text}' converted to 0x{actual:x16}, but 0x{expected:x16} is the nearest binary64.");
        }
    }

    private static Gen<string> DigitStringGen(int numberBase, Gen<int> lengthGen) =>
        from length in lengthGen
        from digits in Gen.ArrayOf(DigitGen(numberBase), length)
        select new string(digits);

    // Digits are biased towards zero and the largest digit: runs of those are what put a value on
    // or beside a rounding boundary, which uniformly random digits practically never do.
    private static Gen<char> DigitGen(int numberBase) =>
        Gen.Choose(0, (numberBase * 2) - 1).Select(choice => choice switch
        {
            _ when choice < numberBase => DigitAlphabet[choice],
            _ when choice % 2 == 0 => '0',
            _ => DigitAlphabet[numberBase - 1],
        });

    private static BigInteger ToBigInteger(string digits, int numberBase)
    {
        var value = BigInteger.Zero;
        foreach (var digit in digits)
        {
            value = (value * numberBase) + (digit <= '9' ? digit - '0' : digit - 'a' + 10);
        }

        return value;
    }

    // BigInteger.GetBitLength is unavailable on .NET Framework.
    private static int BitLength(BigInteger value)
    {
        var bytes = value.ToByteArray();
        var bits = (bytes.Length - 1) * 8;
        for (var high = bytes[bytes.Length - 1]; high != 0; high >>= 1)
        {
            bits++;
        }

        return bits;
    }
}

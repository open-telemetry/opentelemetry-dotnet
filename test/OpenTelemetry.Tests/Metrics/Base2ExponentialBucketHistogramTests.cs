// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Tests;
using Xunit;
using Xunit.Abstractions;

namespace OpenTelemetry.Metrics.Tests;

public class Base2ExponentialBucketHistogramTests
{
    private readonly ITestOutputHelper output;

    public Base2ExponentialBucketHistogramTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public void ScalingFactorCalculation()
    {
        var histogram = new Base2ExponentialBucketHistogram();

        histogram.Scale = 20;
        Assert.Equal("0 10000010011 0111000101010100011101100101001010111000001011111110", IEEE754Double.FromDouble(histogram.ScalingFactor).ToString());

        histogram.Scale = 19;
        Assert.Equal("0 10000010010 0111000101010100011101100101001010111000001011111110", IEEE754Double.FromDouble(histogram.ScalingFactor).ToString());

        histogram.Scale = 18;
        Assert.Equal("0 10000010001 0111000101010100011101100101001010111000001011111110", IEEE754Double.FromDouble(histogram.ScalingFactor).ToString());

        histogram.Scale = 17;
        Assert.Equal("0 10000010000 0111000101010100011101100101001010111000001011111110", IEEE754Double.FromDouble(histogram.ScalingFactor).ToString());

        histogram.Scale = 16;
        Assert.Equal("0 10000001111 0111000101010100011101100101001010111000001011111110", IEEE754Double.FromDouble(histogram.ScalingFactor).ToString());

        histogram.Scale = 15;
        Assert.Equal("0 10000001110 0111000101010100011101100101001010111000001011111110", IEEE754Double.FromDouble(histogram.ScalingFactor).ToString());

        histogram.Scale = 14;
        Assert.Equal("0 10000001101 0111000101010100011101100101001010111000001011111110", IEEE754Double.FromDouble(histogram.ScalingFactor).ToString());

        histogram.Scale = 13;
        Assert.Equal("0 10000001100 0111000101010100011101100101001010111000001011111110", IEEE754Double.FromDouble(histogram.ScalingFactor).ToString());

        histogram.Scale = 12;
        Assert.Equal("0 10000001011 0111000101010100011101100101001010111000001011111110", IEEE754Double.FromDouble(histogram.ScalingFactor).ToString());

        histogram.Scale = 11;
        Assert.Equal("0 10000001010 0111000101010100011101100101001010111000001011111110", IEEE754Double.FromDouble(histogram.ScalingFactor).ToString());

        histogram.Scale = 10;
        Assert.Equal("0 10000001001 0111000101010100011101100101001010111000001011111110", IEEE754Double.FromDouble(histogram.ScalingFactor).ToString());

        histogram.Scale = 9;
        Assert.Equal("0 10000001000 0111000101010100011101100101001010111000001011111110", IEEE754Double.FromDouble(histogram.ScalingFactor).ToString());

        histogram.Scale = 8;
        Assert.Equal("0 10000000111 0111000101010100011101100101001010111000001011111110", IEEE754Double.FromDouble(histogram.ScalingFactor).ToString());

        histogram.Scale = 7;
        Assert.Equal("0 10000000110 0111000101010100011101100101001010111000001011111110", IEEE754Double.FromDouble(histogram.ScalingFactor).ToString());

        histogram.Scale = 6;
        Assert.Equal("0 10000000101 0111000101010100011101100101001010111000001011111110", IEEE754Double.FromDouble(histogram.ScalingFactor).ToString());

        histogram.Scale = 5;
        Assert.Equal("0 10000000100 0111000101010100011101100101001010111000001011111110", IEEE754Double.FromDouble(histogram.ScalingFactor).ToString());

        histogram.Scale = 4;
        Assert.Equal("0 10000000011 0111000101010100011101100101001010111000001011111110", IEEE754Double.FromDouble(histogram.ScalingFactor).ToString());

        histogram.Scale = 3;
        Assert.Equal("0 10000000010 0111000101010100011101100101001010111000001011111110", IEEE754Double.FromDouble(histogram.ScalingFactor).ToString());

        histogram.Scale = 2;
        Assert.Equal("0 10000000001 0111000101010100011101100101001010111000001011111110", IEEE754Double.FromDouble(histogram.ScalingFactor).ToString());

        histogram.Scale = 1;
        Assert.Equal("0 10000000000 0111000101010100011101100101001010111000001011111110", IEEE754Double.FromDouble(histogram.ScalingFactor).ToString());

        histogram.Scale = 0;
        Assert.Equal("0 01111111111 0111000101010100011101100101001010111000001011111110", IEEE754Double.FromDouble(histogram.ScalingFactor).ToString());

        histogram.Scale = -1;
        Assert.Equal("0 01111111110 0111000101010100011101100101001010111000001011111110", IEEE754Double.FromDouble(histogram.ScalingFactor).ToString());

        histogram.Scale = -2;
        Assert.Equal("0 01111111101 0111000101010100011101100101001010111000001011111110", IEEE754Double.FromDouble(histogram.ScalingFactor).ToString());

        histogram.Scale = -3;
        Assert.Equal("0 01111111100 0111000101010100011101100101001010111000001011111110", IEEE754Double.FromDouble(histogram.ScalingFactor).ToString());

        histogram.Scale = -4;
        Assert.Equal("0 01111111011 0111000101010100011101100101001010111000001011111110", IEEE754Double.FromDouble(histogram.ScalingFactor).ToString());

        histogram.Scale = -5;
        Assert.Equal("0 01111111010 0111000101010100011101100101001010111000001011111110", IEEE754Double.FromDouble(histogram.ScalingFactor).ToString());

        histogram.Scale = -6;
        Assert.Equal("0 01111111001 0111000101010100011101100101001010111000001011111110", IEEE754Double.FromDouble(histogram.ScalingFactor).ToString());

        histogram.Scale = -7;
        Assert.Equal("0 01111111000 0111000101010100011101100101001010111000001011111110", IEEE754Double.FromDouble(histogram.ScalingFactor).ToString());

        histogram.Scale = -8;
        Assert.Equal("0 01111110111 0111000101010100011101100101001010111000001011111110", IEEE754Double.FromDouble(histogram.ScalingFactor).ToString());

        histogram.Scale = -9;
        Assert.Equal("0 01111110110 0111000101010100011101100101001010111000001011111110", IEEE754Double.FromDouble(histogram.ScalingFactor).ToString());

        histogram.Scale = -10;
        Assert.Equal("0 01111110101 0111000101010100011101100101001010111000001011111110", IEEE754Double.FromDouble(histogram.ScalingFactor).ToString());

        histogram.Scale = -11;
        Assert.Equal("0 01111110100 0111000101010100011101100101001010111000001011111110", IEEE754Double.FromDouble(histogram.ScalingFactor).ToString());
    }

    [Fact]
    public void IndexLookupScale0()
    {
        /*
        An exponential bucket histogram with scale = 0.
        The base is 2 ^ (2 ^ 0) = 2.
        The buckets are:
            ...
            bucket[-3]: (1/8, 1/4]
            bucket[-2]: (1/4, 1/2]
            bucket[-1]: (1/2, 1]
            bucket[0]:  (1, 2]
            bucket[1]:  (2, 4]
            bucket[2]:  (4, 8]
            bucket[3]:  (8, 16]
            ...
        */
        var histogram = new Base2ExponentialBucketHistogram(maxBuckets: 2, scale: 0);

        Assert.Equal(-1075, histogram.MapToIndex(IEEE754Double.FromString("0 00000000000 0000000000000000000000000000000000000000000000000001"))); // ~4.9406564584124654E-324 (minimum subnormal positive, double.Epsilon, 2 ^ -1074)
        Assert.Equal(-1074, histogram.MapToIndex(IEEE754Double.FromString("0 00000000000 0000000000000000000000000000000000000000000000000010"))); // double.Epsilon * 2
        Assert.Equal(-1073, histogram.MapToIndex(IEEE754Double.FromString("0 00000000000 0000000000000000000000000000000000000000000000000011"))); // double.Epsilon * 3
        Assert.Equal(-1073, histogram.MapToIndex(IEEE754Double.FromString("0 00000000000 0000000000000000000000000000000000000000000000000100"))); // double.Epsilon * 4
        Assert.Equal(-1072, histogram.MapToIndex(IEEE754Double.FromString("0 00000000000 0000000000000000000000000000000000000000000000000101"))); // double.Epsilon * 5
        Assert.Equal(-1072, histogram.MapToIndex(IEEE754Double.FromString("0 00000000000 0000000000000000000000000000000000000000000000000110"))); // double.Epsilon * 6
        Assert.Equal(-1072, histogram.MapToIndex(IEEE754Double.FromString("0 00000000000 0000000000000000000000000000000000000000000000000111"))); // double.Epsilon * 7
        Assert.Equal(-1072, histogram.MapToIndex(IEEE754Double.FromString("0 00000000000 0000000000000000000000000000000000000000000000001000"))); // double.Epsilon * 8
        Assert.Equal(-1025, histogram.MapToIndex(IEEE754Double.FromString("0 00000000000 0100000000000000000000000000000000000000000000000000"))); // ~5.562684646268003E-309 (2 ^ -1024)
        Assert.Equal(-1024, histogram.MapToIndex(IEEE754Double.FromString("0 00000000000 0100000000000000000000000000000000000000000000000001"))); // ~5.56268464626801E-309
        Assert.Equal(-1024, histogram.MapToIndex(IEEE754Double.FromString("0 00000000000 1000000000000000000000000000000000000000000000000000"))); // ~1.1125369292536007E-308 (2 ^ -1023)
        Assert.Equal(-1023, histogram.MapToIndex(IEEE754Double.FromString("0 00000000000 1000000000000000000000000000000000000000000000000001"))); // ~1.112536929253601E-308
        Assert.Equal(-1023, histogram.MapToIndex(IEEE754Double.FromString("0 00000000000 1111111111111111111111111111111111111111111111111111"))); // ~2.2250738585072009E-308 (maximum subnormal positive)
        Assert.Equal(-1023, histogram.MapToIndex(IEEE754Double.FromString("0 00000000001 0000000000000000000000000000000000000000000000000000"))); // ~2.2250738585072014E-308 (minimum normal positive, 2 ^ -1022)
        Assert.Equal(-1022, histogram.MapToIndex(IEEE754Double.FromString("0 00000000001 0000000000000000000000000000000000000000000000000001"))); // ~2.225073858507202E-308
        Assert.Equal(-8, histogram.MapToIndex(IEEE754Double.FromString("0 01111111000 0000000000000000000000000000000000000000000000000000"))); // 1/128
        Assert.Equal(-7, histogram.MapToIndex(IEEE754Double.FromString("0 01111111001 0000000000000000000000000000000000000000000000000000"))); // 1/64
        Assert.Equal(-6, histogram.MapToIndex(IEEE754Double.FromString("0 01111111010 0000000000000000000000000000000000000000000000000000"))); // 1/32
        Assert.Equal(-5, histogram.MapToIndex(IEEE754Double.FromString("0 01111111011 0000000000000000000000000000000000000000000000000000"))); // 1/16
        Assert.Equal(-4, histogram.MapToIndex(IEEE754Double.FromString("0 01111111100 0000000000000000000000000000000000000000000000000000"))); // 1/8
        Assert.Equal(-3, histogram.MapToIndex(IEEE754Double.FromString("0 01111111101 0000000000000000000000000000000000000000000000000000"))); // 1/4
        Assert.Equal(-2, histogram.MapToIndex(IEEE754Double.FromString("0 01111111110 0000000000000000000000000000000000000000000000000000"))); // 1/2
        Assert.Equal(-1, histogram.MapToIndex(IEEE754Double.FromString("0 01111111110 0000000000000000000000000000000000000000000000000001"))); // ~0.5000000000000001
        Assert.Equal(-1, histogram.MapToIndex(IEEE754Double.FromString("0 01111111111 0000000000000000000000000000000000000000000000000000"))); // 1
        Assert.Equal(0, histogram.MapToIndex(IEEE754Double.FromString("0 01111111111 0000000000000000000000000000000000000000000000000001"))); // ~1.0000000000000002
        Assert.Equal(0, histogram.MapToIndex(IEEE754Double.FromString("0 10000000000 0000000000000000000000000000000000000000000000000000"))); // 2
        Assert.Equal(1, histogram.MapToIndex(IEEE754Double.FromString("0 10000000001 0000000000000000000000000000000000000000000000000000"))); // 4
        Assert.Equal(2, histogram.MapToIndex(IEEE754Double.FromString("0 10000000010 0000000000000000000000000000000000000000000000000000"))); // 8
        Assert.Equal(3, histogram.MapToIndex(IEEE754Double.FromString("0 10000000011 0000000000000000000000000000000000000000000000000000"))); // 16
        Assert.Equal(4, histogram.MapToIndex(IEEE754Double.FromString("0 10000000100 0000000000000000000000000000000000000000000000000000"))); // 32
        Assert.Equal(5, histogram.MapToIndex(IEEE754Double.FromString("0 10000000101 0000000000000000000000000000000000000000000000000000"))); // 64
        Assert.Equal(6, histogram.MapToIndex(IEEE754Double.FromString("0 10000000110 0000000000000000000000000000000000000000000000000000"))); // 128
        Assert.Equal(52, histogram.MapToIndex(IEEE754Double.FromString("0 10000110011 1111111111111111111111111111111111111111111111111111"))); // 9,007,199,254,740,991 (Number.MAX_SAFE_INTEGER, 2 ^ 53 - 1)
        Assert.Equal(52, histogram.MapToIndex(IEEE754Double.FromString("0 10000110100 0000000000000000000000000000000000000000000000000000"))); // 9,007,199,254,740,992 (Number.MAX_SAFE_INTEGER + 1, 2 ^ 53)
        Assert.Equal(1022, histogram.MapToIndex(IEEE754Double.FromString("0 11111111110 0000000000000000000000000000000000000000000000000000"))); // ~8.98846567431158E+307 (2 ^ 1023)
        Assert.Equal(1023, histogram.MapToIndex(IEEE754Double.FromString("0 11111111110 0000000000000000000000000000000000000000000000000001"))); // ~8.988465674311582E+307 (2 ^ 1023 + 1)
        Assert.Equal(1023, histogram.MapToIndex(IEEE754Double.FromString("0 11111111110 1111111111111111111111111111111111111111111111111110"))); // ~1.7976931348623155E+308 (2 ^ 1024 - 2)
        Assert.Equal(1023, histogram.MapToIndex(IEEE754Double.FromString("0 11111111110 1111111111111111111111111111111111111111111111111111"))); // ~1.7976931348623157E+308 (maximum normal positive, double.MaxValue, 2 ^ 1024 - 1)
    }

    [Fact]
    public void IndexLookupScaleMinusOne()
    {
        /*
        An exponential bucket histogram with scale = -1.
        The base is 2 ^ (2 ^ 1) = 4.
        The buckets are:
            ...
            bucket[-3]: (1/64, 1/16]
            bucket[-2]: (1/16, 1/4]
            bucket[-1]: (1/4, 1]
            bucket[0]:  (1, 4]
            bucket[1]:  (4, 16]
            bucket[2]:  (16, 64]
            bucket[3]:  (64, 256]
            ...
        */
        var histogram = new Base2ExponentialBucketHistogram(maxBuckets: 2, scale: -1);

        Assert.Equal(-538, histogram.MapToIndex(IEEE754Double.FromString("0 00000000000 0000000000000000000000000000000000000000000000000001"))); // ~4.9406564584124654E-324 (minimum subnormal positive, double.Epsilon, 2 ^ -1074)
        Assert.Equal(-537, histogram.MapToIndex(IEEE754Double.FromString("0 00000000000 0000000000000000000000000000000000000000000000000010"))); // double.Epsilon * 2
        Assert.Equal(-537, histogram.MapToIndex(IEEE754Double.FromString("0 00000000000 0000000000000000000000000000000000000000000000000011"))); // double.Epsilon * 3
        Assert.Equal(-537, histogram.MapToIndex(IEEE754Double.FromString("0 00000000000 0000000000000000000000000000000000000000000000000100"))); // double.Epsilon * 4
        Assert.Equal(-536, histogram.MapToIndex(IEEE754Double.FromString("0 00000000000 0000000000000000000000000000000000000000000000000101"))); // double.Epsilon * 5
        Assert.Equal(-536, histogram.MapToIndex(IEEE754Double.FromString("0 00000000000 0000000000000000000000000000000000000000000000000110"))); // double.Epsilon * 6
        Assert.Equal(-536, histogram.MapToIndex(IEEE754Double.FromString("0 00000000000 0000000000000000000000000000000000000000000000000111"))); // double.Epsilon * 7
        Assert.Equal(-536, histogram.MapToIndex(IEEE754Double.FromString("0 00000000000 0000000000000000000000000000000000000000000000001000"))); // double.Epsilon * 8
        Assert.Equal(-513, histogram.MapToIndex(IEEE754Double.FromString("0 00000000000 0100000000000000000000000000000000000000000000000000"))); // ~5.562684646268003E-309 (2 ^ -1024)
        Assert.Equal(-512, histogram.MapToIndex(IEEE754Double.FromString("0 00000000000 0100000000000000000000000000000000000000000000000001"))); // ~5.56268464626801E-309
        Assert.Equal(-512, histogram.MapToIndex(IEEE754Double.FromString("0 00000000000 1000000000000000000000000000000000000000000000000000"))); // ~1.1125369292536007E-308 (2 ^ -1023)
        Assert.Equal(-512, histogram.MapToIndex(IEEE754Double.FromString("0 00000000000 1000000000000000000000000000000000000000000000000001"))); // ~1.112536929253601E-308
        Assert.Equal(-512, histogram.MapToIndex(IEEE754Double.FromString("0 00000000000 1111111111111111111111111111111111111111111111111111"))); // ~2.2250738585072009E-308 (maximum subnormal positive)
        Assert.Equal(-512, histogram.MapToIndex(IEEE754Double.FromString("0 00000000001 0000000000000000000000000000000000000000000000000000"))); // ~2.2250738585072014E-308 (minimum normal positive, 2 ^ -1022)
        Assert.Equal(-511, histogram.MapToIndex(IEEE754Double.FromString("0 00000000001 0000000000000000000000000000000000000000000000000001"))); // ~2.225073858507202E-308
        Assert.Equal(-4, histogram.MapToIndex(IEEE754Double.FromString("0 01111111000 0000000000000000000000000000000000000000000000000000"))); // 1/128
        Assert.Equal(-4, histogram.MapToIndex(IEEE754Double.FromString("0 01111111001 0000000000000000000000000000000000000000000000000000"))); // 1/64
        Assert.Equal(-3, histogram.MapToIndex(IEEE754Double.FromString("0 01111111010 0000000000000000000000000000000000000000000000000000"))); // 1/32
        Assert.Equal(-3, histogram.MapToIndex(IEEE754Double.FromString("0 01111111011 0000000000000000000000000000000000000000000000000000"))); // 1/16
        Assert.Equal(-2, histogram.MapToIndex(IEEE754Double.FromString("0 01111111100 0000000000000000000000000000000000000000000000000000"))); // 1/8
        Assert.Equal(-2, histogram.MapToIndex(IEEE754Double.FromString("0 01111111101 0000000000000000000000000000000000000000000000000000"))); // 1/4
        Assert.Equal(-1, histogram.MapToIndex(IEEE754Double.FromString("0 01111111110 0000000000000000000000000000000000000000000000000000"))); // 1/2
        Assert.Equal(-1, histogram.MapToIndex(IEEE754Double.FromString("0 01111111110 0000000000000000000000000000000000000000000000000001"))); // ~0.5000000000000001
        Assert.Equal(-1, histogram.MapToIndex(IEEE754Double.FromString("0 01111111111 0000000000000000000000000000000000000000000000000000"))); // 1
        Assert.Equal(0, histogram.MapToIndex(IEEE754Double.FromString("0 01111111111 0000000000000000000000000000000000000000000000000001"))); // ~1.0000000000000002
        Assert.Equal(0, histogram.MapToIndex(IEEE754Double.FromString("0 10000000000 0000000000000000000000000000000000000000000000000000"))); // 2
        Assert.Equal(0, histogram.MapToIndex(IEEE754Double.FromString("0 10000000001 0000000000000000000000000000000000000000000000000000"))); // 4
        Assert.Equal(1, histogram.MapToIndex(IEEE754Double.FromString("0 10000000010 0000000000000000000000000000000000000000000000000000"))); // 8
        Assert.Equal(1, histogram.MapToIndex(IEEE754Double.FromString("0 10000000011 0000000000000000000000000000000000000000000000000000"))); // 16
        Assert.Equal(2, histogram.MapToIndex(IEEE754Double.FromString("0 10000000100 0000000000000000000000000000000000000000000000000000"))); // 32
        Assert.Equal(2, histogram.MapToIndex(IEEE754Double.FromString("0 10000000101 0000000000000000000000000000000000000000000000000000"))); // 64
        Assert.Equal(3, histogram.MapToIndex(IEEE754Double.FromString("0 10000000110 0000000000000000000000000000000000000000000000000000"))); // 128
        Assert.Equal(511, histogram.MapToIndex(IEEE754Double.FromString("0 11111111110 0000000000000000000000000000000000000000000000000000"))); // ~8.98846567431158E+307 (2 ^ 1023)
        Assert.Equal(511, histogram.MapToIndex(IEEE754Double.FromString("0 11111111110 0000000000000000000000000000000000000000000000000001"))); // ~8.988465674311582E+307 (2 ^ 1023 + 1)
        Assert.Equal(511, histogram.MapToIndex(IEEE754Double.FromString("0 11111111110 1111111111111111111111111111111111111111111111111110"))); // ~1.7976931348623155E+308 (2 ^ 1024 - 2)
        Assert.Equal(511, histogram.MapToIndex(IEEE754Double.FromString("0 11111111110 1111111111111111111111111111111111111111111111111111"))); // ~1.7976931348623157E+308 (maximum normal positive, double.MaxValue, 2 ^ 1024 - 1)
    }

    [Fact]
    public void IndexLookupScaleMinusTwo()
    {
        /*
        An exponential bucket histogram with scale = -2.
        The base is 2 ^ (2 ^ 2) = 16.
        The buckets are:
            ...
            bucket[-3]: (1/4096, 1/256]
            bucket[-2]: (1/256, 1/16]
            bucket[-1]: (1/16, 1]
            bucket[0]:  (1, 16]
            bucket[1]:  (16, 256]
            bucket[2]:  (256, 4096]
            bucket[3]:  (4096, 65536]
            ...
        */
        var histogram = new Base2ExponentialBucketHistogram(maxBuckets: 2, scale: -2);

        Assert.Equal(-269, histogram.MapToIndex(IEEE754Double.FromString("0 00000000000 0000000000000000000000000000000000000000000000000001"))); // ~4.9406564584124654E-324 (minimum subnormal positive, double.Epsilon, 2 ^ -1074)
        Assert.Equal(-269, histogram.MapToIndex(IEEE754Double.FromString("0 00000000000 0000000000000000000000000000000000000000000000000010"))); // double.Epsilon * 2
        Assert.Equal(-269, histogram.MapToIndex(IEEE754Double.FromString("0 00000000000 0000000000000000000000000000000000000000000000000011"))); // double.Epsilon * 3
        Assert.Equal(-269, histogram.MapToIndex(IEEE754Double.FromString("0 00000000000 0000000000000000000000000000000000000000000000000100"))); // double.Epsilon * 4
        Assert.Equal(-268, histogram.MapToIndex(IEEE754Double.FromString("0 00000000000 0000000000000000000000000000000000000000000000000101"))); // double.Epsilon * 5
        Assert.Equal(-268, histogram.MapToIndex(IEEE754Double.FromString("0 00000000000 0000000000000000000000000000000000000000000000000110"))); // double.Epsilon * 6
        Assert.Equal(-268, histogram.MapToIndex(IEEE754Double.FromString("0 00000000000 0000000000000000000000000000000000000000000000000111"))); // double.Epsilon * 7
        Assert.Equal(-268, histogram.MapToIndex(IEEE754Double.FromString("0 00000000000 0000000000000000000000000000000000000000000000001000"))); // double.Epsilon * 8
        Assert.Equal(-257, histogram.MapToIndex(IEEE754Double.FromString("0 00000000000 0100000000000000000000000000000000000000000000000000"))); // ~5.562684646268003E-309 (2 ^ -1024)
        Assert.Equal(-256, histogram.MapToIndex(IEEE754Double.FromString("0 00000000000 0100000000000000000000000000000000000000000000000001"))); // ~5.56268464626801E-309
        Assert.Equal(-256, histogram.MapToIndex(IEEE754Double.FromString("0 00000000000 1000000000000000000000000000000000000000000000000000"))); // ~1.1125369292536007E-308 (2 ^ -1023)
        Assert.Equal(-256, histogram.MapToIndex(IEEE754Double.FromString("0 00000000000 1000000000000000000000000000000000000000000000000001"))); // ~1.112536929253601E-308
        Assert.Equal(-256, histogram.MapToIndex(IEEE754Double.FromString("0 00000000000 1111111111111111111111111111111111111111111111111111"))); // ~2.2250738585072009E-308 (maximum subnormal positive)
        Assert.Equal(-256, histogram.MapToIndex(IEEE754Double.FromString("0 00000000001 0000000000000000000000000000000000000000000000000000"))); // ~2.2250738585072014E-308 (minimum normal positive, 2 ^ -1022)
        Assert.Equal(-256, histogram.MapToIndex(IEEE754Double.FromString("0 00000000001 0000000000000000000000000000000000000000000000000001"))); // ~2.225073858507202E-308
        Assert.Equal(-2, histogram.MapToIndex(IEEE754Double.FromString("0 01111111000 0000000000000000000000000000000000000000000000000000"))); // 1/128
        Assert.Equal(-2, histogram.MapToIndex(IEEE754Double.FromString("0 01111111001 0000000000000000000000000000000000000000000000000000"))); // 1/64
        Assert.Equal(-2, histogram.MapToIndex(IEEE754Double.FromString("0 01111111010 0000000000000000000000000000000000000000000000000000"))); // 1/32
        Assert.Equal(-2, histogram.MapToIndex(IEEE754Double.FromString("0 01111111011 0000000000000000000000000000000000000000000000000000"))); // 1/16
        Assert.Equal(-1, histogram.MapToIndex(IEEE754Double.FromString("0 01111111100 0000000000000000000000000000000000000000000000000000"))); // 1/8
        Assert.Equal(-1, histogram.MapToIndex(IEEE754Double.FromString("0 01111111101 0000000000000000000000000000000000000000000000000000"))); // 1/4
        Assert.Equal(-1, histogram.MapToIndex(IEEE754Double.FromString("0 01111111110 0000000000000000000000000000000000000000000000000000"))); // 1/2
        Assert.Equal(-1, histogram.MapToIndex(IEEE754Double.FromString("0 01111111110 0000000000000000000000000000000000000000000000000001"))); // ~0.5000000000000001
        Assert.Equal(-1, histogram.MapToIndex(IEEE754Double.FromString("0 01111111111 0000000000000000000000000000000000000000000000000000"))); // 1
        Assert.Equal(0, histogram.MapToIndex(IEEE754Double.FromString("0 01111111111 0000000000000000000000000000000000000000000000000001"))); // ~1.0000000000000002
        Assert.Equal(0, histogram.MapToIndex(IEEE754Double.FromString("0 10000000000 0000000000000000000000000000000000000000000000000000"))); // 2
        Assert.Equal(0, histogram.MapToIndex(IEEE754Double.FromString("0 10000000001 0000000000000000000000000000000000000000000000000000"))); // 4
        Assert.Equal(0, histogram.MapToIndex(IEEE754Double.FromString("0 10000000010 0000000000000000000000000000000000000000000000000000"))); // 8
        Assert.Equal(0, histogram.MapToIndex(IEEE754Double.FromString("0 10000000011 0000000000000000000000000000000000000000000000000000"))); // 16
        Assert.Equal(1, histogram.MapToIndex(IEEE754Double.FromString("0 10000000100 0000000000000000000000000000000000000000000000000000"))); // 32
        Assert.Equal(1, histogram.MapToIndex(IEEE754Double.FromString("0 10000000101 0000000000000000000000000000000000000000000000000000"))); // 64
        Assert.Equal(1, histogram.MapToIndex(IEEE754Double.FromString("0 10000000110 0000000000000000000000000000000000000000000000000000"))); // 128
        Assert.Equal(255, histogram.MapToIndex(IEEE754Double.FromString("0 11111111110 0000000000000000000000000000000000000000000000000000"))); // ~8.98846567431158E+307 (2 ^ 1023)
        Assert.Equal(255, histogram.MapToIndex(IEEE754Double.FromString("0 11111111110 0000000000000000000000000000000000000000000000000001"))); // ~8.988465674311582E+307 (2 ^ 1023 + 1)
        Assert.Equal(255, histogram.MapToIndex(IEEE754Double.FromString("0 11111111110 1111111111111111111111111111111111111111111111111110"))); // ~1.7976931348623155E+308 (2 ^ 1024 - 2)
        Assert.Equal(255, histogram.MapToIndex(IEEE754Double.FromString("0 11111111110 1111111111111111111111111111111111111111111111111111"))); // ~1.7976931348623157E+308 (maximum normal positive, double.MaxValue, 2 ^ 1024 - 1)
    }

    [Fact]
    public void IndexLookupScaleMinusTen()
    {
        /*
        An exponential bucket histogram with scale = -10.
        The base is 2 ^ (2 ^ 10) = 2 ^ 1024 = double.MaxValue + 2 ^ -52 (slightly bigger than double.MaxValue).
        The buckets are:
            bucket[-2]: [double.Epsilon, 2 ^ -1024]
            bucket[-1]: (2 ^ -1024, 1]
            bucket[0]:  (1, double.MaxValue]
        */
        var histogram = new Base2ExponentialBucketHistogram(maxBuckets: 2, scale: -10);

        Assert.Equal(-2, histogram.MapToIndex(IEEE754Double.FromString("0 00000000000 0000000000000000000000000000000000000000000000000001"))); // ~4.9406564584124654E-324 (minimum subnormal positive, double.Epsilon, 2 ^ -1074)
        Assert.Equal(-2, histogram.MapToIndex(IEEE754Double.FromString("0 00000000000 0100000000000000000000000000000000000000000000000000"))); // ~5.562684646268003E-309 (2 ^ -1024)
        Assert.Equal(-1, histogram.MapToIndex(IEEE754Double.FromString("0 00000000000 0100000000000000000000000000000000000000000000000001"))); // ~5.56268464626801E-309
        Assert.Equal(-1, histogram.MapToIndex(IEEE754Double.FromString("0 00000000000 1000000000000000000000000000000000000000000000000001"))); // ~1.1125369292536007E-308 (2 ^ -1023)
        Assert.Equal(-1, histogram.MapToIndex(IEEE754Double.FromString("0 00000000000 1111111111111111111111111111111111111111111111111111"))); // ~2.2250738585072009E-308 (maximum subnormal positive)
        Assert.Equal(-1, histogram.MapToIndex(IEEE754Double.FromString("0 00000000001 0000000000000000000000000000000000000000000000000000"))); // ~2.2250738585072014E-308 (minimum normal positive, 2 ^ -1022)
        Assert.Equal(-1, histogram.MapToIndex(IEEE754Double.FromString("0 01111111111 0000000000000000000000000000000000000000000000000000"))); // 1
        Assert.Equal(0, histogram.MapToIndex(IEEE754Double.FromString("0 01111111111 0000000000000000000000000000000000000000000000000001"))); // ~1.0000000000000002
        Assert.Equal(0, histogram.MapToIndex(IEEE754Double.FromString("0 11111111110 1111111111111111111111111111111111111111111111111111"))); // ~1.7976931348623157E+308 (maximum normal positive, double.MaxValue, 2 ^ 1024 - 1)
    }

    [Fact]
    public void IndexLookupScaleMinusEleven()
    {
        /*
        An exponential bucket histogram with scale = -11.
        The base is 2 ^ (2 ^ 11) = 2 ^ 2048 (much bigger than double.MaxValue).
        The buckets are:
            bucket[-1]: [double.Epsilon, 1]
            bucket[0]:  (1, double.MaxValue]
        */
        var histogram = new Base2ExponentialBucketHistogram(maxBuckets: 2, scale: -11);

        Assert.Equal(-1, histogram.MapToIndex(IEEE754Double.FromString("0 00000000000 0000000000000000000000000000000000000000000000000001"))); // ~4.9406564584124654E-324 (minimum subnormal positive, double.Epsilon, 2 ^ -1074)
        Assert.Equal(-1, histogram.MapToIndex(IEEE754Double.FromString("0 00000000000 1111111111111111111111111111111111111111111111111111"))); // ~2.2250738585072009E-308 (maximum subnormal positive)
        Assert.Equal(-1, histogram.MapToIndex(IEEE754Double.FromString("0 00000000001 0000000000000000000000000000000000000000000000000000"))); // ~2.2250738585072014E-308 (minimum normal positive, 2 ^ -1022)
        Assert.Equal(-1, histogram.MapToIndex(IEEE754Double.FromString("0 01111111111 0000000000000000000000000000000000000000000000000000"))); // 1
        Assert.Equal(0, histogram.MapToIndex(IEEE754Double.FromString("0 01111111111 0000000000000000000000000000000000000000000000000001"))); // ~1.0000000000000002
        Assert.Equal(0, histogram.MapToIndex(IEEE754Double.FromString("0 11111111110 1111111111111111111111111111111111111111111111111111"))); // ~1.7976931348623157E+308 (maximum normal positive, double.MaxValue, 2 ^ 1024 - 1)
    }

    [Fact]
    public void IndexLookupScaleOne()
    {
        /*
        An exponential bucket histogram with scale = 1.
                                     ___
        The base is 2 ^ (2 ^ -1) = \/ 2  = 1.4142135623730951.
        The buckets are:
            ...
            bucket[-3]: (0.3535533905932738, 1/2]
            bucket[-2]: (1/2, 0.7071067811865476]
            bucket[-1]: (0.7071067811865476, 1]
            bucket[0]:  (1, 1.4142135623730951]
            bucket[1]:  (1.4142135623730951, 2]
            bucket[2]:  (2, 2.8284271247461901]
            bucket[3]:  (2.8284271247461901, 4]
            ...
        */
        var histogram = new Base2ExponentialBucketHistogram(maxBuckets: 2, scale: 1);

        Assert.Equal(-2149, histogram.MapToIndex(IEEE754Double.FromString("0 00000000000 0000000000000000000000000000000000000000000000000001"))); // ~4.9406564584124654E-324 (minimum subnormal positive, double.Epsilon, 2 ^ -1074)
        Assert.Equal(-2147, histogram.MapToIndex(IEEE754Double.FromString("0 00000000000 0000000000000000000000000000000000000000000000000010"))); // double.Epsilon * 2
        Assert.Equal(-2145, histogram.MapToIndex(IEEE754Double.FromString("0 00000000000 0000000000000000000000000000000000000000000000000011"))); // double.Epsilon * 3
        Assert.Equal(-2145, histogram.MapToIndex(IEEE754Double.FromString("0 00000000000 0000000000000000000000000000000000000000000000000100"))); // double.Epsilon * 4

        // Assert.Equal(-2143, histogram.MapToIndex(IEEE754Double.FromString("0 00000000000 0000000000000000000000000000000000000000000000000101"))); // double.Epsilon * 5
        Assert.Equal(-2143, histogram.MapToIndex(IEEE754Double.FromString("0 00000000000 0000000000000000000000000000000000000000000000000110"))); // double.Epsilon * 6
        Assert.Equal(-2143, histogram.MapToIndex(IEEE754Double.FromString("0 00000000000 0000000000000000000000000000000000000000000000000111"))); // double.Epsilon * 7
        Assert.Equal(-2143, histogram.MapToIndex(IEEE754Double.FromString("0 00000000000 0000000000000000000000000000000000000000000000001000"))); // double.Epsilon * 8
        Assert.Equal(-2049, histogram.MapToIndex(IEEE754Double.FromString("0 00000000000 0100000000000000000000000000000000000000000000000000"))); // ~5.562684646268003E-309 (2 ^ -1024)

        // Assert.Equal(-2048, histogram.MapToIndex(IEEE754Double.FromString("0 00000000000 0100000000000000000000000000000000000000000000000001"))); // ~5.56268464626801E-309
        Assert.Equal(-2047, histogram.MapToIndex(IEEE754Double.FromString("0 00000000000 1000000000000000000000000000000000000000000000000000"))); // ~1.1125369292536007E-308 (2 ^ -1023)

        // Assert.Equal(-2046, histogram.MapToIndex(IEEE754Double.FromString("0 00000000000 1000000000000000000000000000000000000000000000000001"))); // ~1.112536929253601E-308
        Assert.Equal(-2045, histogram.MapToIndex(IEEE754Double.FromString("0 00000000000 1111111111111111111111111111111111111111111111111111"))); // ~2.2250738585072009E-308 (maximum subnormal positive)
        Assert.Equal(-2045, histogram.MapToIndex(IEEE754Double.FromString("0 00000000001 0000000000000000000000000000000000000000000000000000"))); // ~2.2250738585072014E-308 (minimum normal positive, 2 ^ -1022)

        // Assert.Equal(-2044, histogram.MapToIndex(IEEE754Double.FromString("0 00000000001 0000000000000000000000000000000000000000000000000001"))); // ~2.225073858507202E-308
        Assert.Equal(-15, histogram.MapToIndex(IEEE754Double.FromString("0 01111111000 0000000000000000000000000000000000000000000000000000"))); // 1/128
        Assert.Equal(-13, histogram.MapToIndex(IEEE754Double.FromString("0 01111111001 0000000000000000000000000000000000000000000000000000"))); // 1/64
        Assert.Equal(-11, histogram.MapToIndex(IEEE754Double.FromString("0 01111111010 0000000000000000000000000000000000000000000000000000"))); // 1/32
        Assert.Equal(-9, histogram.MapToIndex(IEEE754Double.FromString("0 01111111011 0000000000000000000000000000000000000000000000000000"))); // 1/16
        Assert.Equal(-7, histogram.MapToIndex(IEEE754Double.FromString("0 01111111100 0000000000000000000000000000000000000000000000000000"))); // 1/8
        Assert.Equal(-5, histogram.MapToIndex(IEEE754Double.FromString("0 01111111101 0000000000000000000000000000000000000000000000000000"))); // 1/4
        Assert.Equal(-3, histogram.MapToIndex(IEEE754Double.FromString("0 01111111110 0000000000000000000000000000000000000000000000000000"))); // 1/2
        Assert.Equal(-2, histogram.MapToIndex(IEEE754Double.FromString("0 01111111110 0000000000000000000000000000000000000000000000000001"))); // ~0.5000000000000001
        Assert.Equal(-1, histogram.MapToIndex(IEEE754Double.FromString("0 01111111111 0000000000000000000000000000000000000000000000000000"))); // 1
        Assert.Equal(0, histogram.MapToIndex(IEEE754Double.FromString("0 01111111111 0000000000000000000000000000000000000000000000000001"))); // ~1.0000000000000002
        Assert.Equal(1, histogram.MapToIndex(IEEE754Double.FromString("0 10000000000 0000000000000000000000000000000000000000000000000000"))); // 2
        Assert.Equal(3, histogram.MapToIndex(IEEE754Double.FromString("0 10000000001 0000000000000000000000000000000000000000000000000000"))); // 4
        Assert.Equal(5, histogram.MapToIndex(IEEE754Double.FromString("0 10000000010 0000000000000000000000000000000000000000000000000000"))); // 8
        Assert.Equal(7, histogram.MapToIndex(IEEE754Double.FromString("0 10000000011 0000000000000000000000000000000000000000000000000000"))); // 16
        Assert.Equal(9, histogram.MapToIndex(IEEE754Double.FromString("0 10000000100 0000000000000000000000000000000000000000000000000000"))); // 32
        Assert.Equal(11, histogram.MapToIndex(IEEE754Double.FromString("0 10000000101 0000000000000000000000000000000000000000000000000000"))); // 64
        Assert.Equal(13, histogram.MapToIndex(IEEE754Double.FromString("0 10000000110 0000000000000000000000000000000000000000000000000000"))); // 128
        Assert.Equal(105, histogram.MapToIndex(IEEE754Double.FromString("0 10000110011 1111111111111111111111111111111111111111111111111111"))); // 9,007,199,254,740,991 (Number.MAX_SAFE_INTEGER, 2 ^ 53 - 1)
        Assert.Equal(105, histogram.MapToIndex(IEEE754Double.FromString("0 10000110100 0000000000000000000000000000000000000000000000000000"))); // 9,007,199,254,740,992 (Number.MAX_SAFE_INTEGER + 1, 2 ^ 53)
        Assert.Equal(2045, histogram.MapToIndex(IEEE754Double.FromString("0 11111111110 0000000000000000000000000000000000000000000000000000"))); // ~8.98846567431158E+307 (2 ^ 1023)

        // Assert.Equal(2046, histogram.MapToIndex(IEEE754Double.FromString("0 11111111110 0000000000000000000000000000000000000000000000000001"))); // ~8.988465674311582E+307 (2 ^ 1023 + 1)
        Assert.Equal(2047, histogram.MapToIndex(IEEE754Double.FromString("0 11111111110 1111111111111111111111111111111111111111111111111110"))); // ~1.7976931348623155E+308 (2 ^ 1024 - 2)
        Assert.Equal(2047, histogram.MapToIndex(IEEE754Double.FromString("0 11111111110 1111111111111111111111111111111111111111111111111111"))); // ~1.7976931348623157E+308 (maximum normal positive, double.MaxValue, 2 ^ 1024 - 1)
    }

    [Fact]
    public void IndexLookupScaleTwenty()
    {
        /*
        An exponential bucket histogram with scale = 20.
                                    1048576 ___
        The base is 2 ^ (2 ^ -20) =       \/ 2  = 1.0000006610368821.
        The buckets are:
            ...
            bucket[-3]: (0.9999980168919756, 0.9999986779275468]
            bucket[-2]: (0.9999986779275468, 0.9999993389635549]
            bucket[-1]: (0.9999993389635549, 1]
            bucket[0]:  (1, 1.0000006610368821]
            bucket[1]:  (1.0000006610368821, 1.0000013220742011]
            bucket[2]:  (1.0000013220742011, 1.0000019831119571]
            bucket[3]:  (1.0000019831119571, 1.0000026441501501]
            ...
        */

        var histogram = new Base2ExponentialBucketHistogram(maxBuckets: 2, scale: 20);

        Assert.Equal((-1074 * 1048576) - 1, histogram.MapToIndex(IEEE754Double.FromString("0 00000000000 0000000000000000000000000000000000000000000000000001"))); // ~4.9406564584124654E-324 (minimum subnormal positive, double.Epsilon, 2 ^ -1074)
        Assert.Equal(-1, histogram.MapToIndex(IEEE754Double.FromString("0 01111111111 0000000000000000000000000000000000000000000000000000"))); // 1
        Assert.Equal((1023 * 1048576) - 1, histogram.MapToIndex(IEEE754Double.FromString("0 11111111110 0000000000000000000000000000000000000000000000000000"))); // ~8.98846567431158E+307 (2 ^ 1023)
        Assert.Equal((1024 * 1048576) - 1, histogram.MapToIndex(IEEE754Double.FromString("0 11111111110 1111111111111111111111111111111111111111111111111111"))); // ~1.7976931348623157E+308 (maximum normal positive, double.MaxValue, 2 ^ 1024 - 1)
    }

    [Fact]
    public void InfinityHandling()
    {
        var histogram = new Base2ExponentialBucketHistogram(maxBuckets: 2, scale: 0);

        histogram.Record(double.PositiveInfinity);
        histogram.Record(double.NegativeInfinity);

        Assert.Equal(0, histogram.ZeroCount + histogram.PositiveBuckets.Size + histogram.NegativeBuckets.Size);
    }

    [Fact]
    public void NaNHandling()
    {
        var histogram = new Base2ExponentialBucketHistogram(maxBuckets: 2, scale: 0);

        histogram.Record(double.NaN); // NaN (language/runtime native)
        histogram.Record(IEEE754Double.FromString("0 11111111111 0000000000000000000000000000000000000000000000000001").DoubleValue); // sNaN on x86/64 and ARM
        histogram.Record(IEEE754Double.FromString("0 11111111111 1000000000000000000000000000000000000000000000000001").DoubleValue); // qNaN on x86/64 and ARM
        histogram.Record(IEEE754Double.FromString("0 11111111111 1111111111111111111111111111111111111111111111111111").DoubleValue); // NaN (alternative encoding)

        Assert.Equal(0, histogram.ZeroCount + histogram.PositiveBuckets.Size + histogram.NegativeBuckets.Size);
    }

    [Fact]
    public void ZeroHandling()
    {
        var histogram = new Base2ExponentialBucketHistogram(maxBuckets: 2, scale: 0);

        histogram.Record(IEEE754Double.FromString("0 00000000000 0000000000000000000000000000000000000000000000000000").DoubleValue); // +0
        histogram.Record(IEEE754Double.FromString("1 00000000000 0000000000000000000000000000000000000000000000000000").DoubleValue); // -0

        Assert.Equal(2, histogram.ZeroCount);
    }

    [Fact]
    public void ScaleOneIndexesWithPowerOfTwoLowerBound()
    {
        /*
        The range of indexes tested is fixed to evaluate values recorded
        within [2^-25, 2^25] or approximately [0.00000002980232239, 33554432].

        For perspective, assume the unit of values recorded is seconds then this
        test represents the imprecision of MapToIndex for values recorded between
        approximately 29.80 nanoseconds and 1.06 years.

        The output of this test is as follows:

            Scale: 1
            Indexes per power of 2: 2
            Index range: [-50, 50]
            Value range: [2.9802322387695312E-08, 33554432]
            Successes: 18
            Failures: 33
            Average number of values near a bucket boundary that are off by one: 2.878787878787879
            Average range of values near a bucket boundary that are off by one: 3.0880480139955346E-09

        That is, there are ~2.89 values near each bucket boundary tested that
        are mapped to an index off by one. The range of these incorrectly mapped
        values, assuming seconds as the unit, is ~3.09 nanoseconds.
        */

        // This test only tests scale 1, but it can be adjusted to test any
        // positive scale by changing the scale of the histogram. The output
        // and results are identical for all positive scales.
        var scale = 1;
        var histogram = new Base2ExponentialBucketHistogram(scale: scale);

        // These are used to capture stats for an analysis for where MapToIndex is off by one.
        var successes = 0;
        var failures = 0;
        var diffs = new List<double>();
        var numValuesOffByOne = new List<int>();

        // Only indexes with a lower bound that is an exact power of two are tested.
        var indexesPerPowerOf2 = 1 << scale;
        var exp = -25;

        var index = exp * indexesPerPowerOf2;
        var endIndex = Math.Abs(index);
        var lowerBound = Math.Pow(2, exp);

        this.output.WriteLine(string.Empty);
        this.output.WriteLine($"Scale: {scale}");
        this.output.WriteLine($"Indexes per power of 2: {indexesPerPowerOf2}");
        this.output.WriteLine($"Index range: [{index}, {endIndex}]");
        this.output.WriteLine($"Value range: [{lowerBound}, {Math.Pow(2, Math.Abs(exp))}]");

        for (; index <= endIndex; index += indexesPerPowerOf2, lowerBound = Math.Pow(2, ++exp))
        {
            // Buckets are lower bound exclusive, therefore
            // MapToIndex(LowerBoundOfBucketN) = IndexOfBucketN - 1.
            Assert.Equal(index - 1, histogram.MapToIndex(lowerBound));

            // If MapToIndex was mathematically precise, the following assertion would pass.
            // BitIncrement(lowerBound) increments lowerBound by the smallest increment possible.
            // MapToIndex(BitIncrement(LowerBoundOfBucketN)) should equal IndexOfBucketN.
            // However, because MapToIndex at positive scales is imprecise, the assertion can fail
            // for values very close to a bucket boundary.

            // Assert.Equal(index, histogram.MapToIndex(BitIncrement(lowerBound)));

            // Knowing that MapToIndex is imprecise near bucket boundaries,
            // the following produces an analysis of the magnitude of imprecision.

            var incremented = MathHelper.BitIncrement(lowerBound);

            if (index == histogram.MapToIndex(incremented))
            {
                // This is a scenario where the assertion above would have passed.
                ++successes;
            }
            else
            {
                // This is a scenario where the assertion above would have failed.
                ++failures;

                // Count the number of values near the bucket boundary
                // for which MapToIndex produces a result that is off by one.
                var increments = 1;
                while (index != histogram.MapToIndex(incremented))
                {
                    incremented = MathHelper.BitIncrement(incremented);
                    increments++;
                }

                // Capture stats for this bucket index.
                numValuesOffByOne.Add(increments - 1);
                diffs.Add(incremented - lowerBound);
            }
        }

        this.output.WriteLine($"Successes: {successes}");
        this.output.WriteLine($"Failures: {failures}");
        this.output.WriteLine($"Average number of values near a bucket boundary that are off by one: {numValuesOffByOne.Average()}");
        this.output.WriteLine($"Average range of values near a bucket boundary that are off by one: {diffs.Average()}");
    }
}

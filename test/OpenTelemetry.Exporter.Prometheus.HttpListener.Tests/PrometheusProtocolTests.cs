// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter.Prometheus.Tests;

public class PrometheusProtocolTests
{
    [Fact]
    public void Equals_ReturnsTrueForIdenticalInstances()
    {
        var first = new PrometheusProtocol(
            PrometheusProtocol.PrometheusTextMediaType,
            PrometheusProtocol.UnderscoresEscaping,
            PrometheusProtocol.PrometheusV1,
            false);

        var second = new PrometheusProtocol(
            PrometheusProtocol.PrometheusTextMediaType,
            PrometheusProtocol.UnderscoresEscaping,
            PrometheusProtocol.PrometheusV1,
            false);

        Assert.True(first.Equals(second));
        Assert.True(second.Equals(first));
    }

    [Fact]
    public void Equals_ReturnsTrueForSameInstance()
    {
        var protocol = new PrometheusProtocol(
            PrometheusProtocol.OpenMetricsMediaType,
            null,
            PrometheusProtocol.OpenMetricsV1,
            true);

        Assert.True(protocol.Equals(protocol));
    }

    [Fact]
    public void Equals_ReturnsFalseForDifferentMediaType()
    {
        var first = new PrometheusProtocol(
            PrometheusProtocol.PrometheusTextMediaType,
            null,
            PrometheusProtocol.PrometheusV1,
            false);

        var second = new PrometheusProtocol(
            PrometheusProtocol.OpenMetricsMediaType,
            null,
            PrometheusProtocol.PrometheusV1,
            true);

        Assert.False(first.Equals(second));
        Assert.False(second.Equals(first));
    }

    [Fact]
    public void Equals_ReturnsFalseForDifferentEscaping()
    {
        var first = new PrometheusProtocol(
            PrometheusProtocol.PrometheusTextMediaType,
            null,
            PrometheusProtocol.PrometheusV1,
            false);

        var second = new PrometheusProtocol(
            PrometheusProtocol.PrometheusTextMediaType,
            PrometheusProtocol.UnderscoresEscaping,
            PrometheusProtocol.PrometheusV1,
            false);

        Assert.False(first.Equals(second));
        Assert.False(second.Equals(first));
    }

    [Fact]
    public void Equals_ReturnsFalseForDifferentVersion()
    {
        var first = new PrometheusProtocol(
            PrometheusProtocol.PrometheusTextMediaType,
            null,
            PrometheusProtocol.PrometheusV0,
            false);

        var second = new PrometheusProtocol(
            PrometheusProtocol.PrometheusTextMediaType,
            null,
            PrometheusProtocol.PrometheusV1,
            false);

        Assert.False(first.Equals(second));
        Assert.False(second.Equals(first));
    }

    [Fact]
    public void Equals_ReturnsFalseForDifferentIsOpenMetrics()
    {
        var first = new PrometheusProtocol(
            PrometheusProtocol.PrometheusTextMediaType,
            null,
            PrometheusProtocol.PrometheusV1,
            false);

        var second = new PrometheusProtocol(
            PrometheusProtocol.PrometheusTextMediaType,
            null,
            PrometheusProtocol.PrometheusV1,
            true);

        Assert.False(first.Equals(second));
        Assert.False(second.Equals(first));
    }

    [Fact]
    public void Equals_ReturnsTrueForIdenticalInstancesWithNullEscaping()
    {
        var first = new PrometheusProtocol(
            PrometheusProtocol.OpenMetricsMediaType,
            null,
            PrometheusProtocol.OpenMetricsV1,
            true);

        var second = new PrometheusProtocol(
            PrometheusProtocol.OpenMetricsMediaType,
            null,
            PrometheusProtocol.OpenMetricsV1,
            true);

        Assert.True(first.Equals(second));
        Assert.True(second.Equals(first));
    }

    [Fact]
    public void Equals_Object_ReturnsTrueForIdenticalInstances()
    {
        var first = new PrometheusProtocol(
            PrometheusProtocol.PrometheusTextMediaType,
            PrometheusProtocol.UnderscoresEscaping,
            PrometheusProtocol.PrometheusV1,
            false);

        object second = new PrometheusProtocol(
            PrometheusProtocol.PrometheusTextMediaType,
            PrometheusProtocol.UnderscoresEscaping,
            PrometheusProtocol.PrometheusV1,
            false);

        Assert.True(first.Equals(second));
    }

    [Fact]
    public void Equals_Object_ReturnsFalseForNull()
    {
        var protocol = new PrometheusProtocol(
            PrometheusProtocol.PrometheusTextMediaType,
            null,
            PrometheusProtocol.PrometheusV1,
            false);

        Assert.False(protocol.Equals(null));
    }

    [Fact]
    public void Equals_Object_ReturnsFalseForDifferentType()
    {
        var protocol = new PrometheusProtocol(
            PrometheusProtocol.PrometheusTextMediaType,
            null,
            PrometheusProtocol.PrometheusV1,
            false);

        Assert.False(protocol.Equals("not a PrometheusProtocol"));
        Assert.False(protocol.Equals(42));
    }

    [Fact]
    public void GetHashCode_ReturnsSameValueForEqualInstances()
    {
        var first = new PrometheusProtocol(
            PrometheusProtocol.PrometheusTextMediaType,
            PrometheusProtocol.UnderscoresEscaping,
            PrometheusProtocol.PrometheusV1,
            false);

        var second = new PrometheusProtocol(
            PrometheusProtocol.PrometheusTextMediaType,
            PrometheusProtocol.UnderscoresEscaping,
            PrometheusProtocol.PrometheusV1,
            false);

        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void GetHashCode_ReturnsSameValueForSameInstance()
    {
        var protocol = new PrometheusProtocol(
            PrometheusProtocol.OpenMetricsMediaType,
            null,
            PrometheusProtocol.OpenMetricsV1,
            true);

        var hash1 = protocol.GetHashCode();
        var hash2 = protocol.GetHashCode();

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void GetHashCode_ReturnsSameValueForIdenticalInstancesWithNullEscaping()
    {
        var first = new PrometheusProtocol(
            PrometheusProtocol.OpenMetricsMediaType,
            null,
            PrometheusProtocol.OpenMetricsV1,
            true);

        var second = new PrometheusProtocol(
            PrometheusProtocol.OpenMetricsMediaType,
            null,
            PrometheusProtocol.OpenMetricsV1,
            true);

        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentForDifferentMediaType()
    {
        var first = new PrometheusProtocol(
            PrometheusProtocol.PrometheusTextMediaType,
            null,
            PrometheusProtocol.PrometheusV1,
            false);

        var second = new PrometheusProtocol(
            PrometheusProtocol.OpenMetricsMediaType,
            null,
            PrometheusProtocol.OpenMetricsV1,
            true);

        // Hash codes should be different (but technically could collide)
        // We're just verifying they don't throw and are consistent
        var hash1 = first.GetHashCode();
        var hash2 = second.GetHashCode();

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void GetHashCode_DifferentForDifferentEscaping()
    {
        var first = new PrometheusProtocol(
            PrometheusProtocol.PrometheusTextMediaType,
            null,
            PrometheusProtocol.PrometheusV1,
            false);

        var second = new PrometheusProtocol(
            PrometheusProtocol.PrometheusTextMediaType,
            PrometheusProtocol.UnderscoresEscaping,
            PrometheusProtocol.PrometheusV1,
            false);

        var hash1 = first.GetHashCode();
        var hash2 = second.GetHashCode();

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void GetHashCode_DifferentForDifferentVersion()
    {
        var first = new PrometheusProtocol(
            PrometheusProtocol.PrometheusTextMediaType,
            null,
            PrometheusProtocol.PrometheusV0,
            false);

        var second = new PrometheusProtocol(
            PrometheusProtocol.PrometheusTextMediaType,
            null,
            PrometheusProtocol.PrometheusV1,
            false);

        var hash1 = first.GetHashCode();
        var hash2 = second.GetHashCode();

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void GetHashCode_DifferentForDifferentIsOpenMetrics()
    {
        var first = new PrometheusProtocol(
            PrometheusProtocol.PrometheusTextMediaType,
            null,
            PrometheusProtocol.PrometheusV1,
            false);

        var second = new PrometheusProtocol(
            PrometheusProtocol.PrometheusTextMediaType,
            null,
            PrometheusProtocol.PrometheusV1,
            true);

        var hash1 = first.GetHashCode();
        var hash2 = second.GetHashCode();

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void Equals_WorksWithFallbackConstant()
    {
        var fallback1 = PrometheusProtocol.Fallback;
        var fallback2 = new PrometheusProtocol(
            PrometheusProtocol.PrometheusTextMediaType,
            null,
            PrometheusProtocol.PrometheusV0,
            false);

        Assert.True(fallback1.Equals(fallback2));
        Assert.True(fallback2.Equals(fallback1));
        Assert.Equal(fallback1.GetHashCode(), fallback2.GetHashCode());
    }

    [Theory]
    [InlineData(PrometheusProtocol.PrometheusTextMediaType, null, false)]
    [InlineData(PrometheusProtocol.PrometheusTextMediaType, "underscores", false)]
    [InlineData(PrometheusProtocol.OpenMetricsMediaType, null, true)]
    [InlineData(PrometheusProtocol.OpenMetricsMediaType, "underscores", true)]
    public void Equals_EqualsOperator_ConsistentWithEquals(string mediaType, string? escaping, bool isOpenMetrics)
    {
        var version = isOpenMetrics ? PrometheusProtocol.OpenMetricsV1 : PrometheusProtocol.PrometheusV1;

        var first = new PrometheusProtocol(mediaType, escaping, version, isOpenMetrics);
        var second = new PrometheusProtocol(mediaType, escaping, version, isOpenMetrics);

        // For structs, == operator needs to be implemented separately, but Equals should work
        Assert.True(first.Equals(second));
    }

    [Fact]
    public void CanBeUsedAsDictionaryKey()
    {
        var dictionary = new Dictionary<PrometheusProtocol, string>();

        var first = new PrometheusProtocol(
            PrometheusProtocol.PrometheusTextMediaType,
            null,
            PrometheusProtocol.PrometheusV1,
            false);

        var second = new PrometheusProtocol(
            PrometheusProtocol.OpenMetricsMediaType,
            PrometheusProtocol.UnderscoresEscaping,
            PrometheusProtocol.OpenMetricsV1,
            true);

        dictionary[first] = "prometheus";
        dictionary[second] = "openmetrics";

        Assert.Equal("prometheus", dictionary[first]);
        Assert.Equal("openmetrics", dictionary[second]);
        Assert.Equal(2, dictionary.Count);
    }

    [Fact]
    public void CanBeUsedInHashSet()
    {
        var hashSet = new HashSet<PrometheusProtocol>();

        var first = new PrometheusProtocol(
            PrometheusProtocol.PrometheusTextMediaType,
            null,
            PrometheusProtocol.PrometheusV1,
            false);

        var second = new PrometheusProtocol(
            PrometheusProtocol.PrometheusTextMediaType,
            null,
            PrometheusProtocol.PrometheusV1,
            false);

        var third = new PrometheusProtocol(
            PrometheusProtocol.OpenMetricsMediaType,
            null,
            PrometheusProtocol.OpenMetricsV1,
            true);

        Assert.True(hashSet.Add(first));
        Assert.False(hashSet.Add(second)); // Should be considered duplicate
        Assert.True(hashSet.Add(third));

        Assert.Equal(2, hashSet.Count);
        Assert.Contains(first, hashSet);
        Assert.Contains(second, hashSet); // Should find first
        Assert.Contains(third, hashSet);
    }

    // Tests for short-circuit evaluation in Equals method
    [Fact]
    public void Equals_ShortCircuitsOnIsOpenMetrics()
    {
        // Test that if IsOpenMetrics differs, other properties don't matter
        var first = new PrometheusProtocol(
            PrometheusProtocol.PrometheusTextMediaType,
            PrometheusProtocol.UnderscoresEscaping,
            PrometheusProtocol.PrometheusV1,
            false);

        var second = new PrometheusProtocol(
            PrometheusProtocol.PrometheusTextMediaType,
            PrometheusProtocol.UnderscoresEscaping,
            PrometheusProtocol.PrometheusV1,
            true);

        Assert.False(first.Equals(second));
    }

    [Fact]
    public void Equals_ShortCircuitsOnMediaType()
    {
        // Test that if IsOpenMetrics matches but MediaType differs, other properties don't matter
        var first = new PrometheusProtocol(
            PrometheusProtocol.PrometheusTextMediaType,
            PrometheusProtocol.UnderscoresEscaping,
            PrometheusProtocol.PrometheusV1,
            false);

        var second = new PrometheusProtocol(
            PrometheusProtocol.OpenMetricsMediaType,
            PrometheusProtocol.UnderscoresEscaping,
            PrometheusProtocol.PrometheusV1,
            false);

        Assert.False(first.Equals(second));
    }

    [Fact]
    public void Equals_ShortCircuitsOnEscaping()
    {
        // Test that if IsOpenMetrics and MediaType match but Escaping differs, Version doesn't matter
        var first = new PrometheusProtocol(
            PrometheusProtocol.PrometheusTextMediaType,
            null,
            PrometheusProtocol.PrometheusV1,
            false);

        var second = new PrometheusProtocol(
            PrometheusProtocol.PrometheusTextMediaType,
            PrometheusProtocol.UnderscoresEscaping,
            PrometheusProtocol.PrometheusV1,
            false);

        Assert.False(first.Equals(second));
    }

    [Fact]
    public void Equals_ChecksAllPropertiesWhenPreviousMatch()
    {
        // Ensure all properties are checked (Version is last)
        var first = new PrometheusProtocol(
            PrometheusProtocol.PrometheusTextMediaType,
            PrometheusProtocol.UnderscoresEscaping,
            PrometheusProtocol.PrometheusV0,
            false);

        var second = new PrometheusProtocol(
            PrometheusProtocol.PrometheusTextMediaType,
            PrometheusProtocol.UnderscoresEscaping,
            PrometheusProtocol.PrometheusV1,
            false);

        Assert.False(first.Equals(second));
    }

    [Fact]
    public void Equals_ReturnsFalseForMultipleDifferentProperties()
    {
        // Test when multiple properties differ
        var first = new PrometheusProtocol(
            PrometheusProtocol.PrometheusTextMediaType,
            null,
            PrometheusProtocol.PrometheusV0,
            false);

        var second = new PrometheusProtocol(
            PrometheusProtocol.OpenMetricsMediaType,
            PrometheusProtocol.UnderscoresEscaping,
            PrometheusProtocol.OpenMetricsV1,
            true);

        Assert.False(first.Equals(second));
        Assert.False(second.Equals(first));
    }

    [Fact]
    public void Equals_HandlesEscapingNullVsEmptyString()
    {
        // Test null vs empty string for Escaping property
        var first = new PrometheusProtocol(
            PrometheusProtocol.PrometheusTextMediaType,
            null,
            PrometheusProtocol.PrometheusV1,
            false);

        var second = new PrometheusProtocol(
            PrometheusProtocol.PrometheusTextMediaType,
            string.Empty,
            PrometheusProtocol.PrometheusV1,
            false);

        // null and empty string should be treated as different
        Assert.False(first.Equals(second));
    }

    [Fact]
    public void Equals_HandlesBothEscapingNonNull()
    {
        // Test both with non-null escaping but different values
        var first = new PrometheusProtocol(
            PrometheusProtocol.PrometheusTextMediaType,
            PrometheusProtocol.UnderscoresEscaping,
            PrometheusProtocol.PrometheusV1,
            false);

        var second = new PrometheusProtocol(
            PrometheusProtocol.PrometheusTextMediaType,
            PrometheusProtocol.AllowUtf8Escaping,
            PrometheusProtocol.PrometheusV1,
            false);

        Assert.False(first.Equals(second));
    }

    [Fact]
    public void GetHashCode_IsConsistentAcrossMultipleCalls()
    {
        var protocol = new PrometheusProtocol(
            PrometheusProtocol.PrometheusTextMediaType,
            PrometheusProtocol.UnderscoresEscaping,
            PrometheusProtocol.PrometheusV1,
            false);

        var hash1 = protocol.GetHashCode();
        var hash2 = protocol.GetHashCode();
        var hash3 = protocol.GetHashCode();

        Assert.Equal(hash1, hash2);
        Assert.Equal(hash2, hash3);
    }

    [Fact]
    public void GetHashCode_HandlesNullEscaping()
    {
        // Ensure GetHashCode handles null escaping without throwing
        var protocol = new PrometheusProtocol(
            PrometheusProtocol.PrometheusTextMediaType,
            null,
            PrometheusProtocol.PrometheusV1,
            false);

        var hash = protocol.GetHashCode();

        // Should not throw and should be consistent
        Assert.Equal(hash, protocol.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentForNullVsEmptyStringEscaping()
    {
        var first = new PrometheusProtocol(
            PrometheusProtocol.PrometheusTextMediaType,
            null,
            PrometheusProtocol.PrometheusV1,
            false);

        var second = new PrometheusProtocol(
            PrometheusProtocol.PrometheusTextMediaType,
            string.Empty,
            PrometheusProtocol.PrometheusV1,
            false);

        var hash1 = first.GetHashCode();
        var hash2 = second.GetHashCode();

        // Hash codes should differ for null vs empty string
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void Equals_Symmetry()
    {
        // Test symmetry: if x.Equals(y), then y.Equals(x)
        var first = new PrometheusProtocol(
            PrometheusProtocol.OpenMetricsMediaType,
            PrometheusProtocol.UnderscoresEscaping,
            PrometheusProtocol.OpenMetricsV1,
            true);

        var second = new PrometheusProtocol(
            PrometheusProtocol.OpenMetricsMediaType,
            PrometheusProtocol.UnderscoresEscaping,
            PrometheusProtocol.OpenMetricsV1,
            true);

        Assert.Equal(first.Equals(second), second.Equals(first));
    }

    [Fact]
    public void Equals_Transitivity()
    {
        // Test transitivity: if x.Equals(y) and y.Equals(z), then x.Equals(z)
        var first = new PrometheusProtocol(
            PrometheusProtocol.PrometheusTextMediaType,
            null,
            PrometheusProtocol.PrometheusV0,
            false);

        var second = new PrometheusProtocol(
            PrometheusProtocol.PrometheusTextMediaType,
            null,
            PrometheusProtocol.PrometheusV0,
            false);

        var protocol3 = new PrometheusProtocol(
            PrometheusProtocol.PrometheusTextMediaType,
            null,
            PrometheusProtocol.PrometheusV0,
            false);

        Assert.True(first.Equals(second));
        Assert.True(second.Equals(protocol3));
        Assert.True(first.Equals(protocol3));
    }

    [Fact]
    public void GetHashCode_DifferentVersionsProduceDifferentHashes()
    {
        // Test all version combinations
        var prometheusV0 = new PrometheusProtocol(
            PrometheusProtocol.PrometheusTextMediaType,
            null,
            PrometheusProtocol.PrometheusV0,
            false);

        var prometheusV1 = new PrometheusProtocol(
            PrometheusProtocol.PrometheusTextMediaType,
            null,
            PrometheusProtocol.PrometheusV1,
            false);

        var openMetricsV0 = new PrometheusProtocol(
            PrometheusProtocol.OpenMetricsMediaType,
            null,
            PrometheusProtocol.OpenMetricsV0,
            true);

        var openMetricsV1 = new PrometheusProtocol(
            PrometheusProtocol.OpenMetricsMediaType,
            null,
            PrometheusProtocol.OpenMetricsV1,
            true);

        var hashes = new[]
        {
            prometheusV0.GetHashCode(),
            prometheusV1.GetHashCode(),
            openMetricsV0.GetHashCode(),
            openMetricsV1.GetHashCode(),
        };

        // All hash codes should be different
        Assert.Equal(4, hashes.Distinct().Count());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Equals_HandlesIsOpenMetricsCorrectly(bool isOpenMetrics)
    {
        var mediaType = isOpenMetrics ? PrometheusProtocol.OpenMetricsMediaType : PrometheusProtocol.PrometheusTextMediaType;
        var version = isOpenMetrics ? PrometheusProtocol.OpenMetricsV1 : PrometheusProtocol.PrometheusV1;

        var first = new PrometheusProtocol(mediaType, null, version, isOpenMetrics);
        var second = new PrometheusProtocol(mediaType, null, version, isOpenMetrics);

        Assert.True(first.Equals(second));
    }

    [Fact]
    public void Equals_Object_BoxingScenario()
    {
        // Test boxing scenario where struct is cast to object
        var first = new PrometheusProtocol(
            PrometheusProtocol.PrometheusTextMediaType,
            PrometheusProtocol.UnderscoresEscaping,
            PrometheusProtocol.PrometheusV1,
            false);

        object boxed1 = first;
        object boxed2 = new PrometheusProtocol(
            PrometheusProtocol.PrometheusTextMediaType,
            PrometheusProtocol.UnderscoresEscaping,
            PrometheusProtocol.PrometheusV1,
            false);

        Assert.True(boxed1.Equals(boxed2));
        Assert.True(boxed2.Equals(boxed1));
    }
}

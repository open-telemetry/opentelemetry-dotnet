// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using OpenTelemetry.PersistentStorage.FileSystem;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests.PersistentStorage;

public class PersistentStorageHelperTests
{
    [Theory]
    [InlineData("2024-01-15T143025.1234567Z-abc123.blob", "2024-01-15T14:30:25.1234567Z")]
    [InlineData("2023-12-31T235959.9999999Z-def456.blob", "2023-12-31T23:59:59.9999999Z")]
    [InlineData("2024-06-30T000000.0000000Z-xyz789.blob", "2024-06-30T00:00:00.0000000Z")]
    [InlineData("2024-02-29T120000.5000000Z-leap123.blob", "2024-02-29T12:00:00.5000000Z")]
    public void GetDateTimeFromBlobName_ValidFormat_ReturnsCorrectDateTime(string filePath, string expectedDateTimeString)
    {
        var expectedDateTime = DateTime.ParseExact(
            expectedDateTimeString,
            "yyyy-MM-ddTHH:mm:ss.fffffffZ",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

        var result = PersistentStorageHelper.GetDateTimeFromBlobName(filePath);

        Assert.Equal(expectedDateTime, result);
        Assert.Equal(DateTimeKind.Utc, result.Kind);
    }

    [Theory]
    [InlineData("2024-06-15T143025.1234567Z-abc123.tmp")]
    [InlineData("/path/to/2024-06-15T143025.1234567Z-abc123.blob")]
    [InlineData("C:\\temp\\2024-06-15T143025.1234567Z-abc123.blob")]
    public void GetDateTimeFromBlobName_WithDifferentPathFormats_ReturnsCorrectDateTime(string filePath)
    {
        var expectedDateTime = DateTime.ParseExact(
            "2024-06-15T14:30:25.1234567Z",
            "yyyy-MM-ddTHH:mm:ss.fffffffZ",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

        var result = PersistentStorageHelper.GetDateTimeFromBlobName(filePath);

        Assert.Equal(expectedDateTime, result);
        Assert.Equal(DateTimeKind.Utc, result.Kind);
    }

    [Theory]
    [InlineData("invalid-format.blob")]
    [InlineData("2024-01-15T14:30:25Z-abc123.blob")]
    [InlineData("abc-def.blob")]
    [InlineData("invalidformat.blob")]
    public void GetDateTimeFromBlobName_InvalidFormat_ReturnsDateTimeMinValue(string filePath)
    {
        var result = PersistentStorageHelper.GetDateTimeFromBlobName(filePath);

        Assert.Equal(DateTime.MinValue, result);
        Assert.Equal(DateTimeKind.Utc, result.Kind);
    }

    [Fact]
    public void GetDateTimeFromBlobName_EnsuresUtcTimeZone()
    {
        var filePath = "2024-01-15T143025.1234567Z-abc123.blob";

        var result = PersistentStorageHelper.GetDateTimeFromBlobName(filePath);

        Assert.Equal(DateTimeKind.Utc, result.Kind);
    }

    [Theory]
    [InlineData("2024-01-15T143025.1234567Z-abc123@2024-01-15T143525.1234567Z.lock", "2024-01-15T14:35:25.1234567Z")]
    [InlineData("2023-12-31T235959.9999999Z-def456@2024-01-01T000000.0000000Z.lock", "2024-01-01T00:00:00.0000000Z")]
    [InlineData("2024-06-30T000000.0000000Z-xyz789@2024-06-30T000500.0000000Z.lock", "2024-06-30T00:05:00.0000000Z")]
    [InlineData("2024-02-29T120000.5000000Z-leap123@2024-02-29T121000.5000000Z.lock", "2024-02-29T12:10:00.5000000Z")]
    public void GetDateTimeFromLeaseName_ValidFormat_ReturnsCorrectDateTime(string filePath, string expectedDateTimeString)
    {
        var expectedDateTime = DateTime.ParseExact(
            expectedDateTimeString,
            "yyyy-MM-ddTHH:mm:ss.fffffffZ",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

        var result = PersistentStorageHelper.GetDateTimeFromLeaseName(filePath);

        Assert.Equal(expectedDateTime, result);
        Assert.Equal(DateTimeKind.Utc, result.Kind);
    }

    [Theory]
    [InlineData("/path/to/2024-01-15T143025.1234567Z-abc123@2024-01-15T143525.1234567Z.lock")]
    [InlineData("C:\\temp\\2024-01-15T143025.1234567Z-abc123@2024-01-15T143525.1234567Z.lock")]
    public void GetDateTimeFromLeaseName_WithDifferentPathFormats_ReturnsCorrectDateTime(string filePath)
    {
        var expectedDateTime = DateTime.ParseExact(
            "2024-01-15T14:35:25.1234567Z",
            "yyyy-MM-ddTHH:mm:ss.fffffffZ",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

        var result = PersistentStorageHelper.GetDateTimeFromLeaseName(filePath);

        Assert.Equal(expectedDateTime, result);
        Assert.Equal(DateTimeKind.Utc, result.Kind);
    }

    [Theory]
    [InlineData("invalid-format.lock")]
    [InlineData("2024-01-15T14:30:25Z-abc123.lock")]
    [InlineData("abc-def@2024-01-15T14:30:25Z.lock")]
    [InlineData("2024-01-15T143025.1234567Z-abc123.lock")]
    public void GetDateTimeFromLeaseName_InvalidFormat_ReturnsDateTimeMinValue(string filePath)
    {
        var result = PersistentStorageHelper.GetDateTimeFromLeaseName(filePath);

        Assert.Equal(DateTime.MinValue, result);
        Assert.Equal(DateTimeKind.Utc, result.Kind);
    }

    [Fact]
    public void GetDateTimeFromLeaseName_EnsuresUtcTimeZone()
    {
        var filePath = "2024-01-15T143025.1234567Z-abc123@2024-01-15T143525.1234567Z.lock";

        var result = PersistentStorageHelper.GetDateTimeFromLeaseName(filePath);

        Assert.Equal(DateTimeKind.Utc, result.Kind);
    }

    [Theory]
    [InlineData("2024-01-15T143025.1234567Z-abc123@2024-01-15T153525.1234567Z.lock")]
    public void GetDateTimeFromLeaseName_ExtractsLeaseTime_NotBlobTime(string filePath)
    {
        var result = PersistentStorageHelper.GetDateTimeFromLeaseName(filePath);

        var blobTime = DateTime.ParseExact(
            "2024-01-15T14:30:25.1234567Z",
            "yyyy-MM-ddTHH:mm:ss.fffffffZ",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

        var leaseTime = DateTime.ParseExact(
            "2024-01-15T15:35:25.1234567Z",
            "yyyy-MM-ddTHH:mm:ss.fffffffZ",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

        Assert.NotEqual(blobTime, result);
        Assert.Equal(leaseTime, result);
    }

    [Fact]
    public void GetDateTimeFromBlobName_WithMinimumValue_IsUtc()
    {
        var filePath = "0001-01-01T000000.0000000Z-abc123.blob";

        var result = PersistentStorageHelper.GetDateTimeFromBlobName(filePath);

        Assert.Equal(DateTimeKind.Utc, result.Kind);
        Assert.Equal(DateTime.MinValue.ToUniversalTime(), result);
    }

    [Fact]
    public void GetDateTimeFromLeaseName_WithMinimumValue_IsUtc()
    {
        var filePath = "2024-01-15T143025.1234567Z-abc123@0001-01-01T000000.0000000Z.lock";

        var result = PersistentStorageHelper.GetDateTimeFromLeaseName(filePath);

        Assert.Equal(DateTimeKind.Utc, result.Kind);
        Assert.Equal(DateTime.MinValue.ToUniversalTime(), result);
    }

    [Theory]
    [InlineData("2024-07-15T120000.0000000Z-abc123.blob")]
    [InlineData("2024-01-15T000000.0000000Z-abc123.blob")]
    [InlineData("2024-12-31T235959.9999999Z-abc123.blob")]
    public void GetDateTimeFromBlobName_AcrossTimeZones_AlwaysReturnsUtc(string filePath)
    {
        var result = PersistentStorageHelper.GetDateTimeFromBlobName(filePath);

        Assert.Equal(DateTimeKind.Utc, result.Kind);
    }

    [Theory]
    [InlineData("2024-07-15T120000.0000000Z-abc123@2024-07-15T121000.0000000Z.lock")]
    [InlineData("2024-01-15T000000.0000000Z-abc123@2024-01-15T001000.0000000Z.lock")]
    [InlineData("2024-12-31T235959.9999999Z-abc123@2024-12-31T235959.9999999Z.lock")]
    public void GetDateTimeFromLeaseName_AcrossTimeZones_AlwaysReturnsUtc(string filePath)
    {
        var result = PersistentStorageHelper.GetDateTimeFromLeaseName(filePath);

        Assert.Equal(DateTimeKind.Utc, result.Kind);
    }

    [Theory]
    [InlineData("2024-01-15T143025.1234567Z-abc123.blob", "2024-01-16T143025.1234567Z-def456.blob")]
    [InlineData("2024-01-15T143025.1234567Z-abc123.blob", "2024-01-15T153025.1234567Z-abc123.blob")]
    public void GetDateTimeFromBlobName_ConsistentResults_ForMultipleCalls(string filePath1, string filePath2)
    {
        var result1a = PersistentStorageHelper.GetDateTimeFromBlobName(filePath1);
        var result1b = PersistentStorageHelper.GetDateTimeFromBlobName(filePath1);
        var result2 = PersistentStorageHelper.GetDateTimeFromBlobName(filePath2);

        Assert.Equal(result1a, result1b);
        Assert.NotEqual(result1a, result2);
    }

    [Theory]
    [InlineData("2024-01-15T143025.1234567Z-abc123@2024-01-15T143525.1234567Z.lock", "2024-01-16T143025.1234567Z-def456@2024-01-16T143525.1234567Z.lock")]
    [InlineData("2024-01-15T143025.1234567Z-abc123@2024-01-15T143525.1234567Z.lock", "2024-01-15T143025.1234567Z-abc123@2024-01-15T153525.1234567Z.lock")]
    public void GetDateTimeFromLeaseName_ConsistentResults_ForMultipleCalls(string filePath1, string filePath2)
    {
        var result1a = PersistentStorageHelper.GetDateTimeFromLeaseName(filePath1);
        var result1b = PersistentStorageHelper.GetDateTimeFromLeaseName(filePath1);
        var result2 = PersistentStorageHelper.GetDateTimeFromLeaseName(filePath2);

        Assert.Equal(result1a, result1b);
        Assert.NotEqual(result1a, result2);
    }

    [Fact]
    public void RemoveExpiredLease_WithoutLeaseDelimiter_ReturnsFalse()
    {
        var leaseDeadline = DateTime.UtcNow;

        var result = PersistentStorageHelper.RemoveExpiredLease(leaseDeadline, "invalid-format.lock");

        Assert.False(result);
    }
}

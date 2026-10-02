// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Runtime.InteropServices;
using OpenTelemetry.PersistentStorage.FileSystem;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests.PersistentStorage;

public class PersistentStorageHelperTests
{
    internal const long DefaultRetentionMilliseconds = 172_800_000; // 2 days (FileBlobProvider default)
    internal const long DefaultWriteTimeoutMilliseconds = 60_000; // 1 minute (FileBlobProvider default)

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
        Assert.Equal(DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc), result);
    }

    [Fact]
    public void GetDateTimeFromLeaseName_WithMinimumValue_IsUtc()
    {
        var filePath = "2024-01-15T143025.1234567Z-abc123@0001-01-01T000000.0000000Z.lock";

        var result = PersistentStorageHelper.GetDateTimeFromLeaseName(filePath);

        Assert.Equal(DateTimeKind.Utc, result.Kind);
        Assert.Equal(DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc), result);
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

    [Fact]
    public void RemoveExpiredLease_WithoutLeaseDelimiterInDirectoryContainingAtSign_DoesNotMoveFileOutOfStorageDirectory()
    {
        using var root = new TemporaryDirectory();

        var storage = Path.Combine(root.Path, "user@host", "traces");
        Directory.CreateDirectory(storage);

        var leaseFile = Path.Combine(storage, "invalid-format.lock");
        File.WriteAllText(leaseFile, "lease");

        var result = PersistentStorageHelper.RemoveExpiredLease(DateTime.UtcNow, leaseFile);

        Assert.False(result);
        Assert.True(File.Exists(leaseFile), "The lease file should have been left in place.");
        Assert.False(File.Exists(Path.Combine(root.Path, "user")), "The lease file must not be moved outside the storage directory.");
    }

    [Fact]
    public void RemoveExpiredLease_InDirectoryContainingAtSign_RestoresBlobInStorageDirectory()
    {
        using var root = new TemporaryDirectory();

        var storage = Path.Combine(root.Path, "user@host", "traces");
        Directory.CreateDirectory(storage);

        var blobName = "2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdef.blob";
        var leaseFile = Path.Combine(storage, $"{blobName}@2020-01-01T000500.0000000Z.lock");
        File.WriteAllText(leaseFile, "lease");

        var result = PersistentStorageHelper.RemoveExpiredLease(DateTime.UtcNow, leaseFile);

        Assert.True(result);
        Assert.False(File.Exists(leaseFile));
        Assert.True(File.Exists(Path.Combine(storage, blobName)));
    }

    [Theory]
    [InlineData("worldmap.blob")]
    [InlineData("cache-v2.blob")]
    [InlineData("2020-01-01T000000.0000000Z-notaguid.blob")]
    [InlineData("important-notes.tmp")]
    [InlineData("2020-01-01T000000.0000000Z-notaguid.blob.tmp")]
    [InlineData("database.lock")]
    [InlineData("backup@2020-01-01T000000.0000000Z.lock")]
    [InlineData("2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdef.blob@notes.lock")]
    [InlineData("2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdef.blob@.lock")]
    public void RemoveExpiredBlobs_DoesNotRemoveFilesItDidNotCreate(string fileName)
    {
        using var temp = new TemporaryDirectory();

        var foreignFile = CreateFile(temp.Path, fileName);

        PersistentStorageHelper.RemoveExpiredBlobs(temp.Path, DefaultRetentionMilliseconds, DefaultWriteTimeoutMilliseconds);

        Assert.True(File.Exists(foreignFile), $"{fileName} was removed.");
    }

    [Theory]
    [InlineData("foreign\\2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdef.blob")]
    [InlineData("foreign\\2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdef.blob.tmp")]
    [InlineData("foreign\\2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdef.blob@2020-01-01T000000.0000000Z.lock")]
    public void RemoveExpiredBlobs_DoesNotRemoveFilesWithBackslashesInTheirNameItDidNotCreate(string fileName)
    {
        // A backslash is a valid file name character on non-Windows platforms, so must not be treated as a directory separator.
        Assert.SkipWhen(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "Backslash is not a valid file name character on Windows.");

        using var temp = new TemporaryDirectory();

        var foreignFile = CreateFile(temp.Path, fileName);

        PersistentStorageHelper.RemoveExpiredBlobs(temp.Path, DefaultRetentionMilliseconds, DefaultWriteTimeoutMilliseconds);

        Assert.True(File.Exists(foreignFile), $"{fileName} was removed.");
    }

    [Fact]
    public void RemoveExpiredBlobs_RemovesItsOwnExpiredFiles()
    {
        using var temp = new TemporaryDirectory();

        var expired = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var recentBlob = CreateFile(temp.Path, PersistentStorageHelper.GetUniqueFileName(".blob"));
        var expiredBlob = CreateFile(temp.Path, BlobName(expired));
        var timedOutTemporaryFile = CreateFile(temp.Path, BlobName(expired) + ".tmp");

        var leasedBlob = Path.Combine(temp.Path, BlobName(DateTime.UtcNow));
        var expiredLease = CreateFile(temp.Path, Path.GetFileName(leasedBlob) + "@2020-01-01T000000.0000000Z.lock");

        var blobWithMalformedLease = Path.Combine(temp.Path, BlobName(DateTime.UtcNow));
        var malformedLease = CreateFile(temp.Path, Path.GetFileName(blobWithMalformedLease) + "@not-a-timestamp.lock");

        PersistentStorageHelper.RemoveExpiredBlobs(temp.Path, DefaultRetentionMilliseconds, DefaultWriteTimeoutMilliseconds);

        Assert.True(File.Exists(recentBlob));
        Assert.False(File.Exists(expiredBlob));
        Assert.False(File.Exists(timedOutTemporaryFile));

        // Expired leases are released.
        Assert.False(File.Exists(expiredLease));
        Assert.True(File.Exists(leasedBlob));

        // A lease whose timestamp is not in the format the component uses was not created by it, so is left alone.
        Assert.True(File.Exists(malformedLease));
        Assert.False(File.Exists(blobWithMalformedLease));
    }

    internal static string BlobName(DateTime timestamp)
        => FormattableString.Invariant($"{timestamp:yyyy-MM-ddTHHmmss.fffffffZ}-{Guid.NewGuid():N}.blob");

    internal static string CreateFile(string directory, string fileName)
    {
        var path = Path.Combine(directory, fileName);
        File.WriteAllText(path, "data");
        return path;
    }
}

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

    [Theory]
    [InlineData("2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdef.BLOB")]
    [InlineData("2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdef.Blob")]
    [InlineData("2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdef.blob.TMP")]
    [InlineData("2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdef.BLOB.tmp")]
    [InlineData("2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdef.blob@2020-01-01T000000.0000000Z.LOCK")]
    [InlineData("2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdef.BLOB@2020-01-01T000000.0000000Z.lock")]
    [InlineData("2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdef.blob@2020-01-01T000000.0000000Z.lock.TMP")]
    [InlineData("2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdef.blob@2020-01-01T000000.0000000Z.LOCK.tmp")]
    public void RemoveExpiredBlobs_DoesNotRemoveFilesWithDifferentlyCasedExtensionsItDidNotCreate(string fileName)
    {
        // The exporter only creates files with lowercase extensions.
        using var temp = new TemporaryDirectory();

        var foreignFile = CreateFile(temp.Path, fileName);

        PersistentStorageHelper.RemoveExpiredBlobs(temp.Path, DefaultRetentionMilliseconds, DefaultWriteTimeoutMilliseconds);

        Assert.True(File.Exists(foreignFile), $"{fileName} was removed or renamed.");
    }

    [Theory]
    [InlineData("2020-01-01T000000.0000000Z-0123456789ABCDEF0123456789ABCDEF.blob")]
    [InlineData("2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdeF.blob")]
    [InlineData("2020-01-01T000000.0000000Z-0123456789ABCDEF0123456789ABCDEF.blob.tmp")]
    [InlineData("2020-01-01T000000.0000000Z-0123456789ABCDEF0123456789ABCDEF.blob@2020-01-01T000000.0000000Z.lock")]
    [InlineData("2020-01-01T000000.0000000Z-0123456789ABCDEF0123456789ABCDEF.blob@2020-01-01T000000.0000000Z.lock.tmp")]
    [InlineData("2020-01-01T000000.0000000Z-0123456789ABCDEF0123456789ABCDEF.blob@2020-01-01T000000.0000000Z.lock@2020-01-01T000000.0000000Z.lock")]
    public void RemoveExpiredBlobs_DoesNotRemoveFilesWithUppercaseGuidsItDidNotCreate(string fileName)
    {
        // The exporter only creates files with GUIDs formatted as lowercase hexadecimal digits.
        using var temp = new TemporaryDirectory();

        var foreignFile = CreateFile(temp.Path, fileName);

        PersistentStorageHelper.RemoveExpiredBlobs(temp.Path, DefaultRetentionMilliseconds, DefaultWriteTimeoutMilliseconds);

        Assert.True(File.Exists(foreignFile), $"{fileName} was removed or renamed.");
    }

    [Theory]
    [InlineData("2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdef.blob@2020-01-01T000000.0000000Z.lock@notes.lock")]
    [InlineData("2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdef.blob@notes.lock@2020-01-01T000000.0000000Z.lock")]
    [InlineData("2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdef.blob@2020-01-01T000000.0000000Z.LOCK@2020-01-01T000000.0000000Z.lock")]
    [InlineData("2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdef.blob@2020-01-01T000000.0000000Z.lock@.lock")]
    [InlineData("2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdef.blob@@2020-01-01T000000.0000000Z.lock")]
    [InlineData("2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdef.blob@2020-01-01T000000.0000000Z.lock@2020-01-01T000000.0000000Z.lock.TMP")]
    [InlineData("worldmap.blob@2020-01-01T000000.0000000Z.lock@2020-01-01T000000.0000000Z.lock")]
    [InlineData("2020-01-01T000000.0000000Z-notaguid.blob@2020-01-01T000000.0000000Z.lock@2020-01-01T000000.0000000Z.lock.tmp")]
    public void RemoveExpiredBlobs_DoesNotRemoveNestedLeasesItDidNotCreate(string fileName)
    {
        using var temp = new TemporaryDirectory();

        var foreignFile = CreateFile(temp.Path, fileName);

        PersistentStorageHelper.RemoveExpiredBlobs(temp.Path, DefaultRetentionMilliseconds, DefaultWriteTimeoutMilliseconds);

        Assert.True(File.Exists(foreignFile), $"{fileName} was removed or renamed.");
    }

    [Theory]
    [InlineData("2020-01-01T000000.0000000Z- 0123456789abcdef0123456789abcdef.blob")]
    [InlineData("2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdef .blob")]
    [InlineData("2020-01-01T000000.0000000Z- 0123456789abcdef0123456789abcdef .blob")]
    [InlineData(" 2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdef.blob")]
    [InlineData("2020-01-01T000000.0000000Z -0123456789abcdef0123456789abcdef.blob")]
    [InlineData("2020-01-01T000000.0000000Z- 0123456789abcdef0123456789abcdef.blob.tmp")]
    [InlineData("2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdef .blob.tmp")]
    [InlineData("2020-01-01T000000.0000000Z- 0123456789abcdef0123456789abcdef.blob@2020-01-01T000000.0000000Z.lock")]
    [InlineData("2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdef .blob@2020-01-01T000000.0000000Z.lock")]
    [InlineData("2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdef.blob@ 2020-01-01T000000.0000000Z.lock")]
    [InlineData("2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdef.blob@2020-01-01T000000.0000000Z .lock")]
    public void RemoveExpiredBlobs_DoesNotRemoveFilesWithWhitespacePaddedNamesItDidNotCreate(string fileName)
    {
        using var temp = new TemporaryDirectory();

        var foreignFile = CreateFile(temp.Path, fileName);

        PersistentStorageHelper.RemoveExpiredBlobs(temp.Path, DefaultRetentionMilliseconds, DefaultWriteTimeoutMilliseconds);

        Assert.True(File.Exists(foreignFile), $"{fileName} was removed or renamed.");
    }

    [Fact]
    public void RemoveExpiredBlobs_ReleasesExpiredNestedLeases()
    {
        // Previous versions appended a further lease to the name of a leased blob that was written to with a new lease: {blob}@{timestamp}.lock@{timestamp}.lock
        using var temp = new TemporaryDirectory();

        var utcNow = DateTime.UtcNow;
        var blob = BlobName(utcNow);
        var expiredLease = CreateFile(temp.Path, $"{blob}@2020-01-01T000000.0000000Z.lock@2020-01-01T000500.0000000Z.lock");
        var activeLease = CreateFile(temp.Path, $"{blob}@2020-01-01T000000.0000000Z.lock@{FormatTimestamp(utcNow.AddHours(1))}.lock");

        PersistentStorageHelper.RemoveExpiredBlobs(temp.Path, DefaultRetentionMilliseconds, DefaultWriteTimeoutMilliseconds);

        Assert.False(File.Exists(expiredLease), "The expired nested lease was not released.");
        Assert.True(File.Exists(Path.Combine(temp.Path, $"{blob}@2020-01-01T000000.0000000Z.lock")), "The expired nested lease was not released to the lease it was derived from.");
        Assert.True(File.Exists(activeLease), "The active nested lease was released.");
    }

    [Fact]
    public void RemoveExpiredBlobs_RemovesTimedOutTemporaryFilesOfNestedLeases()
    {
        using var temp = new TemporaryDirectory();

        var utcNow = DateTime.UtcNow;
        var expiredLease = CreateFile(temp.Path, BlobName(utcNow) + "@2020-01-01T000000.0000000Z.lock@2020-01-01T000500.0000000Z.lock.tmp");
        var activeLease = CreateFile(temp.Path, BlobName(utcNow) + $"@2020-01-01T000000.0000000Z.lock@{FormatTimestamp(utcNow.AddHours(1))}.lock.tmp");

        PersistentStorageHelper.RemoveExpiredBlobs(temp.Path, DefaultRetentionMilliseconds, DefaultWriteTimeoutMilliseconds);

        Assert.False(File.Exists(expiredLease), "The temporary file of an expired nested lease was not removed.");
        Assert.True(File.Exists(activeLease), "The temporary file of an active nested lease was removed.");
    }

    [Fact]
    public void RemoveExpiredBlobs_RemovesTimedOutTemporaryFilesOfLeasedBlobs()
    {
        // A temporary file of a leased blob written by previous versions is named {blob}@{timestamp}.lock.tmp,
        // and is only abandoned once the lease has expired, regardless of when the blob itself was created.
        using var temp = new TemporaryDirectory();

        var utcNow = DateTime.UtcNow;
        var expiredLease = CreateFile(temp.Path, BlobName(utcNow) + "@2020-01-01T000000.0000000Z.lock.tmp");
        var activeLease = CreateFile(temp.Path, BlobName(new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)) + $"@{FormatTimestamp(utcNow.AddHours(1))}.lock.tmp");

        PersistentStorageHelper.RemoveExpiredBlobs(temp.Path, DefaultRetentionMilliseconds, DefaultWriteTimeoutMilliseconds);

        Assert.False(File.Exists(expiredLease), "The temporary file of an expired lease was not removed.");
        Assert.True(File.Exists(activeLease), "The temporary file of an active lease was removed.");
    }

    [Fact]
    public void RemoveExpiredLease_BeforeTheYear2000_IsNotTreatedAsLegacy()
    {
        // Devices without a real-time clock may start with a clock in the past, such as 1970, until it is synchronized.
        using var temp = new TemporaryDirectory();

        var leasePath = CreateFile(temp.Path, $"{BlobName(new DateTime(1999, 1, 1, 0, 0, 0, DateTimeKind.Utc))}@1999-01-01T000500.0000000Z.lock");

        Assert.False(PersistentStorageHelper.RemoveExpiredLease(new DateTime(1999, 1, 1, 0, 1, 0, DateTimeKind.Utc), leasePath));
        Assert.True(File.Exists(leasePath));
    }

    [Fact]
    public void ClaimFile_MovesFileToTemporaryFileThatIsTimedOut()
    {
        using var temp = new TemporaryDirectory();

        var utcNow = DateTime.UtcNow;
        var leasePath = CreateFile(temp.Path, $"{BlobName(new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc))}@{FormatTimestamp(utcNow.AddMinutes(1))}.lock");

        var claimedPath = PersistentStorageHelper.ClaimFile(leasePath);

        // The claimed file can no longer be leased, released or removed using the name it had before it was claimed.
        Assert.Equal([claimedPath], Directory.GetFiles(temp.Path));
        Assert.EndsWith(".blob.tmp", claimedPath, StringComparison.Ordinal);
        Assert.Equal("data", File.ReadAllText(claimedPath));
        Assert.False(PersistentStorageHelper.RemoveExpiredLease(utcNow.AddHours(1), leasePath));

        // If the claimed file is left behind, it is timed out relative to when it was claimed, not when the blob was created.
        var timeoutDeadline = utcNow - TimeSpan.FromMilliseconds(DefaultWriteTimeoutMilliseconds);
        Assert.False(PersistentStorageHelper.RemoveTimedOutTmpFiles(timeoutDeadline, claimedPath));
        Assert.True(PersistentStorageHelper.RemoveTimedOutTmpFiles(utcNow.AddMinutes(5), claimedPath));
        Assert.Empty(Directory.GetFiles(temp.Path));
    }

    [Fact]
    public void ClaimFile_ThrowsIfFileDoesNotExist()
    {
        using var temp = new TemporaryDirectory();

        var blobPath = Path.Combine(temp.Path, BlobName(DateTime.UtcNow));

        Assert.Throws<FileNotFoundException>(() => PersistentStorageHelper.ClaimFile(blobPath));
        Assert.Empty(Directory.GetFiles(temp.Path));
    }

    [Fact]
    public void RemoveClaimedFile_RemovesFileAndReturnsItsSize()
    {
        using var temp = new TemporaryDirectory();

        var claimedPath = PersistentStorageHelper.ClaimFile(CreateFile(temp.Path, BlobName(DateTime.UtcNow)));

        Assert.Equal(4, PersistentStorageHelper.RemoveClaimedFile(claimedPath));
        Assert.Empty(Directory.GetFiles(temp.Path));
    }

    [Fact]
    public void RemoveClaimedFile_ReturnsZeroIfFileDoesNotExist()
    {
        using var temp = new TemporaryDirectory();

        var claimedPath = PersistentStorageHelper.GetUniqueFilePath(Path.Combine(temp.Path, BlobName(DateTime.UtcNow)), ".blob.tmp");

        Assert.Equal(0, PersistentStorageHelper.RemoveClaimedFile(claimedPath));
    }

    [Theory]
    [InlineData("2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdef.BLOB")]
    [InlineData("2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdef.Blob")]
    public void IsBlobFileName_RequiresLowercaseExtension(string fileName)
        => Assert.False(PersistentStorageHelper.IsBlobFileName(fileName));

    [Theory]
    [InlineData("2020-01-01T000000.0000000Z-0123456789ABCDEF0123456789ABCDEF.blob")]
    [InlineData("2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdeF.blob")]
    [InlineData("2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdeg.blob")]
    [InlineData("2020-01-01T000000.0000000Z-{0123456789abcdef0123456789abcde}.blob")]
    public void IsBlobFileName_RequiresLowercaseHexadecimalGuid(string fileName)
        => Assert.False(PersistentStorageHelper.IsBlobFileName(fileName));

    [Theory]
    [InlineData("2020-01-01T000000.0000000Z- 0123456789abcdef0123456789abcdef.blob")]
    [InlineData("2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdef .blob")]
    [InlineData("2020-01-01T000000.0000000Z-\t0123456789abcdef0123456789abcdef.blob")]
    [InlineData("2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdef\t.blob")]
    [InlineData("2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdef\n.blob")]
    [InlineData("2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcde.blob")]
    [InlineData("2020-01-01T000000.0000000Z-0123456789abcdef0123456789abcdef0.blob")]
    public void IsBlobFileName_RejectsGuidsThatAreNotExactly32HexCharacters(string fileName)
        => Assert.False(PersistentStorageHelper.IsBlobFileName(fileName));

    [Theory]
    [InlineData("2567-02-29T000000.000000Z-0123456789abcdef0123456789abcdef.blob")]
    [InlineData("2567-02-29 000000.0000000Z-0123456789abcdef0123456789abcdef.blob")]
    [InlineData("2567-02-29T00:00:00.0000000Z-0123456789abcdef0123456789abcdef.blob")]
    [InlineData("2567-O2-29T000000.0000000Z-0123456789abcdef0123456789abcdef.blob")]
    [InlineData("\u0662\u0665\u0666\u0667-02-29T000000.0000000Z-0123456789abcdef0123456789abcdef.blob")]
    public void IsBlobFileName_RejectsTimestampsInOtherFormats(string fileName)
        => Assert.False(PersistentStorageHelper.IsBlobFileName(fileName));

    [Fact]
    public void GetUniqueFileName_UsesInvariantCalendar()
    {
        string fileName;
        DateTime before;
        DateTime after;

        using (CultureSwitcher.UseCulture("th-TH"))
        {
            before = DateTime.UtcNow;
            fileName = PersistentStorageHelper.GetUniqueFileName(".blob");
            after = DateTime.UtcNow;
        }

        Assert.True(PersistentStorageHelper.IsBlobFileName(fileName), $"{fileName} is not a valid blob name.");
        Assert.InRange(PersistentStorageHelper.GetDateTimeFromBlobName(fileName), before, after);
    }

    internal static string BlobName(DateTime timestamp)
        => $"{FormatTimestamp(timestamp)}-{Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)}.blob";

    internal static string FormatTimestamp(DateTime timestamp)
        => timestamp.ToString("yyyy-MM-ddTHHmmss.fffffffZ", CultureInfo.InvariantCulture);

    internal static string CreateFile(string directory, string fileName)
    {
        var path = Path.Combine(directory, fileName);
        File.WriteAllText(path, "data");
        return path;
    }
}

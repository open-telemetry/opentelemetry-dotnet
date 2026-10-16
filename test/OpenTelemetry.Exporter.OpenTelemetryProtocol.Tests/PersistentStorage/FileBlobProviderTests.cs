// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.InteropServices;
using OpenTelemetry.PersistentStorage.FileSystem;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests.PersistentStorage;

public class FileBlobProviderTests
{
    [Fact]
    public void TryCreateBlob_WhenWriteExceedsEmptyStorageLimit_RejectsWithoutPersisting()
    {
        using var temp = new TemporaryDirectory();
        using var provider = new FileBlobProvider(temp.Path, maxSizeInBytes: 3);

        Assert.False(provider.TryCreateBlob(new byte[4].AsSpan(), out var blob));
        Assert.Null(blob);
        Assert.Empty(Directory.EnumerateFiles(temp.Path, "*.blob"));
    }

    [Fact]
    public void TryCreateBlob_WhenWriteWouldExceedExistingStorage_RejectsWithoutPersisting()
    {
        using var temp = new TemporaryDirectory();

        File.WriteAllBytes(Path.Combine(temp.Path, "existing.bin"), new byte[3]);
        using var provider = new FileBlobProvider(temp.Path, maxSizeInBytes: 5);

        Assert.False(provider.TryCreateBlob(new byte[3].AsSpan(), out var blob));
        Assert.Null(blob);
        Assert.Empty(Directory.EnumerateFiles(temp.Path, "*.blob"));
    }

    [Fact]
    public void TryCreateBlob_WhenWriteExactlyFillsStorageLimit_Accepts()
    {
        using var temp = new TemporaryDirectory();
        using var provider = new FileBlobProvider(temp.Path, maxSizeInBytes: 4);

        Assert.True(provider.TryCreateBlob(new byte[4].AsSpan(), out var blob));
        Assert.NotNull(blob);
        Assert.Single(Directory.EnumerateFiles(temp.Path, "*.blob"));
        Assert.True(blob.TryDelete());
    }

    [Fact]
    public void TryCreateBlob_IgnoresFilesInSubdirectoriesForStorageLimit()
    {
        using var temp = new TemporaryDirectory();

        var nested = Path.Combine(temp.Path, "nested");
        Directory.CreateDirectory(nested);
        File.WriteAllBytes(Path.Combine(nested, "large.bin"), new byte[1024]);

        using var provider = new FileBlobProvider(temp.Path, maxSizeInBytes: 4);

        Assert.True(provider.TryCreateBlob(new byte[4].AsSpan(), out var blob));
        Assert.NotNull(blob);
        Assert.Single(Directory.EnumerateFiles(temp.Path, "*.blob"));
        Assert.True(blob.TryDelete());
    }

    [Fact]
    public async Task MaintenanceEvent_DoesNotRemoveFilesItDidNotCreate()
    {
        using var temp = new TemporaryDirectory();

        var foreignBlob = PersistentStorageHelperTests.CreateFile(temp.Path, "someones-database.blob");
        var expiredBlob = PersistentStorageHelperTests.CreateFile(
            temp.Path,
            PersistentStorageHelperTests.BlobName(new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)));

        using var provider = new FileBlobProvider(
            temp.Path,
            maintenancePeriodInMilliseconds: 100,
            retentionPeriodInMilliseconds: PersistentStorageHelperTests.DefaultRetentionMilliseconds,
            writeTimeoutInMilliseconds: (int)PersistentStorageHelperTests.DefaultWriteTimeoutMilliseconds);

        // The removal of the provider's own expired blob shows that the maintenance timer has run.
        var timeout = TimeSpan.FromSeconds(30);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        while (File.Exists(expiredBlob) && stopwatch.Elapsed < timeout)
        {
            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        Assert.False(File.Exists(expiredBlob), "The maintenance timer did not run.");
        Assert.True(File.Exists(foreignBlob), "The maintenance timer removed a file it did not create.");
    }

    [Fact]
    public void GetBlobs_DoesNotReturnFilesItDidNotCreate()
    {
        using var temp = new TemporaryDirectory();

        var timestamp = PersistentStorageHelperTests.FormatTimestamp(DateTime.UtcNow);

        PersistentStorageHelperTests.CreateFile(temp.Path, $"{timestamp}-report.blob");
        PersistentStorageHelperTests.CreateFile(temp.Path, $"{timestamp}-0123456789ABCDEF0123456789ABCDEF.blob");
        PersistentStorageHelperTests.CreateFile(temp.Path, $"{timestamp}- 0123456789abcdef0123456789abcdef.blob");
        PersistentStorageHelperTests.CreateFile(temp.Path, $"{timestamp}-00112233445566778899aabbccddeeff.BLOB");

        var ownBlob = PersistentStorageHelperTests.CreateFile(temp.Path, $"{timestamp}-fedcba9876543210fedcba9876543210.blob");

        using var provider = new FileBlobProvider(temp.Path);

        var blob = Assert.Single(provider.GetBlobs());
        Assert.Equal(ownBlob, ((FileBlob)blob).FullPath);
    }

    [Fact]
    public void GetBlobs_DoesNotReturnFilesWithDifferentlyCasedExtensionItDidNotCreate()
    {
        // On Windows, enumerating *.blob also matches files with differently cased extensions
        using var temp = new TemporaryDirectory();

        PersistentStorageHelperTests.CreateFile(temp.Path, $"{PersistentStorageHelperTests.FormatTimestamp(DateTime.UtcNow)}-0123456789abcdef0123456789abcdef.BLOB");

        using var provider = new FileBlobProvider(temp.Path);

        Assert.Empty(provider.GetBlobs());
    }

    [Fact]
    public void TryCreateBlob_WithLease_UsesInvariantCalendar()
    {
        using var temp = new TemporaryDirectory();
        using var provider = new FileBlobProvider(temp.Path);

        using (CultureSwitcher.UseCulture("th-TH"))
        {
            var before = DateTime.UtcNow;
            Assert.True(provider.TryCreateBlob([1, 2, 3], 60_000, out var blob));
            var after = DateTime.UtcNow;

            var leasePath = ((FileBlob)blob).FullPath;
            Assert.InRange(PersistentStorageHelper.GetDateTimeFromLeaseName(leasePath), before.AddMinutes(1), after.AddMinutes(1));

            before = DateTime.UtcNow;
            Assert.True(blob.TryLease(120_000));
            after = DateTime.UtcNow;

            leasePath = ((FileBlob)blob).FullPath;
            Assert.InRange(PersistentStorageHelper.GetDateTimeFromLeaseName(leasePath), before.AddMinutes(2), after.AddMinutes(2));

            // The lease is recognized as one created by the exporter, so is released once expired.
            Assert.True(PersistentStorageHelper.RemoveExpiredLease(DateTime.UtcNow.AddHours(1), leasePath));
        }
    }

    [Fact]
    public void TryWrite_LeasedBlobWithLease_ReplacesLease()
    {
        using var temp = new TemporaryDirectory();
        using var provider = new FileBlobProvider(temp.Path);

        Assert.True(provider.TryCreateBlob([1, 2, 3], 60_000, out var blob));
        var leasePath = ((FileBlob)blob).FullPath;
        var blobPath = leasePath.Substring(0, leasePath.LastIndexOf('@'));

        Assert.True(blob.TryWrite([4, 5, 6], 60_000));
        var newLeasePath = ((FileBlob)blob).FullPath;

        // The blob is leased again from its original name, rather than the previous lease being extended,
        // and the previous lease is replaced rather than left alongside the newly written blob.
        Assert.StartsWith(blobPath + "@", newLeasePath, StringComparison.Ordinal);
        Assert.DoesNotContain(".lock@", newLeasePath, StringComparison.Ordinal);
        Assert.Equal([newLeasePath], Directory.GetFiles(temp.Path));
        Assert.Equal([4, 5, 6], File.ReadAllBytes(newLeasePath));

        // The lease is kept while it is held, and released to the original name once it has expired.
        var utcNow = DateTime.UtcNow;
        Assert.False(PersistentStorageHelper.RemoveExpiredLease(utcNow, newLeasePath));
        Assert.True(PersistentStorageHelper.RemoveExpiredLease(utcNow.AddMinutes(5), newLeasePath));

        Assert.Equal([blobPath], Directory.GetFiles(temp.Path));
        Assert.Equal([4, 5, 6], File.ReadAllBytes(blobPath));
    }

    [Fact]
    public void TryWrite_LeasedBlobWithoutLease_ReleasesLease()
    {
        using var temp = new TemporaryDirectory();
        using var provider = new FileBlobProvider(temp.Path);

        Assert.True(provider.TryCreateBlob([1, 2, 3], 60_000, out var blob));
        var leasePath = ((FileBlob)blob).FullPath;
        var blobPath = leasePath.Substring(0, leasePath.LastIndexOf('@'));

        Assert.True(blob.TryWrite([4, 5, 6]));

        Assert.Equal(blobPath, ((FileBlob)blob).FullPath);
        Assert.Equal([blobPath], Directory.GetFiles(temp.Path));
        Assert.Equal([4, 5, 6], File.ReadAllBytes(blobPath));
    }

    [Fact]
    public void TryWrite_BlobWithoutLease_ReplacesBlob()
    {
        using var temp = new TemporaryDirectory();
        using var provider = new FileBlobProvider(temp.Path);

        Assert.True(provider.TryCreateBlob([1, 2, 3], out var blob));
        var blobPath = ((FileBlob)blob).FullPath;

        Assert.True(blob.TryWrite([4, 5, 6]));

        Assert.Equal(blobPath, ((FileBlob)blob).FullPath);
        Assert.Equal([blobPath], Directory.GetFiles(temp.Path));
        Assert.Equal([4, 5, 6], File.ReadAllBytes(blobPath));
    }

    [Fact]
    public void TryWrite_BlobWithLease_ReplacesBlob()
    {
        using var temp = new TemporaryDirectory();
        using var provider = new FileBlobProvider(temp.Path);

        Assert.True(provider.TryCreateBlob([1, 2, 3], out var blob));
        var blobPath = ((FileBlob)blob).FullPath;

        Assert.True(blob.TryWrite([4, 5, 6], 60_000));
        var leasePath = ((FileBlob)blob).FullPath;

        Assert.StartsWith(blobPath + "@", leasePath, StringComparison.Ordinal);
        Assert.Equal([leasePath], Directory.GetFiles(temp.Path));
        Assert.Equal([4, 5, 6], File.ReadAllBytes(leasePath));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(60_000)]
    public void TryWrite_BlobWhoseLeaseWasReleased_ReplacesBlob(int leasePeriodMilliseconds)
    {
        using var temp = new TemporaryDirectory();
        using var provider = new FileBlobProvider(temp.Path, maxSizeInBytes: 10);

        Assert.True(provider.TryCreateBlob([1, 2, 3, 4], 60_000, out var blob));
        var leasePath = ((FileBlob)blob).FullPath;
        var blobPath = leasePath.Substring(0, leasePath.LastIndexOf('@'));

        // The lease expires and is released by the storage maintenance before the blob is written to again.
        Assert.True(PersistentStorageHelper.RemoveExpiredLease(DateTime.UtcNow.AddMinutes(5), leasePath));
        Assert.Equal([blobPath], Directory.GetFiles(temp.Path));

        Assert.True(blob.TryWrite([5, 6, 7, 8], leasePeriodMilliseconds));

        var path = ((FileBlob)blob).FullPath;

        if (leasePeriodMilliseconds > 0)
        {
            Assert.StartsWith(blobPath + "@", path, StringComparison.Ordinal);
        }
        else
        {
            Assert.Equal(blobPath, path);
        }

        Assert.Equal([path], Directory.GetFiles(temp.Path));
        Assert.Equal([5, 6, 7, 8], File.ReadAllBytes(path));
        Assert.False(File.Exists(leasePath + ".tmp"));

        // Only the 4 bytes of the blob's current content are stored, so there is still space for another blob.
        Assert.True(provider.TryCreateBlob([9, 10, 11, 12], out _));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(60_000)]
    public void TryWrite_BlobWhoseNestedLeaseWasPartiallyReleased_ReplacesBlob(int leasePeriodMilliseconds)
    {
        using var temp = new TemporaryDirectory();

        var blobPath = Path.Combine(temp.Path, PersistentStorageHelperTests.BlobName(DateTime.UtcNow));
        var partiallyReleasedPath = PersistentStorageHelperTests.CreateFile(temp.Path, $"{Path.GetFileName(blobPath)}@2020-01-01T000000.0000000Z.lock");
        var nestedLeasePath = $"{partiallyReleasedPath}@2020-01-01T000500.0000000Z.lock";

        var blob = new FileBlob(nestedLeasePath);

        Assert.True(blob.TryWrite([5, 6, 7, 8], leasePeriodMilliseconds));

        var path = blob.FullPath;

        Assert.Equal([path], Directory.GetFiles(temp.Path));
        Assert.Equal([5, 6, 7, 8], File.ReadAllBytes(path));
    }

    [Fact]
    public void TryWrite_NewBlob_DoesNotLeaveTemporaryFile()
    {
        using var temp = new TemporaryDirectory();

        var blobPath = Path.Combine(temp.Path, PersistentStorageHelperTests.BlobName(DateTime.UtcNow));
        var blob = new FileBlob(blobPath);

        Assert.True(blob.TryWrite([1, 2, 3]));

        Assert.Equal([blobPath], Directory.GetFiles(temp.Path));
        Assert.Equal([1, 2, 3], File.ReadAllBytes(blobPath));
    }

    [Fact]
    public void TryWrite_ExistingBlob_DoesNotCountReplacedContentTowardsStorageLimit()
    {
        using var temp = new TemporaryDirectory();
        using var provider = new FileBlobProvider(temp.Path, maxSizeInBytes: 10);

        Assert.True(provider.TryCreateBlob([1, 2, 3, 4], 60_000, out var blob));
        Assert.True(blob.TryWrite([5, 6, 7, 8], 60_000));
        Assert.True(blob.TryWrite([9, 10, 11, 12], 60_000));

        // Only the 4 bytes of the blob's current content are stored, so there is still space for another blob.
        Assert.True(provider.TryCreateBlob([13, 14, 15, 16], out _));
    }

    [Fact]
    public void TryWrite_BlobWhoseFileWasRemoved_UsesTemporaryFileThatIsNotTimedOutImmediately()
    {
        using var temp = new TemporaryDirectory();

        // A blob created a long time ago whose file has since been removed, such as by the retention period
        var blobPath = Path.Combine(temp.Path, PersistentStorageHelperTests.BlobName(new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        var blob = new FileBlob(blobPath);

        // Make moving the temporary file into place fail so that it is left behind to be inspected
        Directory.CreateDirectory(blobPath);

        Assert.False(blob.TryWrite([4, 5, 6]));

        var temporaryFile = Assert.Single(Directory.GetFiles(temp.Path, "*.tmp"));

        // The temporary file is not timed out until the write timeout has elapsed since it was written,
        // regardless of when the blob was created, so that it is not removed while the write is in progress.
        var utcNow = DateTime.UtcNow;
        var timeoutDeadline = utcNow - TimeSpan.FromMilliseconds(PersistentStorageHelperTests.DefaultWriteTimeoutMilliseconds);
        Assert.False(PersistentStorageHelper.RemoveTimedOutTmpFiles(timeoutDeadline, temporaryFile));
        Assert.True(File.Exists(temporaryFile));

        Assert.True(PersistentStorageHelper.RemoveTimedOutTmpFiles(utcNow.AddMinutes(5), temporaryFile));
        Assert.False(File.Exists(temporaryFile));
    }

    [Fact]
    public void TryWrite_FailedWriteToExistingBlob_LeavesTemporaryFileThatIsTimedOutLater()
    {
        using var temp = new TemporaryDirectory();

        // A blob created a long time ago, whose lease has also long since expired
        var blobPath = Path.Combine(temp.Path, PersistentStorageHelperTests.BlobName(new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        var leasePath = PersistentStorageHelperTests.CreateFile(temp.Path, $"{Path.GetFileName(blobPath)}@2020-01-01T000500.0000000Z.lock");

        var blob = new FileBlob(leasePath);

        // Writing to the leased blob without a lease moves it back to its original name. If a file with that name
        // has since been created, the move fails, so the temporary file is left behind.
        File.WriteAllBytes(blobPath, [7, 8, 9]);
        Assert.False(blob.TryWrite([4, 5, 6]));

        var temporaryFile = Assert.Single(Directory.GetFiles(temp.Path, "*.tmp"));

        // The temporary file is not timed out until the write timeout has elapsed since it was written, regardless of
        // when the blob was created or its lease expired, so that it is not removed while the write is in progress...
        var utcNow = DateTime.UtcNow;
        var timeoutDeadline = utcNow - TimeSpan.FromMilliseconds(PersistentStorageHelperTests.DefaultWriteTimeoutMilliseconds);
        Assert.False(PersistentStorageHelper.RemoveTimedOutTmpFiles(timeoutDeadline, temporaryFile));
        Assert.True(File.Exists(temporaryFile));

        // ...and it is removed once it has.
        Assert.True(PersistentStorageHelper.RemoveTimedOutTmpFiles(utcNow.AddMinutes(5), temporaryFile));
        Assert.False(File.Exists(temporaryFile));

        Assert.Equal("data", File.ReadAllText(leasePath));
        Assert.Equal([7, 8, 9], File.ReadAllBytes(blobPath));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(60_000)]
    public void TryWrite_ExistingBlobThatCannotBeClaimed_DoesNotMoveNewContentIntoPlace(int leasePeriodMilliseconds)
    {
        // The existing file of a blob is claimed before its new content is moved into place, rather than being removed
        // afterwards, as by then the new content could have been released to the name the existing file was found at.
        // An open file can only be prevented from being moved on Windows, so the order is only observable there.
        Assert.SkipUnless(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "Open files can only be prevented from being moved on Windows.");

        using var temp = new TemporaryDirectory();
        using var provider = new FileBlobProvider(temp.Path);

        Assert.True(provider.TryCreateBlob([1, 2, 3], 60_000, out var blob));
        var leasePath = ((FileBlob)blob).FullPath;

        using (new FileStream(leasePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.False(blob.TryWrite([4, 5, 6], leasePeriodMilliseconds));
        }

        // The new content is left in a temporary file for the storage maintenance to remove, and the existing blob is unchanged.
        var temporaryFile = Assert.Single(Directory.GetFiles(temp.Path, "*.tmp"));

        Assert.Equal(leasePath, ((FileBlob)blob).FullPath);
        Assert.Equal([leasePath, temporaryFile], Directory.GetFiles(temp.Path).OrderBy(path => path.EndsWith(".tmp", StringComparison.Ordinal)));
        Assert.Equal([1, 2, 3], File.ReadAllBytes(leasePath));
        Assert.Equal([4, 5, 6], File.ReadAllBytes(temporaryFile));
    }

    [Fact]
    public void TryWrite_ExistingBlobWhoseNewContentCannotBeMovedIntoPlace_RestoresExistingBlob()
    {
        using var temp = new TemporaryDirectory();
        using var provider = new FileBlobProvider(temp.Path);

        Assert.True(provider.TryCreateBlob([1, 2, 3], 60_000, out var blob));
        var leasePath = ((FileBlob)blob).FullPath;
        var blobPath = leasePath.Substring(0, leasePath.LastIndexOf('@'));

        // Make moving the new content into place fail after the existing file has been claimed
        Directory.CreateDirectory(blobPath);

        Assert.False(blob.TryWrite([4, 5, 6]));

        // The claimed file is restored, so the blob's existing content is not lost and only the new content is left behind.
        var temporaryFile = Assert.Single(Directory.GetFiles(temp.Path, "*.tmp"));

        Assert.Equal(leasePath, ((FileBlob)blob).FullPath);
        Assert.Equal([1, 2, 3], File.ReadAllBytes(leasePath));
        Assert.Equal([4, 5, 6], File.ReadAllBytes(temporaryFile));
    }

    [Fact]
    public void TryDelete_RemovesBlobWithoutLeavingFilesBehind()
    {
        using var temp = new TemporaryDirectory();
        using var provider = new FileBlobProvider(temp.Path, maxSizeInBytes: 10);

        Assert.True(provider.TryCreateBlob([1, 2, 3, 4, 5, 6, 7, 8], 60_000, out var blob));
        Assert.True(blob.TryDelete());

        Assert.Empty(Directory.GetFiles(temp.Path));

        // The removed blob is no longer counted towards the storage limit.
        Assert.True(provider.TryCreateBlob([1, 2, 3, 4, 5, 6, 7, 8], out _));
    }

    [Fact]
    public void TryDelete_BlobWhoseLeaseWasReleased_DoesNotRemoveIt()
    {
        using var temp = new TemporaryDirectory();
        using var provider = new FileBlobProvider(temp.Path);

        Assert.True(provider.TryCreateBlob([1, 2, 3], 60_000, out var blob));
        var leasePath = ((FileBlob)blob).FullPath;
        var blobPath = leasePath.Substring(0, leasePath.LastIndexOf('@'));

        // Once released, the blob may be leased by another reader, so it is no longer removed.
        Assert.True(PersistentStorageHelper.RemoveExpiredLease(DateTime.UtcNow.AddMinutes(5), leasePath));
        Assert.False(blob.TryDelete());

        Assert.Equal([blobPath], Directory.GetFiles(temp.Path));
    }

    [Fact]
    public void TryRead_EmptyBlob_DoesNotReadIt()
    {
        using var temp = new TemporaryDirectory();
        using var provider = new FileBlobProvider(temp.Path);

        File.WriteAllBytes(Path.Combine(temp.Path, PersistentStorageHelper.GetUniqueFileName(".blob")), []);

        Assert.True(provider.TryGetBlob(out var blob));
        Assert.False(blob.TryRead(out var data));
        Assert.Null(data);
    }

    [Fact]
    public void TryRead_BlobLargerThanStorageLimit_DoesNotReadIt()
    {
        using var temp = new TemporaryDirectory();
        using var provider = new FileBlobProvider(temp.Path, maxSizeInBytes: 4);

        File.WriteAllBytes(Path.Combine(temp.Path, PersistentStorageHelper.GetUniqueFileName(".blob")), [1, 2, 3, 4, 5]);

        Assert.True(provider.TryGetBlob(out var blob));
        Assert.False(blob.TryRead(out var data));
        Assert.Null(data);
    }

    [Fact]
    public void TryRead_BlobWithinStorageLimit_ReadsIt()
    {
        using var temp = new TemporaryDirectory();
        using var provider = new FileBlobProvider(temp.Path, maxSizeInBytes: 4);

        Assert.True(provider.TryCreateBlob([1, 2, 3, 4], out var blob));
        Assert.True(blob.TryRead(out var data));
        Assert.Equal([1, 2, 3, 4], data);
    }

#if NET
    [Fact]
    public async Task MaintenanceEvent_RecreatesDeletedDirectoryWithOwnerOnlyPermissions()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Unix file modes do not apply on Windows.");
            return;
        }

        const UnixFileMode GroupAndOther =
            UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;

        using var root = new TemporaryDirectory();

        var path = Path.Combine(root.Path, "traces");
        using var provider = new FileBlobProvider(path, maintenancePeriodInMilliseconds: 50);

        Directory.Delete(path);

        var timeout = TimeSpan.FromSeconds(30);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        while (!Directory.Exists(path) && stopwatch.Elapsed < timeout)
        {
            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        Assert.True(Directory.Exists(path), "The storage directory was not recreated.");
        Assert.Equal(UnixFileMode.None, File.GetUnixFileMode(path) & GroupAndOther);
    }

    [Fact]
    public void TryCreateBlob_RestrictsStoragePermissionsToOwner()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Unix file modes do not apply on Windows.");
            return;
        }

        const UnixFileMode GroupAndOther =
            UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;

        using var root = new TemporaryDirectory();

        var path = Path.Combine(root.Path, "traces");
        using var provider = new FileBlobProvider(path);

        Assert.True(provider.TryCreateBlob([1, 2, 3], out var blob));

        var blobPath = Assert.IsType<FileBlob>(blob).FullPath;

        Assert.Equal(UnixFileMode.None, File.GetUnixFileMode(path) & GroupAndOther);
        Assert.Equal(UnixFileMode.None, File.GetUnixFileMode(blobPath) & GroupAndOther);
    }

    [Fact]
    public void TryRead_BlobThatIsSymbolicLink_DoesNotReadIt()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Creating symbolic links on Windows requires elevation.");
            return;
        }

        using var root = new TemporaryDirectory();

        var secret = Path.Combine(root.Path, "secret.key");
        File.WriteAllBytes(secret, [1, 2, 3]);

        var path = Path.Combine(root.Path, "traces");
        using var provider = new FileBlobProvider(path);

        File.CreateSymbolicLink(Path.Combine(path, PersistentStorageHelper.GetUniqueFileName(".blob")), secret);

        Assert.True(provider.TryGetBlob(out var blob));
        Assert.False(blob.TryRead(out var data));
        Assert.Null(data);
    }
#endif
}

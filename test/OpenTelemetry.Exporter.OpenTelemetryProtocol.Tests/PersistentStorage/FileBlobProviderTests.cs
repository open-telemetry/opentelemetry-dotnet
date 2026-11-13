// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.InteropServices;
using System.Text;
#if NET
using System.Formats.Tar;
using System.Runtime.Versioning;
#endif
using OpenTelemetry.PersistentStorage.FileSystem;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests.PersistentStorage;

[Collection(EventSourceCollectionDefinition.Name)]
public class FileBlobProviderTests
{
#if NET
    private const UnixFileMode OwnerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

#endif
    [Fact]
    public void FileBlobProvider_CreatesSubDirectory()
    {
        var testDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));

        using var blobProvider = new FileBlobProvider(testDirectory.FullName);

        Assert.Equal(testDirectory.FullName, blobProvider.DirectoryPath);
        Directory.Exists(testDirectory.FullName);

        // clean up
        Directory.Delete(testDirectory.FullName, true);
    }

    [Fact]
    public void FileBlobProvider_E2E_Test()
    {
        var testDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
        using var blobProvider = new FileBlobProvider(testDirectory.FullName);

        var data = Encoding.UTF8.GetBytes("Hello, World!");

        // Create blob.
        Assert.True(blobProvider.TryCreateBlob(data.AsSpan(), out var blob1));

        // Get blob.
        Assert.True(blobProvider.TryGetBlob(out var blob2));

        Assert.Single(blobProvider.GetBlobs());

        // Verify file name from both create blob and get blob are same.
        Assert.Equal(((FileBlob)blob1).FullPath, ((FileBlob)blob2).FullPath);

        // Validate if content in the blob is same as buffer data passed to create blob.
        Assert.True(blob1.TryRead(out var blobContent));
        Assert.Equal(data, blobContent);

        testDirectory.Delete(true);
    }

    [Fact]
    public void FileBlobProvider_CreateBlobReturnsNullIfBlobProviderIsFull()
    {
        var testDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
        using var blobProvider = new FileBlobProvider(testDirectory.FullName, 100);

        // write a file to fill up the configured max space.
        Assert.True(blobProvider.TryCreateBlob(new byte[100].AsSpan(), out _));

        var data = Encoding.UTF8.GetBytes("Hello, World!");

        Assert.False(blobProvider.TryCreateBlob(data.AsSpan(), out var blob));
        Assert.Null(blob);

        testDirectory.Delete(true);
    }

    [Fact]
    [Obsolete("Validates obsolete byte[] overload compatibility.")]
    public void FileBlobProvider_CreateBlobReturnsFalseForNullByteArray()
    {
        var testDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
        using var blobProvider = new FileBlobProvider(testDirectory.FullName);
        byte[] buffer = null!;

        Assert.False(blobProvider.TryCreateBlob(buffer, out var blob));
        Assert.False(blobProvider.TryCreateBlob(buffer, 1000, out var leasedBlob));

        Assert.Null(blob);
        Assert.Null(leasedBlob);
        Assert.Empty(Directory.EnumerateFiles(blobProvider.DirectoryPath));

        testDirectory.Delete(true);
    }

    [Fact]
    public void FileBlobProvider_TestRetentionPeriod()
    {
        var testDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
        long maxSizeInBytes = 100000;
        var maintenancePeriodInMilliseconds = 3000;
        var retentionPeriodInMilliseconds = 1000;
        var writeTimeOutInMilliseconds = 1000;
        using var blobProvider = new FileBlobProvider(
            testDirectory.FullName,
            maxSizeInBytes,
            maintenancePeriodInMilliseconds,
            retentionPeriodInMilliseconds,
            writeTimeOutInMilliseconds);

        var data = Encoding.UTF8.GetBytes("Hello, World!");
        Assert.True(blobProvider.TryCreateBlob(data.AsSpan(), out var blob));

        // Wait for retention deadline to expire
        Thread.Sleep(2000);
        var retentionDeadline = DateTime.UtcNow - TimeSpan.FromMilliseconds(retentionPeriodInMilliseconds);
        PersistentStorageHelper.RemoveExpiredBlob(retentionDeadline, ((FileBlob)blob).FullPath);

        // Blob will be deleted as retention period is 1 sec
        Assert.False(File.Exists(((FileBlob)blob).FullPath));

        testDirectory.Delete(true);
    }

    [Fact]
    public void FileBlobProvider_TestWriteTimeoutPeriod()
    {
        var testDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
        long maxSizeInBytes = 100000;
        var maintenancePeriodInMilliseconds = 3000;
        var retentionPeriodInMilliseconds = 2000;
        var writeTimeOutInMilliseconds = 1000;
        using var blobProvider = new FileBlobProvider(
            testDirectory.FullName,
            maxSizeInBytes,
            maintenancePeriodInMilliseconds,
            retentionPeriodInMilliseconds,
            writeTimeOutInMilliseconds);

        var data = Encoding.UTF8.GetBytes("Hello, World!");

        Assert.True(blobProvider.TryCreateBlob(data.AsSpan(), out var blob));

        // Mock write
        File.Move(((FileBlob)blob).FullPath, ((FileBlob)blob).FullPath + ".tmp");

        // validate file moved successfully
        Assert.True(File.Exists(((FileBlob)blob).FullPath + ".tmp"));

        // wait for timeout period
        Thread.Sleep(2000);
        var timeoutDeadline = DateTime.UtcNow - TimeSpan.FromMilliseconds(writeTimeOutInMilliseconds);
        PersistentStorageHelper.RemoveTimedOutTmpFiles(timeoutDeadline, ((FileBlob)blob).FullPath + ".tmp");

        // tmp file will be deleted as write timeout period is 1 sec
        Assert.False(File.Exists(((FileBlob)blob).FullPath + ".tmp"));
        Assert.False(File.Exists(((FileBlob)blob).FullPath));

        testDirectory.Delete(true);
    }

    [Fact]
    public void FileBlobProviderTests_TestLeaseExpiration()
    {
        var testDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
        long maxSizeInBytes = 100000;
        var maintenancePeriodInMilliseconds = 3000;
        var retentionPeriodInMilliseconds = 2000;
        var writeTimeOutInMilliseconds = 1000;
        using var blobProvider = new FileBlobProvider(
            testDirectory.FullName,
            maxSizeInBytes,
            maintenancePeriodInMilliseconds,
            retentionPeriodInMilliseconds,
            writeTimeOutInMilliseconds);

        var data = Encoding.UTF8.GetBytes("Hello, World!");

        Assert.True(blobProvider.TryCreateBlob(data.AsSpan(), out var blob));
        var blobPath = ((FileBlob)blob).FullPath;

        blob.TryLease(1000);
        var leasePath = ((FileBlob)blob).FullPath;
        Assert.True(File.Exists(leasePath));

        // Wait for lease to expire
        Thread.Sleep(2000);
        PersistentStorageHelper.RemoveExpiredLease(DateTime.UtcNow, leasePath);

        // File name will be change to .blob
        Assert.True(File.Exists(blobPath));
        Assert.False(File.Exists(leasePath));

        testDirectory.Delete(true);
    }

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

#if NET11_0_OR_GREATER
        var index = leasePath.LastIndexOf('@', StringComparison.Ordinal);
#else
        var index = leasePath.LastIndexOf('@');
#endif

        var blobPath = leasePath.Substring(0, index);

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

#if NET11_0_OR_GREATER
        var index = leasePath.LastIndexOf('@', StringComparison.Ordinal);
#else
        var index = leasePath.LastIndexOf('@');
#endif

        var blobPath = leasePath.Substring(0, index);

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

#if NET11_0_OR_GREATER
        var index = leasePath.LastIndexOf('@', StringComparison.Ordinal);
#else
        var index = leasePath.LastIndexOf('@');
#endif

        var blobPath = leasePath.Substring(0, index);

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
    public void TryWrite_ExistingBlobWithContentLargerThanStorageLimit_RejectsWithoutReplacingExistingContent()
    {
        using var temp = new TemporaryDirectory();
        using var provider = new FileBlobProvider(temp.Path, maxSizeInBytes: 4);

        Assert.True(provider.TryCreateBlob([1, 2, 3], out var blob));
        Assert.False(blob.TryWrite([1, 2, 3, 4, 5]));
        Assert.False(blob.TryWrite([1, 2, 3, 4, 5], 60_000));

        // The existing content is still stored, is not leased, and is still readable
        Assert.Single(Directory.EnumerateFiles(temp.Path));
        Assert.Single(Directory.EnumerateFiles(temp.Path, "*.blob"));
        Assert.True(blob.TryRead(out var content));
        Assert.Equal<byte>([1, 2, 3], content);
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

#if NET11_0_OR_GREATER
        var index = leasePath.LastIndexOf('@', StringComparison.Ordinal);
#else
        var index = leasePath.LastIndexOf('@');
#endif

        var blobPath = leasePath.Substring(0, index);

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

#if NET11_0_OR_GREATER
        var index = leasePath.LastIndexOf('@', StringComparison.Ordinal);
#else
        var index = leasePath.LastIndexOf('@');
#endif

        var blobPath = leasePath.Substring(0, index);

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
    public void Constructor_RelativePath_UsesAbsolutePath()
    {
        using var root = new TemporaryDirectory();

        // The directory that is verified must be the one that is used, even if the current directory changes later
        var relativePath = Path.Combine(Path.GetRelativePath(Directory.GetCurrentDirectory(), root.Path), "traces");

        if (Path.IsPathRooted(relativePath))
        {
            Assert.Skip("The temporary directory cannot be expressed as a relative path.");
            return;
        }

        using var provider = new FileBlobProvider(relativePath);

        Assert.True(Path.IsPathRooted(provider.DirectoryPath));
        Assert.Equal(Path.GetFullPath(relativePath), provider.DirectoryPath);
        Assert.True(provider.TryCreateBlob([1, 2, 3], out var blob));
        Assert.Equal(provider.DirectoryPath, Path.GetDirectoryName(Assert.IsType<FileBlob>(blob).FullPath));
    }

    [Fact]
    public void BoundedMemoryStream_FileReplacedAfterAttributesWereRead_DoesNotBufferTheFile()
    {
        using var root = new TemporaryDirectory();

        // If a path is replaced by a regular file after it was checked to be a directory, archiving it
        // would copy its contents into memory, so the size of the archive must be bounded
        var path = Path.Combine(root.Path, "large");
        File.WriteAllBytes(path, new byte[1024 * 1024]);

        using var stream = new BoundedMemoryStream(64 * 1024);
        using var writer = new TarWriter(stream, TarEntryFormat.Pax, leaveOpen: true);

        Assert.Throws<IOException>(() => writer.WriteEntry(path, entryName: "entry"));
        Assert.True(stream.Length <= 64 * 1024);
    }

    [Fact]
    public void BoundedMemoryStream_WritesUpToTheMaximumLength()
    {
        using var stream = new BoundedMemoryStream(4);

        stream.Write([1, 2], 0, 2);
        stream.WriteByte(3);
        stream.Write(new ReadOnlySpan<byte>([4]));

        Assert.Equal([1, 2, 3, 4], stream.ToArray());
        Assert.Throws<IOException>(() => stream.WriteByte(5));
        Assert.Throws<IOException>(() => stream.Write([5], 0, 1));
        Assert.Throws<IOException>(() => stream.Write(new ReadOnlySpan<byte>([5])));
    }

    [Fact]
    public void BoundedMemoryStream_SetLengthOrCapacityBeyondTheMaximumLength_Throws()
    {
        using var stream = new BoundedMemoryStream(4);

        // Neither can be used to allocate more memory than the stream is allowed to hold
        Assert.Throws<IOException>(() => stream.SetLength(5));
        Assert.Equal(0, stream.Length);

        stream.Capacity = 1024;

        Assert.Equal(4, stream.Capacity);

        stream.SetLength(4);

        Assert.Equal(4, stream.Length);
    }

    [Fact]
    public void BoundedMemoryStream_GrowthDoesNotExceedTheMaximumLength()
    {
        // The stream doubles its capacity as it grows, from a minimum of 256 bytes
        using var stream = new BoundedMemoryStream(300);

        stream.Write(new byte[200], 0, 200);
        stream.Write(new byte[100], 0, 100);

        Assert.Equal(300, stream.Length);
        Assert.Equal(300, stream.Capacity);
        Assert.Throws<IOException>(() => stream.WriteByte(0));
    }

    [Fact]
    public void TryGetBlob_DirectoryReplacedByDirectoryWritableByOtherUsers_DoesNotReturnForeignBlobs()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Unix file modes do not apply on Windows.");
            return;
        }

        using var root = new TemporaryDirectory();

        var path = Path.Combine(root.Path, "traces");
        using var provider = new FileBlobProvider(path);

        // Another user recreates the directory after it was verified, for example after it was cleaned up
        Directory.Delete(path, recursive: true);
        CreateDirectory(path, OwnerOnly | UnixFileMode.OtherWrite);

        var blobPath = Path.Combine(path, PersistentStorageHelper.GetUniqueFileName(".blob"));
        File.WriteAllBytes(blobPath, [1, 2, 3]);

        Assert.False(provider.TryGetBlob(out _));
        Assert.False(provider.TryCreateBlob([4, 5, 6], out _));
        Assert.Equal([blobPath], Directory.GetFiles(path));
    }

    [Fact]
    public void TryRead_DirectoryReplacedByDirectoryWritableByOtherUsersAfterBlobWasListed_DoesNotReturnForeignContent()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Unix file modes do not apply on Windows.");
            return;
        }

        using var root = new TemporaryDirectory();

        var path = Path.Combine(root.Path, "traces");
        using var provider = new FileBlobProvider(path);

        Assert.True(provider.TryCreateBlob([1, 2, 3], out _));
        Assert.True(provider.TryGetBlob(out var blob));

        var blobPath = Assert.IsType<FileBlob>(blob).FullPath;

        // Another user replaces the directory after the blob was listed, and before its content is read
        Directory.Delete(path, recursive: true);
        CreateDirectory(path, OwnerOnly | UnixFileMode.OtherWrite);
        File.WriteAllBytes(blobPath, [4, 5, 6]);

        Assert.False(blob.TryRead(out var data));
        Assert.Null(data);
    }

    [Fact]
    public void TryGetBlob_DirectoryReplacedByDirectoryWritableByOtherUsersWithoutPermissionChecks_ReturnsBlobs()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Unix file modes do not apply on Windows.");
            return;
        }

        using var root = new TemporaryDirectory();

        var path = Path.Combine(root.Path, "traces");
        using var provider = new FileBlobProvider(path, checkPermissions: false);

        Directory.Delete(path, recursive: true);
        CreateDirectory(path, OwnerOnly | UnixFileMode.OtherWrite);

        Assert.True(provider.TryCreateBlob([1, 2, 3], out _));
        Assert.True(provider.TryGetBlob(out _));
    }

    [Fact]
    public async Task MaintenanceEvent_DirectoryReplacedByDirectoryWritableByOtherUsers_DoesNotRemoveFiles()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Unix file modes do not apply on Windows.");
            return;
        }

        using var root = new TemporaryDirectory();

        var path = Path.Combine(root.Path, "traces");
        using var provider = new FileBlobProvider(path, maintenancePeriodInMilliseconds: 50, retentionPeriodInMilliseconds: 1);

        Directory.Delete(path, recursive: true);
        CreateDirectory(path, OwnerOnly | UnixFileMode.OtherWrite);

        // The blob has expired, so it would be removed by maintenance if the directory was trusted
        var blobPath = Path.Combine(path, PersistentStorageHelper.GetUniqueFileName(".blob"));
        File.WriteAllBytes(blobPath, [1, 2, 3]);

        await Task.Delay(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        Assert.True(File.Exists(blobPath));
    }

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

    [Theory]
    [InlineData(UnixFileMode.GroupWrite)]
    [InlineData(UnixFileMode.OtherWrite)]
    [InlineData(UnixFileMode.OtherWrite | UnixFileMode.StickyBit)]
    public void Constructor_ExistingDirectoryWritableByOtherUsers_Throws(UnixFileMode mode)
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Unix file modes do not apply on Windows.");
            return;
        }

        using var root = new TemporaryDirectory();

        // Another user could plant blobs in the directory that would be sent with the exporter's credentials
        var path = Path.Combine(root.Path, "traces");
        CreateDirectory(path, OwnerOnly | mode);

        Assert.Throws<UnauthorizedAccessException>(() => new FileBlobProvider(path));
    }

    [Theory]
    [InlineData(UnixFileMode.GroupWrite)]
    [InlineData(UnixFileMode.OtherWrite)]
    [InlineData(UnixFileMode.OtherWrite | UnixFileMode.StickyBit)]
    public void Constructor_ExistingDirectoryWritableByOtherUsersWithoutPermissionChecks_DoesNotThrow(UnixFileMode mode)
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Unix file modes do not apply on Windows.");
            return;
        }

        using var root = new TemporaryDirectory();

        var path = Path.Combine(root.Path, "traces");
        CreateDirectory(path, OwnerOnly | mode);

        using var provider = new FileBlobProvider(path, checkPermissions: false);

        Assert.True(provider.TryCreateBlob([1, 2, 3], out _));
    }

    [Theory]
    [InlineData(UnixFileMode.GroupWrite)]
    [InlineData(UnixFileMode.OtherWrite)]
    public void Constructor_DirectoryInDirectoryWritableByOtherUsers_Throws(UnixFileMode mode)
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Unix file modes do not apply on Windows.");
            return;
        }

        using var root = new TemporaryDirectory();

        // Another user could replace the directory with one of their own after it has been verified
        var parent = Path.Combine(root.Path, "retry");
        CreateDirectory(parent, OwnerOnly | mode);

        var path = Path.Combine(parent, "traces");

        Assert.Throws<UnauthorizedAccessException>(() => new FileBlobProvider(path));
    }

    [Fact]
    public void Constructor_DirectoryInStickyDirectoryWritableByOtherUsers_DoesNotThrow()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Unix file modes do not apply on Windows.");
            return;
        }

        using var root = new TemporaryDirectory();

        // Like /tmp, other users cannot replace a directory they do not own in a directory with the sticky bit set
        var parent = Path.Combine(root.Path, "retry");
        CreateDirectory(parent, OwnerOnly | UnixFileMode.GroupWrite | UnixFileMode.OtherWrite | UnixFileMode.StickyBit);

        var path = Path.Combine(parent, "traces");
        using var provider = new FileBlobProvider(path);

        Assert.True(provider.TryCreateBlob([1, 2, 3], out _));
    }

    [Fact]
    public void Constructor_ExistingDirectoryOwnedByAnotherUser_Throws()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Unix file modes do not apply on Windows.");
            return;
        }

        if (Environment.IsPrivilegedProcess)
        {
            Assert.Skip("The root directory is owned by the current user.");
            return;
        }

        // The root directory is owned by root and is not writable by other users
        var path = Path.GetPathRoot(Path.GetTempPath())!;

        Assert.Throws<UnauthorizedAccessException>(() => new FileBlobProvider(path));
    }

    [Fact]
    public void Constructor_SymbolicLinkToPrivateDirectory_DoesNotThrow()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Unix file modes do not apply on Windows.");
            return;
        }

        using var root = new TemporaryDirectory();

        var target = Path.Combine(root.Path, "target");
        CreateDirectory(target, OwnerOnly);

        var path = Path.Combine(root.Path, "traces");
        Directory.CreateSymbolicLink(path, "target");

        using var provider = new FileBlobProvider(path);

        Assert.True(provider.TryCreateBlob([1, 2, 3], out _));
        Assert.Single(Directory.GetFiles(target));
    }

    [Fact]
    public void Constructor_SymbolicLinkToDirectoryWritableByOtherUsers_Throws()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Unix file modes do not apply on Windows.");
            return;
        }

        using var root = new TemporaryDirectory();

        var target = Path.Combine(root.Path, "shared");
        CreateDirectory(target, OwnerOnly | UnixFileMode.OtherWrite);

        var path = Path.Combine(root.Path, "traces");
        Directory.CreateSymbolicLink(path, target);

        Assert.Throws<UnauthorizedAccessException>(() => new FileBlobProvider(path));
    }

    [Fact]
    public void Constructor_SymbolicLinkWithParentSegmentAfterSymbolicLink_VerifiesDirectoryTheOperatingSystemUses()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Unix file modes do not apply on Windows.");
            return;
        }

        using var root = new TemporaryDirectory();

        var shared = Path.Combine(root.Path, "shared");
        var target = Path.Combine(shared, "target");

        CreateDirectory(shared, OwnerOnly | UnixFileMode.OtherWrite);
        CreateDirectory(target, OwnerOnly);
        Directory.CreateSymbolicLink(Path.Combine(root.Path, "link"), target);

        // The operating system resolves "link/.." in the target of a link to the shared directory, not to the root directory
        Directory.CreateSymbolicLink(Path.Combine(root.Path, "retry"), "link/..");

        var path = Path.Combine(root.Path, "retry", "traces");

        Assert.Throws<UnauthorizedAccessException>(() => new FileBlobProvider(path));
        Assert.True(Directory.Exists(Path.Combine(shared, "traces")));
    }

    [Fact]
    public void Constructor_ParentSegment_VerifiesDirectoryTheFileSystemApisUse()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Unix file modes do not apply on Windows.");
            return;
        }

        using var root = new TemporaryDirectory();

        var shared = Path.Combine(root.Path, "shared");
        var target = Path.Combine(shared, "target");

        CreateDirectory(shared, OwnerOnly | UnixFileMode.OtherWrite);
        CreateDirectory(target, OwnerOnly);
        Directory.CreateSymbolicLink(Path.Combine(root.Path, "link"), target);

        // .NET removes "link/.." from a path before using it, unlike the operating system
        var path = Path.Combine(root.Path, "link", "..", "traces");
        using var provider = new FileBlobProvider(path);

        Assert.True(provider.TryCreateBlob([1, 2, 3], out _));
        Assert.Single(Directory.GetFiles(Path.Combine(root.Path, "traces")));
        Assert.False(Directory.Exists(Path.Combine(shared, "traces")));
    }

    [Fact]
    public void Constructor_SymbolicLinkLoop_Throws()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Unix file modes do not apply on Windows.");
            return;
        }

        using var root = new TemporaryDirectory();

        Directory.CreateSymbolicLink(Path.Combine(root.Path, "a"), "b");
        Directory.CreateSymbolicLink(Path.Combine(root.Path, "b"), "a");

        Assert.ThrowsAny<IOException>(() => new FileBlobProvider(Path.Combine(root.Path, "a", "traces")));
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

#if NET
    [UnsupportedOSPlatform("windows")]
    private static void CreateDirectory(string path, UnixFileMode mode)
    {
        // The mode is set after the directory is created, as the mode it is created with is restricted by the umask
        Directory.CreateDirectory(path);
        File.SetUnixFileMode(path, mode);
    }
#endif
}

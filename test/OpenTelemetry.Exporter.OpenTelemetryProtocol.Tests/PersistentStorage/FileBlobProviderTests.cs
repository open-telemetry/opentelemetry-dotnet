// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

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
#endif
}

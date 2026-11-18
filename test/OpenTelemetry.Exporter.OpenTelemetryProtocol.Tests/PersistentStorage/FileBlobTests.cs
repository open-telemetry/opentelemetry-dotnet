// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using OpenTelemetry.PersistentStorage.Abstractions;
using OpenTelemetry.PersistentStorage.FileSystem;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests.PersistentStorage;

public class FileBlobTests
{
    [Fact]
    public void FileBlobTests_E2E_Test()
    {
        var testFile = new FileInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
        PersistentBlob blob = new FileBlob(testFile.FullName);

        var data = Encoding.UTF8.GetBytes("Hello, World!");
        blob.TryWrite(data.AsSpan());
        blob.TryRead(out var blobContent);

        Assert.Equal(testFile.FullName, ((FileBlob)blob).FullPath);
        Assert.Equal(data, blobContent);

        Assert.True(blob.TryDelete());
        Assert.False(testFile.Exists);
    }

    [Fact]
    public void FileBlobTests_Lease()
    {
        var testFile = new FileInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
        PersistentBlob blob = new FileBlob(testFile.FullName);

        var data = Encoding.UTF8.GetBytes("Hello, World!");
        var leasePeriodMilliseconds = 1000;
        blob.TryWrite(data.AsSpan());
        blob.TryLease(leasePeriodMilliseconds);

        Assert.Contains(".lock", ((FileBlob)blob).FullPath, StringComparison.Ordinal);

        Assert.True(blob.TryDelete());
        Assert.False(testFile.Exists);
    }

    [Fact]
    public void FileBlobTests_LeaseAfterDelete()
    {
        var testFile = new FileInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
        PersistentBlob blob = new FileBlob(testFile.FullName);

        var data = Encoding.UTF8.GetBytes("Hello, World!");

        Assert.True(blob.TryWrite(data.AsSpan()));
        Assert.True(blob.TryDelete());

        // Lease should return false
        Assert.False(blob.TryLease(1000));
    }

    [Fact]
    public void FileBlobTests_ReadFailsOnAlreadyLeasedFile()
    {
        var testFile = new FileInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
        var blob1 = new FileBlob(testFile.FullName);
        var blob2 = new FileBlob(testFile.FullName);
        var data = Encoding.UTF8.GetBytes("Hello, World!");

        Assert.True(blob2.TryWrite(data.AsSpan()));

        // Leased by another thread/process/object
        Assert.True(blob2.TryLease(10000));

        // Read should fail as file is leased
        Assert.False(blob1.TryRead(out var blob));
        Assert.Null(blob);

        // Clean up
        Assert.True(blob2.TryDelete());
    }

    [Fact]
    public void FileBlobTests_LeaseFailsOnAlreadyLeasedFileByOtherObject()
    {
        var testFile = new FileInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
        var blob1 = new FileBlob(testFile.FullName);
        var blob2 = new FileBlob(testFile.FullName);
        var data = Encoding.UTF8.GetBytes("Hello, World!");

        Assert.True(blob1.TryWrite(data.AsSpan()));

        // Leased by another thread/process/object
        Assert.True(blob2.TryLease(10000));

        // Lease should fail as already leased
        Assert.False(blob1.TryLease(10));

        // Clean up
        Assert.True(blob2.TryDelete());
    }

    [Fact]
    public void FileBlobTests_Delete()
    {
        var testFile = new FileInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
        var blob = new FileBlob(testFile.FullName);

        var data = Encoding.UTF8.GetBytes("Hello, World!");

        Assert.True(blob.TryWrite(data.AsSpan()));

        // Assert
        Assert.True(blob.TryDelete());
        Assert.False(testFile.Exists);
    }

    [Fact]
    public void FileBlobTests_DeleteFailsAfterLeaseIsExpired()
    {
        var testDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));

        // set maintenance job interval to 2 ms, with enough space to store the blob
        using var storage = new FileBlobProvider(testDirectory.FullName, maxSizeInBytes: 100, maintenancePeriodInMilliseconds: 2);

        var data = Encoding.UTF8.GetBytes("Hello, World!");

        Assert.True(storage.TryCreateBlob(data.AsSpan(), out var blob));

        var leasePeriodMilliseconds = 1;

        // lease for 1 ms
        blob.TryLease(leasePeriodMilliseconds);

        // Wait for lease to expire and maintenance job to run
        Thread.Sleep(5000);

        blob.TryDelete();

        // Assert
        Assert.True(storage.TryGetBlob(out var outputBlob));
        Assert.NotNull(outputBlob);

        testDirectory.Delete(true);
    }

    [Fact]
    public void FileBlobTests_LeaseTimeIsUpdatedWhenLeasingAlreadyLeasedFile()
    {
        var testFile = new FileInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
        var blob = new FileBlob(testFile.FullName);
        var data = Encoding.UTF8.GetBytes("Hello, World!");

        Assert.True(blob.TryWrite(data.AsSpan()));

        var initialLeasePeriodMilliseconds = 10_000;
        Assert.True(blob.TryLease(initialLeasePeriodMilliseconds));

        var leaseTime = PersistentStorageHelper.GetDateTimeFromLeaseName(blob.FullPath);

        var extendedLeasePeriodMilliseconds = 20_000;
        Assert.True(blob.TryLease(extendedLeasePeriodMilliseconds));

        var newLeaseTime = PersistentStorageHelper.GetDateTimeFromLeaseName(blob.FullPath);

        Assert.True(
            newLeaseTime > leaseTime,
            $"Expected new lease time > old lease time. Old: {leaseTime:o}, New: {newLeaseTime:o}");

        Assert.True(blob.TryDelete());
    }

    [Fact]
    public void FileBlobTests_FailedWrite()
    {
        var nonExistingPath = Path.Combine("FakePath:/", Path.GetRandomFileName());
        var blob = new FileBlob(nonExistingPath);

        Assert.False(blob.TryWrite([]));
    }
}

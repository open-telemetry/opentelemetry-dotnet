// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.PersistentStorage.FileSystem;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests.PersistentStorage;

public class DirectorySizeTrackerTests
{
    [Fact]
    public void VerifyDirectorySizeTracker()
    {
        var testDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
        testDirectory.Create();

        var directorySizeTracker = new DirectorySizeTracker(maxSizeInBytes: 100, path: testDirectory.FullName);

        // new directory, expected to have space
        Assert.True(directorySizeTracker.IsSpaceAvailable(0, out var currentSize1));
        Assert.Equal(0, currentSize1);

        // add a file and check current space.
        directorySizeTracker.FileAdded(30);
        Assert.True(directorySizeTracker.IsSpaceAvailable(0, out var currentSize2));
        Assert.Equal(30, currentSize2);

        // add a file and check current space. Here we've exceeded the configured max size
        directorySizeTracker.FileAdded(100);
        Assert.False(directorySizeTracker.IsSpaceAvailable(0, out var currentSize3));
        Assert.Equal(130, currentSize3);

        // remove a file and check current space.
        directorySizeTracker.FileRemoved(50);
        Assert.True(directorySizeTracker.IsSpaceAvailable(0, out var currentSize4));
        Assert.Equal(80, currentSize4);

        // since we haven't actually written any files to disk,
        // Recount will reset to zero.
        directorySizeTracker.RecountCurrentSize();
        Assert.True(directorySizeTracker.IsSpaceAvailable(0, out var currentSize5));
        Assert.Equal(0, currentSize5);

        // cleanup
        testDirectory.Delete();
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(30, 70, true)]
    [InlineData(30, 71, false)]
    [InlineData(100, 0, true)]
    [InlineData(100, 1, false)]
    [InlineData(101, 0, false)]
    [InlineData(0, -1, false)]
    [InlineData(long.MaxValue, 1, false)]
    public void IsSpaceAvailable_ConsidersTheSizeOfTheBlobToBeWritten(long currentSize, long additionalSize, bool expected)
    {
        using var temp = new TemporaryDirectory();

        var directorySizeTracker = new DirectorySizeTracker(maxSizeInBytes: 100, path: temp.Path);

        directorySizeTracker.FileAdded(currentSize);

        Assert.Equal(100, directorySizeTracker.MaxSizeInBytes);
        Assert.Equal(expected, directorySizeTracker.IsSpaceAvailable(additionalSize, out var size));
        Assert.Equal(currentSize, size);
    }

    [Fact]
    public void CalculateFolderSize_IgnoresFilesInSubdirectories()
    {
        using var temp = new TemporaryDirectory();

        File.WriteAllBytes(Path.Combine(temp.Path, "top.bin"), new byte[3]);

        var nested = Path.Combine(temp.Path, "nested");
        Directory.CreateDirectory(nested);
        File.WriteAllBytes(Path.Combine(nested, "large.bin"), new byte[1024]);

        Assert.Equal(3, DirectorySizeTracker.CalculateFolderSize(temp.Path));
    }
}

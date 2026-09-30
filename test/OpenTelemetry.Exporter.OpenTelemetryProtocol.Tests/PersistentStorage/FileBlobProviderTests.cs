// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.PersistentStorage.FileSystem;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests.PersistentStorage;

public class FileBlobProviderTests
{
    [Fact]
    public void TryCreateBlob_WhenWriteExceedsEmptyStorageLimit_RejectsWithoutPersisting()
    {
        var path = CreateTempDirectory();
        try
        {
            using var provider = new FileBlobProvider(path, maxSizeInBytes: 3);

            Assert.False(provider.TryCreateBlob(new byte[4].AsSpan(), out var blob));
            Assert.Null(blob);
            Assert.Empty(Directory.EnumerateFiles(path, "*.blob"));
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    public void TryCreateBlob_WhenWriteWouldExceedExistingStorage_RejectsWithoutPersisting()
    {
        var path = CreateTempDirectory();
        try
        {
            File.WriteAllBytes(Path.Combine(path, "existing.bin"), new byte[3]);
            using var provider = new FileBlobProvider(path, maxSizeInBytes: 5);

            Assert.False(provider.TryCreateBlob(new byte[3].AsSpan(), out var blob));
            Assert.Null(blob);
            Assert.Empty(Directory.EnumerateFiles(path, "*.blob"));
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    public void TryCreateBlob_WhenWriteExactlyFillsStorageLimit_Accepts()
    {
        var path = CreateTempDirectory();
        try
        {
            using var provider = new FileBlobProvider(path, maxSizeInBytes: 4);

            Assert.True(provider.TryCreateBlob(new byte[4].AsSpan(), out var blob));
            Assert.NotNull(blob);
            Assert.Single(Directory.EnumerateFiles(path, "*.blob"));
            Assert.True(blob!.TryDelete());
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

#if NET
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

        var root = CreateTempDirectory();

        try
        {
            var path = Path.Combine(root, "traces");
            using var provider = new FileBlobProvider(path);

            Assert.True(provider.TryCreateBlob(new byte[] { 1, 2, 3 }.AsSpan(), out var blob));

            var blobPath = Assert.IsType<FileBlob>(blob).FullPath;

            Assert.Equal(UnixFileMode.None, File.GetUnixFileMode(path) & GroupAndOther);
            Assert.Equal(UnixFileMode.None, File.GetUnixFileMode(blobPath) & GroupAndOther);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
#endif

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(path);
        return path;
    }
}

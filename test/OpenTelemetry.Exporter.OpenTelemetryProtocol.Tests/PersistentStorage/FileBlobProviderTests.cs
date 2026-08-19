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

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(path);
        return path;
    }
}

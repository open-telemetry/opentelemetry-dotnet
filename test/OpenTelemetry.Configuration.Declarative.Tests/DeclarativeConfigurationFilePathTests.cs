// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.InteropServices;
using Xunit;

namespace OpenTelemetry.Configuration.Declarative.Tests;

public sealed class DeclarativeConfigurationFilePathTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_NullOrWhitespace_ThrowsArgumentException(string? path) =>
        Assert.ThrowsAny<ArgumentException>(() => new FilePath(path!));

    [Theory]
    [InlineData(".txt")]
    [InlineData(".json")]
    [InlineData(".config")]
    [InlineData("")] // no extension
    public void Constructor_InvalidExtension_ThrowsArgumentException(string extension)
    {
        using var factory = new DeclarativeYamlTestFileFactory();
        var path = Path.Combine(factory.TempDirectory, $"config{extension}");
        var ex = Assert.Throws<ArgumentException>(() => new FilePath(path));
        Assert.Contains(".yaml", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(".yaml")]
    [InlineData(".YAML")]
    [InlineData(".yml")]
    [InlineData(".YML")]
    public void Constructor_ValidExtension_DoesNotThrow(string extension)
    {
        using var factory = new DeclarativeYamlTestFileFactory();
        var path = Path.Combine(factory.TempDirectory, $"config{extension}");
        _ = new FilePath(path); // must not throw
    }

    [Fact]
    public void Equals_RelativeAndAbsoluteSameFile_AreEqual()
    {
        using var factory = new DeclarativeYamlTestFileFactory();
        var absolutePath = factory.CreateDeclarativeYaml(disabled: true);
        var originalDirectory = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(factory.TempDirectory);
            var relativePath = Path.GetFileName(absolutePath);

            Assert.Equal(new FilePath(absolutePath), new FilePath(relativePath));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
        }
    }

    [Fact]
    public void Equals_DifferentPathCasingOnWindows_AreEqual()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        using var factory = new DeclarativeYamlTestFileFactory();
        var absolutePath = factory.CreateDeclarativeYaml(disabled: true);

        Assert.Equal(new FilePath(absolutePath), new FilePath(absolutePath.ToUpperInvariant()));
    }

    [Fact]
    public void ToString_ReturnsOriginalPath()
    {
        using var factory = new DeclarativeYamlTestFileFactory();
        var path = factory.CreateDeclarativeYaml(disabled: true);

        Assert.Equal(path, new FilePath(path).ToString());
    }

    [Fact]
    public void Path_RelativeInput_IsAbsolute()
    {
        // FilePath.Path must always hold the absolute path resolved at construction time,
        // so that I/O calls succeed even if the working directory changes later.
        using var factory = new DeclarativeYamlTestFileFactory();
        var absolutePath = factory.CreateDeclarativeYaml(disabled: true);
        var originalCwd = Directory.GetCurrentDirectory();

        FilePath fp;
        try
        {
            Directory.SetCurrentDirectory(factory.TempDirectory);
            var relativeName = System.IO.Path.GetFileName(absolutePath);
            fp = new FilePath(relativeName);

            Assert.Equal(absolutePath, fp.Path);
            Assert.Equal(relativeName, fp.ToString());
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCwd);
        }
    }
}

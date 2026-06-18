// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.InteropServices;

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
        // Relative paths resolve against AppContext.BaseDirectory, so the absolute form
        // of a relative name is always Path.Combine(AppContext.BaseDirectory, name).
        // No file needs to exist - FilePath only does path normalisation.
        var relativeName = "otel-config-test.yaml";
        var absolutePath = Path.Combine(AppContext.BaseDirectory, relativeName);

        Assert.Equal(new FilePath(absolutePath), new FilePath(relativeName));
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
    public void GetHashCode_EqualFilePaths_SameHashCode()
    {
        var relativeName = "otel-config-test.yaml";
        var absolutePath = Path.Combine(AppContext.BaseDirectory, relativeName);

        var a = new FilePath(absolutePath);
        var b = new FilePath(relativeName);

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentPathCasingOnWindows_SameHashCode()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        using var factory = new DeclarativeYamlTestFileFactory();
        var absolutePath = factory.CreateDeclarativeYaml(disabled: true);

        var a = new FilePath(absolutePath);
        var b = new FilePath(absolutePath.ToUpperInvariant());

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Path_RelativeInput_ResolvesAgainstAppBaseDirectory()
    {
        var relativeName = "otel-config-test.yaml";
        var expectedAbsolutePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, relativeName));

        var fp = new FilePath(relativeName);

        Assert.Equal(expectedAbsolutePath, fp.Path);
        Assert.Equal(relativeName, fp.ToString());
    }

    [Fact]
    public void Path_RelativeInput_IgnoresCurrentDirectory()
    {
        // Regression test: under IIS in-process hosting, Environment.CurrentDirectory is the
        // IIS worker-process directory, not the application directory. Relative paths must
        // still resolve to the application directory.
        var relativeName = "otel-config-test.yaml";
        var expectedAbsolutePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, relativeName));
        var originalCwd = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(Path.GetTempPath());
            Assert.Equal(expectedAbsolutePath, new FilePath(relativeName).Path);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCwd);
        }
    }

    [Fact]
    public void Path_RootRelativeOnWindows_UsesAppBaseDirectoryDrive()
    {
        // On Windows, a root-relative path (\otel.yaml) is rooted but NOT fully qualified:
        // Path.IsPathRooted returns true but resolution still depends on the current drive.
        // FilePath resolves it by combining with AppContext.BaseDirectory, so the drive
        // letter comes from the application directory rather than the ambient current drive.
        // Path.Combine("C:\App\", "\otel.yaml") -> "C:\otel.yaml" (drive from base preserved).
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        var rootRelative = @"\otel-root-relative-test.yaml";
        var expected = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, rootRelative));

        Assert.Equal(expected, new FilePath(rootRelative).Path);
    }
}

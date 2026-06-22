// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#pragma warning disable OTEL1006

namespace OpenTelemetry.Configuration.Declarative.Tests;

public sealed class FileFormatValidatorTests
{
    // All 1.x versions at or below MaxSupportedMinorVersion must be accepted without warning.
    [Theory]
    [InlineData("1.0")]
    [InlineData("1.0-rc.1")]
    [InlineData("1.0-rc.99")]
    [InlineData("1.1")]
    [InlineData("1.1-rc.1")]
    public void Validate_KnownVersions_AcceptWithNoWarning(string format)
    {
        var warnings = new List<string>();

        FileFormatValidator.Validate(format, warnings.Add);

        Assert.Empty(warnings);
    }

    [Fact]
    public void Validate_VersionWithWhitespace_TrimsAndAcceptsWithNoWarning()
    {
        var warnings = new List<string>();

        FileFormatValidator.Validate($" {FileFormatValidator.SupportedMajorVersion}.{FileFormatValidator.MaxSupportedMinorVersion} ", warnings.Add);

        Assert.Empty(warnings);
    }

    // Minor versions newer than MaxSupportedMinorVersion are accepted but warn (some features may not take effect).
    [Theory]
    [InlineData("1.2")]
    [InlineData("1.2-rc.1")]
    [InlineData("1.99")]
    public void Validate_FutureMinorVersion_AcceptsWithWarning(string format)
    {
        var warnings = new List<string>();

        FileFormatValidator.Validate(format, warnings.Add);

        var warning = Assert.Single(warnings);
        Assert.Contains(format, warning, StringComparison.Ordinal);
        Assert.Contains($"{FileFormatValidator.SupportedMajorVersion}.{FileFormatValidator.MaxSupportedMinorVersion}", warning, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_NullOrWhitespace_ThrowsWithHelpfulMessage(string? format)
    {
        var ex = Assert.Throws<DeclarativeConfigurationException>(
            () => FileFormatValidator.Validate(format, _ => { }));

        Assert.Contains("file_format", ex.Message, StringComparison.Ordinal);
        Assert.Contains($"{FileFormatValidator.SupportedMajorVersion}.{FileFormatValidator.MaxSupportedMinorVersion}", ex.Message, StringComparison.Ordinal);
    }

    // Structurally invalid strings (cannot be parsed as major.minor).
    [Theory]
    [InlineData("banana")]
    [InlineData("1")]
    [InlineData("1.0.0")]
    [InlineData("1.0-rc.")]
    [InlineData("1.0-rc.-1")]
    public void Validate_InvalidFormatString_ThrowsWithInputInMessage(string format)
    {
        var ex = Assert.Throws<DeclarativeConfigurationException>(
            () => FileFormatValidator.Validate(format, _ => { }));

        Assert.Contains(format, ex.Message, StringComparison.Ordinal);
    }

    // Structurally valid but unsupported major version.
    [Theory]
    [InlineData("0.3")]
    [InlineData("0.4")]
    [InlineData("2.0")]
    [InlineData("3.5")]
    public void Validate_UnsupportedMajorVersion_ThrowsWithInputInMessage(string format)
    {
        var ex = Assert.Throws<DeclarativeConfigurationException>(
            () => FileFormatValidator.Validate(format, _ => { }));

        Assert.Contains(format, ex.Message, StringComparison.Ordinal);
        Assert.Contains("major version", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_UnsupportedVersion_DoesNotCallWarn()
    {
        var warned = false;

        Assert.Throws<DeclarativeConfigurationException>(
            () => FileFormatValidator.Validate("99.0", _ => warned = true));

        Assert.False(warned);
    }

    [Fact]
    public void Validate_NullWarn_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => FileFormatValidator.Validate("1.1", null!));
}

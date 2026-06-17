// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#pragma warning disable OTEL1006

namespace OpenTelemetry.Configuration.Declarative.Tests;

public sealed class FileFormatValidatorTests
{
    [Fact]
    public void Validate_ExactExpectedVersion_AcceptsWithNoWarning()
    {
        var warnings = new List<string>();

        FileFormatValidator.Validate("1.0", warnings.Add);

        Assert.Empty(warnings);
    }

    [Theory]
    [InlineData("0.4")]
    [InlineData("1.0-rc.1")]
    [InlineData("1.0-rc.99")]
    public void Validate_SupportedButInexactVersion_AcceptsWithWarning(string format)
    {
        var warnings = new List<string>();

        FileFormatValidator.Validate(format, warnings.Add);

        var warning = Assert.Single(warnings);
        Assert.Contains(format, warning, StringComparison.Ordinal);
        Assert.Contains(FileFormatValidator.ExpectedFileFormat, warning, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ExpectedVersionWithWhitespace_TrimsAndAcceptsWithNoWarning()
    {
        var warnings = new List<string>();

        FileFormatValidator.Validate(" 1.0 ", warnings.Add);

        Assert.Empty(warnings);
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
        Assert.Contains("1.0", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("2.0")]
    [InlineData("0.3")]
    [InlineData("banana")]
    [InlineData("1")]
    [InlineData("1.0.0")]
    [InlineData("1.0-rc.")]
    [InlineData("1.0-rc.-1")]
    public void Validate_UnsupportedVersion_ThrowsWithVersionInMessage(string format)
    {
        var ex = Assert.Throws<DeclarativeConfigurationException>(
            () => FileFormatValidator.Validate(format, _ => { }));

        Assert.Contains(format, ex.Message, StringComparison.Ordinal);
        Assert.Contains("Supported formats", ex.Message, StringComparison.Ordinal);
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
        Assert.Throws<ArgumentNullException>(() => FileFormatValidator.Validate("1.0", null!));
}

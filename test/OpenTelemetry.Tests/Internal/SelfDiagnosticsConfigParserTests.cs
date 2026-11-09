// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Internal.Tests;

public sealed class SelfDiagnosticsConfigParserTests : IDisposable
{
    private readonly TemporaryDirectory directory = new();

    public void Dispose() => this.directory.Dispose();

    [Theory]
    [InlineData(0)]
    [InlineData(1023)]
    [InlineData(32768)]
    [InlineData(int.MaxValue)]
    public void TryGetConfiguration_ReturnsRawValuesAndFullPaths(int fileSize)
    {
        var path = Path.Combine(this.directory.Path, SelfDiagnosticsConfigParser.ConfigFileName);
        File.WriteAllText(path, $$"""{"LogDirectory":"Diagnostics","FileSize":{{fileSize}},"LogLevel":"Error, Warning"}""");
        var parser = new SelfDiagnosticsConfigParser(() => path);

        Assert.True(parser.TryGetConfiguration(out var configuration), "Expected the parser to successfully get the configuration.");
        Assert.Equal(path, configuration.ConfigFilePath);
        Assert.Equal(Path.GetFullPath("Diagnostics"), configuration.LogDirectory);
        Assert.Equal(fileSize, configuration.FileSizeKilobytes);
        Assert.Equal(EventLevel.Warning, configuration.LogLevel);
        Assert.Equal("Error, Warning", configuration.RawLogLevel);
        Assert.False(configuration.FormatMessageSpecified);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not JSON")]
    [InlineData("""{"FileSize":1024,"LogLevel":"Error"}""")]
    [InlineData("""{"LogDirectory":" ","FileSize":1024,"LogLevel":"Error"}""")]
    [InlineData("""{"LogDirectory":"Diagnostics","LogLevel":"Error"}""")]
    [InlineData("""{"LogDirectory":"Diagnostics","FileSize":-1,"LogLevel":"Error"}""")]
    [InlineData("""{"LogDirectory":"Diagnostics","FileSize":2147483648,"LogLevel":"Error"}""")]
    [InlineData("""{"LogDirectory":"Diagnostics","FileSize":1024}""")]
    [InlineData("""{"LogDirectory":"Diagnostics","FileSize":1024,"LogLevel":"warning"}""")]
    [InlineData("""{"LogDirectory":"Diagnostics","FileSize":1024,"LogLevel":"-1"}""")]
    [InlineData("{\"LogDirectory\":\"a\0b\",\"FileSize\":1024,\"LogLevel\":\"Error\"}")]
    public void TryGetConfiguration_InvalidRequiredFields_ReturnFalse(string text)
    {
        var path = Path.Combine(this.directory.Path, SelfDiagnosticsConfigParser.ConfigFileName);
        File.WriteAllText(path, text);
        var parser = new SelfDiagnosticsConfigParser(() => path);

        Assert.False(parser.TryGetConfiguration(out _), "Expected the parser to fail for invalid required fields.");
    }

    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    [InlineData("\"invalid\"")]
    [InlineData("42")]
    [InlineData("null")]
    [InlineData("{}")]
    public void TryGetConfiguration_DetectsFormatMessageRegardlessOfValue(string value)
    {
        var path = Path.Combine(this.directory.Path, SelfDiagnosticsConfigParser.ConfigFileName);
        File.WriteAllText(path, $$"""{"LogDirectory":"Diagnostics","FileSize":1024,"LogLevel":"Warning","formatMessage":{{value}}}""");
        var parser = new SelfDiagnosticsConfigParser(() => path);

        Assert.True(parser.TryGetConfiguration(out var configuration), "Expected the parser to successfully get the configuration.");
        Assert.True(configuration.FormatMessageSpecified);
    }

    [Fact]
    public void TryGetConfiguration_FormatMessageAbsent_IsNotDetected()
    {
        var path = Path.Combine(this.directory.Path, SelfDiagnosticsConfigParser.ConfigFileName);
        File.WriteAllText(path, """{"LogDirectory":"Diagnostics","FileSize":1024,"LogLevel":"Warning"}""");
        var parser = new SelfDiagnosticsConfigParser(() => path);

        Assert.True(parser.TryGetConfiguration(out var configuration), "Expected the parser to successfully get the configuration.");
        Assert.False(configuration.FormatMessageSpecified);
    }

    [Fact]
    public void LocateConfigFile_PrefersWorkingDirectoryThenBaseDirectory()
    {
        using var workingDirectory = new TemporaryDirectory();
        using var baseDirectory = new TemporaryDirectory();
        var workingPath = Path.Combine(workingDirectory.Path, SelfDiagnosticsConfigParser.ConfigFileName);
        var basePath = Path.Combine(baseDirectory.Path, SelfDiagnosticsConfigParser.ConfigFileName);

        Assert.Null(SelfDiagnosticsConfigParser.LocateConfigFile(workingDirectory.Path, baseDirectory.Path));

        File.WriteAllText(basePath, "{}");
        Assert.Equal(basePath, SelfDiagnosticsConfigParser.LocateConfigFile(workingDirectory.Path, baseDirectory.Path));

        File.WriteAllText(workingPath, "{}");
        Assert.Equal(workingPath, SelfDiagnosticsConfigParser.LocateConfigFile(workingDirectory.Path, baseDirectory.Path));
    }

    [Fact]
    public void TryGetConfiguration_PreservesPermissiveParsing()
    {
        var path = Path.Combine(this.directory.Path, SelfDiagnosticsConfigParser.ConfigFileName);
        var text = """
            "logDirectory": "Diagnostics"
            "fileSize": 1024
            "logLevel": "Warning"
            trailing content
            """;
        File.WriteAllText(path, text);
        var parser = new SelfDiagnosticsConfigParser(() => path);

        Assert.True(parser.TryGetConfiguration(out _), "Expected the parser to successfully get the configuration.");
    }

    [Fact]
    public void TryGetConfiguration_ReadsAtMostFourKibibytes()
    {
        const int ConfigBufferSize = 4096;
        var path = Path.Combine(this.directory.Path, SelfDiagnosticsConfigParser.ConfigFileName);

        // Permissive parsing does not need the closing brace, so the final quote of LogLevel is the
        // last byte required for success.
        var text = "{\"LogDirectory\":\"Diagnostics\",\"FileSize\":1024,\"LogLevel\":\"Warning\"";
        var parser = new SelfDiagnosticsConfigParser(() => path);

        File.WriteAllText(path, new string(' ', ConfigBufferSize - text.Length) + text);
        Assert.True(parser.TryGetConfiguration(out _), "Expected the parser to succeed when the configuration ends at the last byte read.");

        File.WriteAllText(path, new string(' ', ConfigBufferSize - text.Length + 1) + text);
        Assert.False(parser.TryGetConfiguration(out _), "Expected the parser to fail when the configuration ends one byte past the read limit.");
    }

    [Fact]
    public void TryGetConfiguration_MissingOrUnavailableFile_ReturnsFalse()
    {
        Assert.False(new SelfDiagnosticsConfigParser(() => null).TryGetConfiguration(out _), "Expected the parser to fail when the file path is null.");
        Assert.False(new SelfDiagnosticsConfigParser(() => Path.Combine(this.directory.Path, "missing.json")).TryGetConfiguration(out _), "Expected the parser to fail when the file is missing.");
        Assert.False(new SelfDiagnosticsConfigParser(() => this.directory.Path).TryGetConfiguration(out _), "Expected the parser to fail when the path is a directory.");
        Assert.False(new SelfDiagnosticsConfigParser(() => throw new IOException("Unavailable.")).TryGetConfiguration(out _), "Expected the parser to fail when an IOException is thrown.");
    }

    [Fact]
    public void SelfDiagnosticsConfigParser_TryParseFilePath_Success()
    {
        var configJson = "{ \t \n "
                            + "\t    \"LogDirectory\" \t : \"Diagnostics\", \n"
                            + "FileSize \t : \t \n"
                            + " 1024 \n}\n";
        Assert.True(SelfDiagnosticsConfigParser.TryParseLogDirectory(configJson, out var logDirectory), "Expected the parser to successfully parse the log directory.");
        Assert.Equal("Diagnostics", logDirectory);
    }

    [Fact]
    public void SelfDiagnosticsConfigParser_TryParseFilePath_MissingField()
    {
        var configJson = @"{
                    ""path"": ""Diagnostics"",
                    ""FileSize"": 1024
                    }";
        Assert.False(SelfDiagnosticsConfigParser.TryParseLogDirectory(configJson, out _), "Expected the parser to fail when the log directory field is missing.");
    }

    [Fact]
    public void SelfDiagnosticsConfigParser_TryParseFileSize()
    {
        var configJson = @"{
                    ""LogDirectory"": ""Diagnostics"",
                    ""FileSize"": 1024
                    }";
        Assert.True(SelfDiagnosticsConfigParser.TryParseFileSize(configJson, out var fileSize), "Expected the parser to successfully parse the file size.");
        Assert.Equal(1024, fileSize);
    }

    [Fact]
    public void SelfDiagnosticsConfigParser_TryParseFileSize_CaseInsensitive()
    {
        var configJson = @"{
                    ""LogDirectory"": ""Diagnostics"",
                    ""fileSize"" :
                                   2048
                    }";
        Assert.True(SelfDiagnosticsConfigParser.TryParseFileSize(configJson, out var fileSize), "Expected the parser to successfully parse the file size in a case-insensitive manner.");
        Assert.Equal(2048, fileSize);
    }

    [Fact]
    public void SelfDiagnosticsConfigParser_TryParseFileSize_MissingField()
    {
        var configJson = @"{
                    ""LogDirectory"": ""Diagnostics"",
                    ""size"": 1024
                    }";
        Assert.False(SelfDiagnosticsConfigParser.TryParseFileSize(configJson, out _), "Expected the parser to fail when the file size field is missing.");
    }

    [Fact]
    public void SelfDiagnosticsConfigParser_TryParseLogLevel()
    {
        var configJson = @"{
                    ""LogDirectory"": ""Diagnostics"",
                    ""FileSize"": 1024,
                    ""LogLevel"": ""Error""
                    }";
        Assert.True(SelfDiagnosticsConfigParser.TryParseLogLevel(configJson, out var logLevelString), "Expected the parser to successfully parse the log level.");
        Assert.Equal("Error", logLevelString);
    }
}

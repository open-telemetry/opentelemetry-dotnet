// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace OpenTelemetry.Internal.Tests;

public class SelfDiagnosticsConfigParserTests
{
    [Fact]
    public void SelfDiagnosticsConfigParser_TryParseFilePath_Success()
    {
        var configJson = "{ \t \n "
                            + "\t    \"LogDirectory\" \t : \"Diagnostics\", \n"
                            + "FileSize \t : \t \n"
                            + " 1024 \n}\n";
        Assert.True(SelfDiagnosticsConfigParser.TryParseLogDirectory(configJson, out var logDirectory));
        Assert.Equal("Diagnostics", logDirectory);
    }

    [Fact]
    public void SelfDiagnosticsConfigParser_TryParseFilePath_MissingField()
    {
        var configJson = @"{
                    ""path"": ""Diagnostics"",
                    ""FileSize"": 1024
                    }";
        Assert.False(SelfDiagnosticsConfigParser.TryParseLogDirectory(configJson, out _));
    }

    [Fact]
    public void SelfDiagnosticsConfigParser_TryParseFileSize()
    {
        var configJson = @"{
                    ""LogDirectory"": ""Diagnostics"",
                    ""FileSize"": 1024
                    }";
        Assert.True(SelfDiagnosticsConfigParser.TryParseFileSize(configJson, out var fileSize));
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
        Assert.True(SelfDiagnosticsConfigParser.TryParseFileSize(configJson, out var fileSize));
        Assert.Equal(2048, fileSize);
    }

    [Fact]
    public void SelfDiagnosticsConfigParser_TryParseFileSize_MissingField()
    {
        var configJson = @"{
                    ""LogDirectory"": ""Diagnostics"",
                    ""size"": 1024
                    }";
        Assert.False(SelfDiagnosticsConfigParser.TryParseFileSize(configJson, out _));
    }

    [Fact]
    public void SelfDiagnosticsConfigParser_TryParseLogLevel()
    {
        var configJson = @"{
                    ""LogDirectory"": ""Diagnostics"",
                    ""FileSize"": 1024,
                    ""LogLevel"": ""Error""
                    }";
        Assert.True(SelfDiagnosticsConfigParser.TryParseLogLevel(configJson, out var logLevelString));
        Assert.Equal("Error", logLevelString);
    }

    [Fact]
    public void SelfDiagnosticsConfigParser_TryParseFormatMessage_Success()
    {
        var configJson = """
            {
                "LogDirectory": "Diagnostics",
                "FileSize": 1024,
                "LogLevel": "Error",
                "FormatMessage": "true"
            }
            """;
        Assert.True(SelfDiagnosticsConfigParser.TryParseFormatMessage(configJson, out var formatMessage));
        Assert.True(formatMessage);
    }

    [Fact]
    public void SelfDiagnosticsConfigParser_TryParseFormatMessage_CaseInsensitive()
    {
        var configJson = """
            {
                "LogDirectory": "Diagnostics",
                "fileSize": 1024,
                "formatMessage": "FALSE"
            }
            """;
        Assert.True(SelfDiagnosticsConfigParser.TryParseFormatMessage(configJson, out var formatMessage));
        Assert.False(formatMessage);
    }

    [Fact]
    public void SelfDiagnosticsConfigParser_TryParseFormatMessage_MissingField()
    {
        var configJson = """
            {
                "LogDirectory": "Diagnostics",
                "FileSize": 1024,
                "LogLevel": "Error"
            }
            """;
        Assert.True(SelfDiagnosticsConfigParser.TryParseFormatMessage(configJson, out var formatMessage));
        Assert.False(formatMessage); // Should default to false
    }

    [Fact]
    public void SelfDiagnosticsConfigParser_TryParseFormatMessage_InvalidValue()
    {
        var configJson = """
            {
                "LogDirectory": "Diagnostics",
                "FileSize": 1024,
                "LogLevel": "Error",
                "FormatMessage": "invalid"
            }
            """;
        Assert.False(SelfDiagnosticsConfigParser.TryParseFormatMessage(configJson, out var formatMessage));
        Assert.False(formatMessage); // Should default to false
    }

    [Fact]
    public void SelfDiagnosticsConfigParser_TryParseFormatMessage_UnquotedBoolean()
    {
        var configJson = """
            {
                "LogDirectory": "Diagnostics",
                "FileSize": 1024,
                "LogLevel": "Error",
                "FormatMessage": true
            }
            """;
        Assert.True(SelfDiagnosticsConfigParser.TryParseFormatMessage(configJson, out var formatMessage));
        Assert.True(formatMessage);
    }
}

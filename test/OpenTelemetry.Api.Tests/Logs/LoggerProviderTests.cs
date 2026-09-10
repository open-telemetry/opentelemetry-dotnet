// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;

namespace OpenTelemetry.Logs.Tests;

public sealed class LoggerProviderTests
{
    [Fact]
    public void NoopLoggerReturnedTest()
    {
        using var provider = new NoopLoggerProvider();

        var logger = provider.GetLogger(name: "TestLogger", version: "Version");

        Assert.NotNull(logger);
        Assert.Equal(typeof(NoopLogger), logger.GetType());

        Assert.Equal(string.Empty, logger.Name);
        Assert.Null(logger.Version);
        Assert.Null(logger.SchemaUrl);
    }

    [Fact]
    public void LoggerReturnedWithInstrumentationScopeTest()
    {
        using var provider = new TestLoggerProvider();

        var logger = provider.GetLogger(name: "TestLogger", version: "Version");

        Assert.NotNull(logger);
        Assert.Equal(typeof(TestLogger), logger.GetType());

        Assert.Equal("TestLogger", logger.Name);
        Assert.Equal("Version", logger.Version);
        Assert.Null(logger.SchemaUrl);

        logger = provider.GetLogger(name: "TestLogger");

        Assert.NotNull(logger);
        Assert.Equal(typeof(TestLogger), logger.GetType());

        Assert.Equal("TestLogger", logger.Name);
        Assert.Null(logger.Version);
        Assert.Null(logger.SchemaUrl);

        logger = provider.GetLogger();

        Assert.NotNull(logger);
        Assert.Equal(typeof(TestLogger), logger.GetType());

        Assert.Equal(string.Empty, logger.Name);
        Assert.Null(logger.Version);
        Assert.Null(logger.SchemaUrl);
    }

    [Fact]
    public void LoggerReturnedWithInstrumentationScopeIncludingSchemaUrlTest()
    {
        using var provider = new TestLoggerProvider();

        var options = new LoggerOptions()
        {
            Name = "TestLogger",
            Version = "Version",
            SchemaUrl = "https://example.com/schema",
        };

        var logger = provider.GetLogger(options);

        Assert.NotNull(logger);
        Assert.Equal(typeof(TestLogger), logger.GetType());

        Assert.Equal("TestLogger", logger.Name);
        Assert.Equal("Version", logger.Version);
        Assert.Equal("https://example.com/schema", logger.SchemaUrl);
    }

    private sealed class NoopLoggerProvider : LoggerProvider;

    private sealed class TestLoggerProvider : LoggerProvider
    {
#if OPENTELEMETRY_API_EXPERIMENTAL_FEATURES_EXPOSED
        protected override bool TryCreateLogger(
#else
        internal override bool TryCreateLogger(
#endif
            LoggerOptions options,
            [NotNullWhen(true)]
            out Logger? logger)
        {
            logger = new TestLogger(options.Name);
            logger.SetInstrumentationScope(options.Version, options.SchemaUrl);
            return true;
        }
    }

    private sealed class TestLogger : Logger
    {
        public TestLogger(string? name)
            : base(name)
        {
        }

        public override void EmitLog(in LogRecordData data, in LogRecordAttributeList attributes)
        {
        }
    }
}

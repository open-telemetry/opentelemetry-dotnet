// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETSTANDARD2_1_OR_GREATER || NET
using System.Diagnostics.CodeAnalysis;
#endif
using Xunit;

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

        logger = provider.GetLogger(name: "TestLogger");

        Assert.NotNull(logger);
        Assert.Equal(typeof(TestLogger), logger.GetType());

        Assert.Equal("TestLogger", logger.Name);
        Assert.Null(logger.Version);

        logger = provider.GetLogger();

        Assert.NotNull(logger);
        Assert.Equal(typeof(TestLogger), logger.GetType());

        Assert.Equal(string.Empty, logger.Name);
        Assert.Null(logger.Version);
    }

    private sealed class NoopLoggerProvider : LoggerProvider
    {
    }

    private sealed class TestLoggerProvider : LoggerProvider
    {
#if OPENTELEMETRY_API_EXPERIMENTAL_FEATURES_EXPOSED
        protected override bool TryCreateLogger(
#else
        internal override bool TryCreateLogger(
#endif
            string? name,
#if NETSTANDARD2_1_OR_GREATER || NET
            [NotNullWhen(true)]
#endif
            out Logger? logger)
        {
            logger = new TestLogger(name);
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

// <copyright file="LoggerProviderTests.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

#nullable enable

#if NETSTANDARD2_1_OR_GREATER || NET6_0_OR_GREATER
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
        protected override bool TryCreateLogger(
            string? name,
#if NETSTANDARD2_1_OR_GREATER || NET6_0_OR_GREATER
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

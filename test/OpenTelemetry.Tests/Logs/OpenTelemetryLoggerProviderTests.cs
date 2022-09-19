// <copyright file="OpenTelemetryLoggerProviderTests.cs" company="OpenTelemetry Authors">
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

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using Xunit;

namespace OpenTelemetry.Logs.Tests
{
    public sealed class OpenTelemetryLoggerProviderTests
    {
        [Fact]
        public void OptionsCtorTests()
        {
            OpenTelemetryLoggerOptions defaults = new();

            using OpenTelemetryLoggerProvider openTelemetryLoggerProvider = new(new TestOptions(new()));

            Assert.Equal(defaults.IncludeAttributes, openTelemetryLoggerProvider.IncludeAttributes);
            Assert.Equal(defaults.IncludeTraceState, openTelemetryLoggerProvider.IncludeTraceState);
            Assert.Equal(defaults.IncludeScopes, openTelemetryLoggerProvider.IncludeScopes);
            Assert.Equal(defaults.IncludeFormattedMessage, openTelemetryLoggerProvider.IncludeFormattedMessage);
            Assert.Equal(defaults.ParseStateValues, openTelemetryLoggerProvider.ParseStateValues);

            var provider = openTelemetryLoggerProvider.Provider as LoggerProviderSdk;

            Assert.NotNull(provider);

            Assert.Null(provider.Processor);
            Assert.NotNull(provider.Resource);
        }

        [Fact]
        public void OptionsCtorWithConfigurationTest()
        {
            OpenTelemetryLoggerOptions defaults = new();

            var options = new OpenTelemetryLoggerOptions
            {
                IncludeAttributes = !defaults.IncludeAttributes,
                IncludeTraceState = !defaults.IncludeTraceState,
                IncludeScopes = !defaults.IncludeScopes,
                IncludeFormattedMessage = !defaults.IncludeFormattedMessage,
                ParseStateValues = !defaults.ParseStateValues,
            };

            options
                .SetResourceBuilder(ResourceBuilder
                    .CreateEmpty()
                    .AddAttributes(new[] { new KeyValuePair<string, object>("key1", "value1") }))
                .AddInMemoryExporter(new List<LogRecord>());

            using OpenTelemetryLoggerProvider openTelemetryLoggerProvider = new(new TestOptions(options));

            Assert.Equal(!defaults.IncludeAttributes, openTelemetryLoggerProvider.IncludeAttributes);
            Assert.Equal(!defaults.IncludeTraceState, openTelemetryLoggerProvider.IncludeTraceState);
            Assert.Equal(!defaults.IncludeScopes, openTelemetryLoggerProvider.IncludeScopes);
            Assert.Equal(!defaults.IncludeFormattedMessage, openTelemetryLoggerProvider.IncludeFormattedMessage);
            Assert.Equal(!defaults.ParseStateValues, openTelemetryLoggerProvider.ParseStateValues);

            var provider = openTelemetryLoggerProvider.Provider as LoggerProviderSdk;

            Assert.NotNull(provider);

            Assert.NotNull(provider.Processor);
            Assert.NotNull(provider.Resource);
            Assert.Contains(provider.Resource.Attributes, value => value.Key == "key1" && (string)value.Value == "value1");
        }

        private sealed class TestOptions : IOptionsMonitor<OpenTelemetryLoggerOptions>
        {
            private readonly OpenTelemetryLoggerOptions options;

            public TestOptions(OpenTelemetryLoggerOptions options)
            {
                this.options = options;
            }

            public OpenTelemetryLoggerOptions CurrentValue => this.options;

            public OpenTelemetryLoggerOptions Get(string name) => this.options;

            public IDisposable OnChange(Action<OpenTelemetryLoggerOptions, string> listener)
            {
                throw new NotImplementedException();
            }
        }
    }
}

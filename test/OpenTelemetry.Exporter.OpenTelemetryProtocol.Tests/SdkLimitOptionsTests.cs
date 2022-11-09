// <copyright file="SdkLimitOptionsTests.cs" company="OpenTelemetry Authors">
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
using Microsoft.Extensions.Configuration;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using Xunit;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests
{
    public sealed class SdkLimitOptionsTests : IDisposable
    {
        public SdkLimitOptionsTests()
        {
            ClearEnvVars();
        }

        public void Dispose()
        {
            ClearEnvVars();
        }

        [Fact]
        public void SdkLimitOptionsDefaults()
        {
            var config = new SdkLimitOptions();

            Assert.Null(config.AttributeValueLengthLimit);
            Assert.Null(config.AttributeCountLimit);
            Assert.Null(config.SpanAttributeValueLengthLimit);
            Assert.Null(config.SpanAttributeCountLimit);
            Assert.Null(config.SpanEventCountLimit);
            Assert.Null(config.SpanLinkCountLimit);
            Assert.Null(config.SpanEventAttributeCountLimit);
            Assert.Null(config.SpanLinkAttributeCountLimit);
        }

        [Fact]
        public void SdkLimitOptionsIsInitializedFromEnvironment()
        {
            Environment.SetEnvironmentVariable("OTEL_ATTRIBUTE_VALUE_LENGTH_LIMIT", "10");
            Environment.SetEnvironmentVariable("OTEL_ATTRIBUTE_COUNT_LIMIT", "10");
            Environment.SetEnvironmentVariable("OTEL_SPAN_ATTRIBUTE_VALUE_LENGTH_LIMIT", "20");
            Environment.SetEnvironmentVariable("OTEL_SPAN_ATTRIBUTE_COUNT_LIMIT", "20");
            Environment.SetEnvironmentVariable("OTEL_SPAN_EVENT_COUNT_LIMIT", "10");
            Environment.SetEnvironmentVariable("OTEL_SPAN_LINK_COUNT_LIMIT", "10");
            Environment.SetEnvironmentVariable("OTEL_EVENT_ATTRIBUTE_COUNT_LIMIT", "30");
            Environment.SetEnvironmentVariable("OTEL_LINK_ATTRIBUTE_COUNT_LIMIT", "30");

            var config = new SdkLimitOptions();

            Assert.Equal(10, config.AttributeValueLengthLimit);
            Assert.Equal(10, config.AttributeCountLimit);
            Assert.Equal(20, config.SpanAttributeValueLengthLimit);
            Assert.Equal(20, config.SpanAttributeCountLimit);
            Assert.Equal(10, config.SpanEventCountLimit);
            Assert.Equal(10, config.SpanLinkCountLimit);
            Assert.Equal(30, config.SpanEventAttributeCountLimit);
            Assert.Equal(30, config.SpanLinkAttributeCountLimit);
        }

        [Fact]
        public void SpanAttributeValueLengthLimitFallback()
        {
            var config = new SdkLimitOptions();

            config.AttributeValueLengthLimit = 10;
            Assert.Equal(10, config.AttributeValueLengthLimit);
            Assert.Equal(10, config.SpanAttributeValueLengthLimit);

            config.SpanAttributeValueLengthLimit = 20;
            Assert.Equal(10, config.AttributeValueLengthLimit);
            Assert.Equal(20, config.SpanAttributeValueLengthLimit);
        }

        [Fact]
        public void SpanAttributeCountLimitFallback()
        {
            var config = new SdkLimitOptions();

            config.AttributeCountLimit = 10;
            Assert.Equal(10, config.AttributeCountLimit);
            Assert.Equal(10, config.SpanAttributeCountLimit);
            Assert.Equal(10, config.SpanEventAttributeCountLimit);
            Assert.Equal(10, config.SpanLinkAttributeCountLimit);

            config.SpanAttributeCountLimit = 20;
            Assert.Equal(10, config.AttributeCountLimit);
            Assert.Equal(20, config.SpanAttributeCountLimit);
            Assert.Equal(20, config.SpanEventAttributeCountLimit);
            Assert.Equal(20, config.SpanLinkAttributeCountLimit);

            config.SpanEventAttributeCountLimit = 30;
            Assert.Equal(10, config.AttributeCountLimit);
            Assert.Equal(20, config.SpanAttributeCountLimit);
            Assert.Equal(30, config.SpanEventAttributeCountLimit);
            Assert.Equal(20, config.SpanLinkAttributeCountLimit);

            config.SpanLinkAttributeCountLimit = 40;
            Assert.Equal(10, config.AttributeCountLimit);
            Assert.Equal(20, config.SpanAttributeCountLimit);
            Assert.Equal(30, config.SpanEventAttributeCountLimit);
            Assert.Equal(40, config.SpanLinkAttributeCountLimit);
        }

        [Fact]
        public void SdkLimitOptionsUsingIConfiguration()
        {
            var values = new Dictionary<string, string>()
            {
                ["OTEL_ATTRIBUTE_VALUE_LENGTH_LIMIT"] = "23",
                ["OTEL_ATTRIBUTE_COUNT_LIMIT"] = "24",
                ["OTEL_SPAN_ATTRIBUTE_VALUE_LENGTH_LIMIT"] = "25",
                ["OTEL_SPAN_ATTRIBUTE_COUNT_LIMIT"] = "26",
                ["OTEL_SPAN_EVENT_COUNT_LIMIT"] = "27",
                ["OTEL_SPAN_LINK_COUNT_LIMIT"] = "28",
                ["OTEL_EVENT_ATTRIBUTE_COUNT_LIMIT"] = "29",
                ["OTEL_LINK_ATTRIBUTE_COUNT_LIMIT"] = "30",
            };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(values)
                .Build();

            var options = new SdkLimitOptions(configuration);

            Assert.Equal(23, options.AttributeValueLengthLimit);
            Assert.Equal(24, options.AttributeCountLimit);
            Assert.Equal(25, options.SpanAttributeValueLengthLimit);
            Assert.Equal(26, options.SpanAttributeCountLimit);
            Assert.Equal(27, options.SpanEventCountLimit);
            Assert.Equal(28, options.SpanLinkCountLimit);
            Assert.Equal(29, options.SpanEventAttributeCountLimit);
            Assert.Equal(30, options.SpanLinkAttributeCountLimit);
        }

        private static void ClearEnvVars()
        {
            Environment.SetEnvironmentVariable("OTEL_ATTRIBUTE_VALUE_LENGTH_LIMIT", null);
            Environment.SetEnvironmentVariable("OTEL_ATTRIBUTE_COUNT_LIMIT", null);
            Environment.SetEnvironmentVariable("OTEL_SPAN_ATTRIBUTE_VALUE_LENGTH_LIMIT", null);
            Environment.SetEnvironmentVariable("OTEL_SPAN_ATTRIBUTE_COUNT_LIMIT", null);
            Environment.SetEnvironmentVariable("OTEL_SPAN_EVENT_COUNT_LIMIT", null);
            Environment.SetEnvironmentVariable("OTEL_SPAN_LINK_COUNT_LIMIT", null);
            Environment.SetEnvironmentVariable("OTEL_EVENT_ATTRIBUTE_COUNT_LIMIT", null);
            Environment.SetEnvironmentVariable("OTEL_LINK_ATTRIBUTE_COUNT_LIMIT", null);
        }
    }
}

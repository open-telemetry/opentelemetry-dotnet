// <copyright file="SdkConfigurationTests.cs" company="OpenTelemetry Authors">
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
using Xunit;

namespace OpenTelemetry.Configuration.Tests
{
    [Collection("xUnitCollectionPreventingTestsThatDependOnSdkConfigurationFromRunningInParallel")]
    public class SdkConfigurationTests : IDisposable
    {
        public SdkConfigurationTests()
        {
            ClearEnvVars();
            SdkConfiguration.Reset();
        }

        public void Dispose()
        {
            ClearEnvVars();
            SdkConfiguration.Reset();
        }

        [Fact]
        public void SdkConfigurationDefaults()
        {
            var config = SdkConfiguration.Instance;

            Assert.Null(config.AttributeValueLengthLimit);
            Assert.Null(config.AttributeCountLimit);
            Assert.Null(config.SpanAttributeValueLengthLimit);
            Assert.Null(config.SpanAttributeCountLimit);
            Assert.Null(config.SpanEventCountLimit);
            Assert.Null(config.SpanLinkCountLimit);
            Assert.Null(config.EventAttributeCountLimit);
            Assert.Null(config.LinkAttributeCountLimit);
        }

        [Fact]
        public void SdkConfigurationIsInitializedFromEnvironment()
        {
            Environment.SetEnvironmentVariable("OTEL_ATTRIBUTE_VALUE_LENGTH_LIMIT", "10");
            Environment.SetEnvironmentVariable("OTEL_ATTRIBUTE_COUNT_LIMIT", "10");
            Environment.SetEnvironmentVariable("OTEL_SPAN_ATTRIBUTE_VALUE_LENGTH_LIMIT", "20");
            Environment.SetEnvironmentVariable("OTEL_SPAN_ATTRIBUTE_COUNT_LIMIT", "20");
            Environment.SetEnvironmentVariable("OTEL_SPAN_EVENT_COUNT_LIMIT", "10");
            Environment.SetEnvironmentVariable("OTEL_SPAN_LINK_COUNT_LIMIT", "10");
            Environment.SetEnvironmentVariable("OTEL_EVENT_ATTRIBUTE_COUNT_LIMIT", "30");
            Environment.SetEnvironmentVariable("OTEL_LINK_ATTRIBUTE_COUNT_LIMIT", "30");

            SdkConfiguration.Reset();
            var config = SdkConfiguration.Instance;

            Assert.Equal(10, config.AttributeValueLengthLimit);
            Assert.Equal(10, config.AttributeCountLimit);
            Assert.Equal(20, config.SpanAttributeValueLengthLimit);
            Assert.Equal(20, config.SpanAttributeCountLimit);
            Assert.Equal(10, config.SpanEventCountLimit);
            Assert.Equal(10, config.SpanLinkCountLimit);
            Assert.Equal(30, config.EventAttributeCountLimit);
            Assert.Equal(30, config.LinkAttributeCountLimit);
        }

        [Fact]
        public void SpanAttributeValueLengthLimitFallback()
        {
            var config = SdkConfiguration.Instance;

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
            var config = SdkConfiguration.Instance;

            config.AttributeCountLimit = 10;
            Assert.Equal(10, config.AttributeCountLimit);
            Assert.Equal(10, config.SpanAttributeCountLimit);
            Assert.Equal(10, config.EventAttributeCountLimit);
            Assert.Equal(10, config.LinkAttributeCountLimit);

            config.SpanAttributeCountLimit = 20;
            Assert.Equal(10, config.AttributeCountLimit);
            Assert.Equal(20, config.SpanAttributeCountLimit);
            Assert.Equal(20, config.EventAttributeCountLimit);
            Assert.Equal(20, config.LinkAttributeCountLimit);

            config.EventAttributeCountLimit = 30;
            Assert.Equal(10, config.AttributeCountLimit);
            Assert.Equal(20, config.SpanAttributeCountLimit);
            Assert.Equal(30, config.EventAttributeCountLimit);
            Assert.Equal(20, config.LinkAttributeCountLimit);

            config.LinkAttributeCountLimit = 40;
            Assert.Equal(10, config.AttributeCountLimit);
            Assert.Equal(20, config.SpanAttributeCountLimit);
            Assert.Equal(30, config.EventAttributeCountLimit);
            Assert.Equal(40, config.LinkAttributeCountLimit);
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

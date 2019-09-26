// <copyright file="StackdriverStatsConfigurationTests.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
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

using System.Diagnostics;
using System.Net;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Config;
using OpenTelemetry.Trace.Export;

namespace OpenTelemetry.Exporter.Stackdriver.Tests
{
    using OpenTelemetry.Exporter.Stackdriver.Implementation;
    using System;
    using Xunit;

    public class StackdriverExporterTests
    {
        [Fact]
        public void Export_Span_Format()
        {
            var span = new SpanBuilder("test-span", new SimpleSpanProcessor(new NoopSpanExporter()),
                TraceConfig.Default).StartSpan();
            span.PutHttpHostAttribute("http://example.com", 80);
            span.PutHttpMethodAttribute("POST");
            span.PutHttpPathAttribute("path");
            span.PutHttpRawUrlAttribute("https://example.com?q=*");
            span.PutHttpRequestSizeAttribute(long.MinValue);
            span.PutHttpResponseSizeAttribute(long.MaxValue);
            span.PutHttpRouteAttribute("route");
            span.PutHttpStatusCode((int)HttpStatusCode.Accepted, "Accepted");
            span.PutHttpUserAgentAttribute("google");

            var convertedSpan = SpanExtensions.ToSpan(span as Span, "test-project");
            Assert.NotNull(convertedSpan);
            Assert.True(convertedSpan.Attributes.AttributeMap.ContainsKey("/http/host"));
            Assert.True(convertedSpan.Attributes.AttributeMap.ContainsKey("/http/method"));
            Assert.True(convertedSpan.Attributes.AttributeMap.ContainsKey("/http/status_code"));
            Assert.True(convertedSpan.Attributes.AttributeMap.ContainsKey("/http/route"));
            Assert.True(convertedSpan.Attributes.AttributeMap.ContainsKey("/http/user_agent"));
            Assert.True(convertedSpan.Attributes.AttributeMap.ContainsKey("/http/request_size"));
            Assert.True(convertedSpan.Attributes.AttributeMap.ContainsKey("/http/response_size"));
            Assert.True(convertedSpan.Attributes.AttributeMap.ContainsKey("/http/url"));
            Assert.True(convertedSpan.Attributes.AttributeMap.ContainsKey("/http/path"));
        }
    }
}

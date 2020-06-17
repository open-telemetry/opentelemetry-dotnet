// <copyright file="Defaults.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics;
using Moq;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Shims.OpenTracing.Tests
{
    /// <summary>
    /// A set of static helper methods for creating some default test setup objects.
    /// </summary>
    internal static class Defaults
    {
        public static SpanContext GetOpenTelemetrySpanContext()
        {
            return new SpanContext(
                ActivityTraceId.CreateRandom(),
                ActivitySpanId.CreateRandom(),
                ActivityTraceFlags.None);
        }

        /// <summary>
        /// Creates an instance of SpanMock from this test project. This is a basic implementation used to verify calls to add events, links, and attributes.
        /// </summary>
        /// <returns>an instance of Span.</returns>
        public static SpanMock GetOpenTelemetrySpanMock()
        {
            return new SpanMock(GetOpenTelemetrySpanContext());
        }

        /// <summary>
        /// Gets a mock implementation of OpenTelemetry TelemetrySpan suitable for certain kinds of unit tests.
        /// </summary>
        /// <returns>a mock of the OpenTelemetry ISpan.</returns>
        public static Mock<TelemetrySpan> GetOpenTelemetryMockSpan()
        {
            var spanContext = GetOpenTelemetrySpanContext();
            var mock = new Mock<TelemetrySpan>();
            mock.Setup(o => o.Context).Returns(spanContext);
            return mock;
        }
    }
}

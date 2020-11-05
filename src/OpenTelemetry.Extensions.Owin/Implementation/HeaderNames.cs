// <copyright file="HeaderNames.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Implementation
{
    // // Microsoft.Net.Http.Headers headers (from https://github.com/dotnet/aspnetcore/blob/v5.0.0-rc.2.20475.17/src/Http/Headers/src/HeaderNames.cs)
    internal static class HeaderNames
    {
        public static readonly string CorrelationContext = "Correlation-Context";
        public static readonly string RequestId = "Request-Id";
        public static readonly string TraceParent = "traceparent";
        public static readonly string TraceState = "tracestate";
    }
}

// <copyright file="SpanAttributeConstants.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace
{
    internal static class SpanAttributeConstants
    {
        public static readonly string ComponentKey = "component";

        public static readonly string HttpMethodKey = "http.method";
        public static readonly string HttpStatusCodeKey = "http.status_code";
        public static readonly string HttpUserAgentKey = "http.user_agent";
        public static readonly string HttpPathKey = "http.path";
        public static readonly string HttpHostKey = "http.host";
        public static readonly string HttpUrlKey = "http.url";
        public static readonly string HttpRouteKey = "http.route";
        public static readonly string HttpFlavorKey = "http.flavor";
    }
}

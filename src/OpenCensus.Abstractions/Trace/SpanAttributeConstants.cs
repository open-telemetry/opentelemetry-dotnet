// <copyright file="SpanAttributeConstants.cs" company="OpenCensus Authors">
// Copyright 2018, OpenCensus Authors
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

namespace OpenCensus.Trace
{
    internal static class SpanAttributeConstants
    {
        public const string HttpMethodKey = "http.method";
        public const string HttpStatusCodeKey = "http.status_code";
        public const string HttpUserAgentKey = "http.user_agent";
        public const string HttpPathKey = "http.path";
        public const string HttpHostKey = "http.host";
        public const string HttpUrlKey = "http.url";
        public const string HttpRequestSizeKey = "http.request.size";
        public const string HttpResponseSizeKey = "http.response.size";
        public const string HttpRouteKey = "http.route";
    }
}

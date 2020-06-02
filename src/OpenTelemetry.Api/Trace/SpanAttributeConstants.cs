// <copyright file="SpanAttributeConstants.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Defines well-known span attribute keys.
    /// </summary>
    public static class SpanAttributeConstants
    {
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public const string ComponentKey = "component";
        public const string PeerServiceKey = "peer.service";

        public const string StatusCodeKey = "ot.status_code";
        public const string StatusDescriptionKey = "ot.status_description";

        public const string HttpMethodKey = "http.method";
        public const string HttpSchemeKey = "http.scheme";
        public const string HttpTargetKey = "http.target";
        public const string HttpStatusCodeKey = "http.status_code";
        public const string HttpStatusTextKey = "http.status_text";
        public const string HttpUserAgentKey = "http.user_agent";
        public const string HttpPathKey = "http.path";
        public const string HttpHostKey = "http.host";
        public const string HttpUrlKey = "http.url";
        public const string HttpRouteKey = "http.route";
        public const string HttpFlavorKey = "http.flavor";

        public const string DatabaseTypeKey = "db.type";
        public const string DatabaseInstanceKey = "db.instance";
        public const string DatabaseStatementKey = "db.statement";
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    }
}

// <copyright file="GrpcTagHelper.cs" company="OpenTelemetry Authors">
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
using System.Linq;
using System.Text.RegularExpressions;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.Dependencies
{
    internal static class GrpcTagHelper
    {
        // The Grpc.Net.Client library adds its own tags to the activity.
        // These tags are used to source the tags added by the OpenTelemetry instrumentation.
        public const string GrpcMethodTagName = "grpc.method";
        public const string GrpcStatusCodeTagName = "grpc.status_code";

        private static readonly Regex GrpcMethodRegex = new Regex(@"^/?(?<service>.*)/(?<method>.*)$", RegexOptions.Compiled);

        public static string GetGrpcMethodFromActivity(Activity activity)
        {
            return activity.Tags.FirstOrDefault(tag => tag.Key == GrpcMethodTagName).Value;
        }

        public static Status GetGrpcStatusCodeFromActivity(Activity activity)
        {
            var status = Status.Unknown;

            var grpcStatusCodeTag = activity.Tags.FirstOrDefault(tag => tag.Key == GrpcStatusCodeTagName).Value;
            if (int.TryParse(grpcStatusCodeTag, out var statusCode))
            {
                status = SpanHelper.ResolveSpanStatusForGrpcStatusCode(statusCode);
            }

            return status;
        }

        public static bool TryParseRpcServiceAndRpcMethod(string grpcMethod, out string rpcService, out string rpcMethod)
        {
            var match = GrpcMethodRegex.Match(grpcMethod);
            if (match.Success)
            {
                rpcService = match.Groups["service"].Value;
                rpcMethod = match.Groups["method"].Value;
                return true;
            }
            else
            {
                rpcService = string.Empty;
                rpcMethod = string.Empty;
                return false;
            }
        }
    }
}

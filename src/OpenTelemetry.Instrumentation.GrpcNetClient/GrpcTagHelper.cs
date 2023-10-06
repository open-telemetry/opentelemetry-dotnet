// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text.RegularExpressions;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.GrpcNetClient;

internal static class GrpcTagHelper
{
    public const string RpcSystemGrpc = "grpc";

    // The Grpc.Net.Client library adds its own tags to the activity.
    // These tags are used to source the tags added by the OpenTelemetry instrumentation.
    public const string GrpcMethodTagName = "grpc.method";
    public const string GrpcStatusCodeTagName = "grpc.status_code";

    private static readonly Regex GrpcMethodRegex = new(@"^/?(?<service>.*)/(?<method>.*)$", RegexOptions.Compiled);

    public static string GetGrpcMethodFromActivity(Activity activity)
    {
        return activity.GetTagValue(GrpcMethodTagName) as string;
    }

    public static bool TryGetGrpcStatusCodeFromActivity(Activity activity, out int statusCode)
    {
        statusCode = -1;
        var grpcStatusCodeTag = activity.GetTagValue(GrpcStatusCodeTagName);
        if (grpcStatusCodeTag == null)
        {
            return false;
        }

        return int.TryParse(grpcStatusCodeTag as string, out statusCode);
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

    /// <summary>
    /// Helper method that populates span properties from RPC status code according
    /// to https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/semantic_conventions/rpc.md#status.
    /// </summary>
    /// <param name="statusCode">RPC status code.</param>
    /// <returns>Resolved span <see cref="Status"/> for the Grpc status code.</returns>
    public static ActivityStatusCode ResolveSpanStatusForGrpcStatusCode(int statusCode)
    {
        var status = ActivityStatusCode.Error;

        if (typeof(StatusCanonicalCode).IsEnumDefined(statusCode))
        {
            status = ((StatusCanonicalCode)statusCode) switch
            {
                StatusCanonicalCode.Ok => ActivityStatusCode.Unset,
                _ => ActivityStatusCode.Error,
            };
        }

        return status;
    }
}

// <copyright file="GrpcClientDiagnosticListener.cs" company="OpenTelemetry Authors">
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
#if NET6_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif
using System.Reflection;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Instrumentation.Http;
using OpenTelemetry.Trace;
using static OpenTelemetry.Internal.HttpSemanticConventionHelper;

namespace OpenTelemetry.Instrumentation.GrpcNetClient.Implementation;

internal sealed class GrpcClientDiagnosticListener : ListenerHandler
{
    internal static readonly AssemblyName AssemblyName = typeof(GrpcClientDiagnosticListener).Assembly.GetName();
    internal static readonly string ActivitySourceName = AssemblyName.Name;
    internal static readonly Version Version = AssemblyName.Version;
    internal static readonly ActivitySource ActivitySource = new(ActivitySourceName, Version.ToString());

    private const string OnStartEvent = "Grpc.Net.Client.GrpcOut.Start";
    private const string OnStopEvent = "Grpc.Net.Client.GrpcOut.Stop";

    private static readonly PropertyFetcher<HttpRequestMessage> StartRequestFetcher = new("Request");
    private static readonly PropertyFetcher<HttpResponseMessage> StopResponseFetcher = new("Response");

    private readonly GrpcClientInstrumentationOptions options;
    private readonly bool emitOldAttributes;
    private readonly bool emitNewAttributes;

    public GrpcClientDiagnosticListener(GrpcClientInstrumentationOptions options)
        : base("Grpc.Net.Client")
    {
        this.options = options;

        this.emitOldAttributes = this.options.HttpSemanticConvention.HasFlag(HttpSemanticConvention.Old);

        this.emitNewAttributes = this.options.HttpSemanticConvention.HasFlag(HttpSemanticConvention.New);
    }

    public override void OnEventWritten(string name, object payload)
    {
        switch (name)
        {
            case OnStartEvent:
                {
                    this.OnStartActivity(Activity.Current, payload);
                }

                break;
            case OnStopEvent:
                {
                    this.OnStopActivity(Activity.Current, payload);
                }

                break;
        }
    }

    public void OnStartActivity(Activity activity, object payload)
    {
        // The overall flow of what GrpcClient library does is as below:
        // Activity.Start()
        // DiagnosticSource.WriteEvent("Start", payload)
        // DiagnosticSource.WriteEvent("Stop", payload)
        // Activity.Stop()

        // This method is in the WriteEvent("Start", payload) path.
        // By this time, samplers have already run and
        // activity.IsAllDataRequested populated accordingly.

        if (Sdk.SuppressInstrumentation)
        {
            return;
        }

        // Ensure context propagation irrespective of sampling decision
        if (!TryFetchRequest(payload, out HttpRequestMessage request))
        {
            GrpcInstrumentationEventSource.Log.NullPayload(nameof(GrpcClientDiagnosticListener), nameof(this.OnStartActivity));
            return;
        }

        var textMapPropagator = Propagators.DefaultTextMapPropagator;
        textMapPropagator.Inject(
            new PropagationContext(activity.Context, Baggage.Current),
            request,
            HttpRequestMessageContextPropagation.HeaderValueSetter);

        if (activity.IsAllDataRequested)
        {
            ActivityInstrumentationHelper.SetActivitySourceProperty(activity, ActivitySource);
            ActivityInstrumentationHelper.SetKindProperty(activity, ActivityKind.Client);

            var grpcMethod = GrpcTagHelper.GetGrpcMethodFromActivity(activity);

            activity.DisplayName = grpcMethod?.Trim('/');

            activity.SetTag(SemanticConventions.AttributeRpcSystem, GrpcTagHelper.RpcSystemGrpc);

            if (GrpcTagHelper.TryParseRpcServiceAndRpcMethod(grpcMethod, out var rpcService, out var rpcMethod))
            {
                activity.SetTag(SemanticConventions.AttributeRpcService, rpcService);
                activity.SetTag(SemanticConventions.AttributeRpcMethod, rpcMethod);

                // Remove the grpc.method tag added by the gRPC .NET library
                activity.SetTag(GrpcTagHelper.GrpcMethodTagName, null);
            }

            var uriHostNameType = Uri.CheckHostName(request.RequestUri.Host);
            if (this.emitOldAttributes)
            {
                if (uriHostNameType == UriHostNameType.IPv4 || uriHostNameType == UriHostNameType.IPv6)
                {
                    activity.SetTag(SemanticConventions.AttributeNetPeerIp, request.RequestUri.Host);
                }
                else
                {
                    activity.SetTag(SemanticConventions.AttributeNetPeerName, request.RequestUri.Host);
                }

                activity.SetTag(SemanticConventions.AttributeNetPeerPort, request.RequestUri.Port);
            }

            // see the spec https://github.com/open-telemetry/semantic-conventions/blob/v1.21.0/docs/http/http-spans.md
            if (this.emitNewAttributes)
            {
                if (uriHostNameType == UriHostNameType.IPv4 || uriHostNameType == UriHostNameType.IPv6)
                {
                    activity.SetTag(SemanticConventions.AttributeServerSocketAddress, request.RequestUri.Host);
                }
                else
                {
                    activity.SetTag(SemanticConventions.AttributeServerAddress, request.RequestUri.Host);
                }

                activity.SetTag(SemanticConventions.AttributeServerPort, request.RequestUri.Port);
            }

            try
            {
                this.options.EnrichWithHttpRequestMessage?.Invoke(activity, request);
            }
            catch (Exception ex)
            {
                GrpcInstrumentationEventSource.Log.EnrichmentException(ex);
            }
        }

        // See https://github.com/grpc/grpc-dotnet/blob/ff1a07b90c498f259e6d9f4a50cdad7c89ecd3c0/src/Grpc.Net.Client/Internal/GrpcCall.cs#L1180-L1183
        // this makes sure that top-level properties on the payload object are always preserved.
#if NET6_0_OR_GREATER
        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The event source guarantees that top level properties are preserved")]
#endif
        static bool TryFetchRequest(object payload, out HttpRequestMessage request)
            => StartRequestFetcher.TryFetch(payload, out request) && request != null;
    }

    public void OnStopActivity(Activity activity, object payload)
    {
        if (activity.IsAllDataRequested)
        {
            bool validConversion = GrpcTagHelper.TryGetGrpcStatusCodeFromActivity(activity, out int status);
            if (validConversion)
            {
                if (activity.Status == ActivityStatusCode.Unset)
                {
                    activity.SetStatus(GrpcTagHelper.ResolveSpanStatusForGrpcStatusCode(status));
                }

                // setting rpc.grpc.status_code
                activity.SetTag(SemanticConventions.AttributeRpcGrpcStatusCode, status);
            }

            // Remove the grpc.status_code tag added by the gRPC .NET library
            activity.SetTag(GrpcTagHelper.GrpcStatusCodeTagName, null);

            if (TryFetchResponse(payload, out HttpResponseMessage response))
            {
                try
                {
                    this.options.EnrichWithHttpResponseMessage?.Invoke(activity, response);
                }
                catch (Exception ex)
                {
                    GrpcInstrumentationEventSource.Log.EnrichmentException(ex);
                }
            }
        }

        // See https://github.com/grpc/grpc-dotnet/blob/ff1a07b90c498f259e6d9f4a50cdad7c89ecd3c0/src/Grpc.Net.Client/Internal/GrpcCall.cs#L1180-L1183
        // this makes sure that top-level properties on the payload object are always preserved.
#if NET6_0_OR_GREATER
        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The event source guarantees that top level properties are preserved")]
#endif
        static bool TryFetchResponse(object payload, out HttpResponseMessage response)
            => StopResponseFetcher.TryFetch(payload, out response) && response != null;
    }
}

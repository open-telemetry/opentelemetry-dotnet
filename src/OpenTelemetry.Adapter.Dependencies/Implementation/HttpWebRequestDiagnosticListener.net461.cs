// <copyright file="HttpWebRequestDiagnosticListener.net461.cs" company="OpenTelemetry Authors">
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
#if NET461
using System;
using System.Diagnostics;
using System.Net;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Adapter.Dependencies.Implementation
{
    internal class HttpWebRequestDiagnosticListener : ListenerHandler
    {
        private readonly PropertyFetcher startRequestFetcher = new PropertyFetcher("Request");
        private readonly PropertyFetcher stopResponseFetcher = new PropertyFetcher("Response");
        private readonly PropertyFetcher stopExceptionFetcher = new PropertyFetcher("Exception");
        private readonly HttpClientAdapterOptions options;

        public HttpWebRequestDiagnosticListener(Tracer tracer, HttpClientAdapterOptions options)
            : base(HttpWebRequestDiagnosticSource.DiagnosticListenerName, tracer)
        {
            this.options = options;
        }

        public override void OnStartActivity(Activity activity, object payload)
        {
            const string EventNameSuffix = ".OnStartActivity";
            if (!(this.startRequestFetcher.Fetch(payload) is HttpWebRequest request))
            {
                AdapterEventSource.Log.NullPayload(nameof(HttpWebRequestDiagnosticListener) + EventNameSuffix);
                return;
            }

            this.Tracer.StartActiveSpanFromActivity(request.RequestUri.AbsolutePath, activity, SpanKind.Client, out var span);

            if (span.IsRecording)
            {
                span.PutComponentAttribute("http");
                span.PutHttpMethodAttribute(request.Method.ToString());
                span.PutHttpHostAttribute(request.RequestUri.Host, request.RequestUri.Port);
                span.PutHttpRawUrlAttribute(request.RequestUri.OriginalString);

                if (this.options.SetHttpFlavor)
                {
                    span.PutHttpFlavorAttribute(request.ProtocolVersion.ToString());
                }
            }

            if (!(this.options.TextFormat is TraceContextFormat))
            {
                this.options.TextFormat.Inject<HttpWebRequest>(span.Context, request, (r, k, v) => r.Headers.Add(k, v));
            }
        }

        public override void OnStopActivity(Activity activity, object payload)
        {
            const string EventNameSuffix = ".OnStopActivity";
            var span = this.Tracer.CurrentSpan;
            try
            {
                if (span == null || !span.Context.IsValid)
                {
                    AdapterEventSource.Log.NullOrBlankSpan(nameof(HttpWebRequestDiagnosticListener) + EventNameSuffix);
                    return;
                }

                if (span.IsRecording)
                {
                    if (this.stopResponseFetcher.Fetch(payload) is HttpWebResponse response)
                    {
                        span.PutHttpStatusCode((int)response.StatusCode, response.StatusDescription);
                    }
                }
            }
            finally
            {
                span?.End();
            }
        }

        public override void OnException(Activity activity, object payload)
        {
            const string EventNameSuffix = ".OnException";
            var span = this.Tracer.CurrentSpan;
            try
            {
                if (span == null || !span.Context.IsValid)
                {
                    AdapterEventSource.Log.NullOrBlankSpan(nameof(HttpWebRequestDiagnosticListener) + EventNameSuffix);
                    return;
                }

                if (span.IsRecording)
                {
                    if (!(this.stopExceptionFetcher.Fetch(payload) is Exception exc))
                    {
                        AdapterEventSource.Log.NullPayload(nameof(HttpWebRequestDiagnosticListener) + EventNameSuffix);
                        return;
                    }

                    ProcessException(span, exc);
                }
            }
            finally
            {
                span?.End();
            }
        }

        private static void ProcessException(TelemetrySpan span, Exception exception)
        {
            if (exception is WebException wexc)
            {
                if (wexc.Response is HttpWebResponse response)
                {
                    span.PutHttpStatusCode((int)response.StatusCode, response.StatusDescription);
                    return;
                }

                switch (wexc.Status)
                {
                    case WebExceptionStatus.Timeout:
                        span.Status = Status.DeadlineExceeded;
                        return;
                    case WebExceptionStatus.NameResolutionFailure:
                        span.Status = Status.InvalidArgument.WithDescription(exception.Message);
                        return;
                    case WebExceptionStatus.SendFailure:
                    case WebExceptionStatus.ConnectFailure:
                    case WebExceptionStatus.SecureChannelFailure:
                    case WebExceptionStatus.TrustFailure:
                        span.Status = Status.FailedPrecondition.WithDescription(exception.Message);
                        return;
                    case WebExceptionStatus.ServerProtocolViolation:
                        span.Status = Status.Unimplemented.WithDescription(exception.Message);
                        return;
                    case WebExceptionStatus.RequestCanceled:
                        span.Status = Status.Cancelled;
                        return;
                    case WebExceptionStatus.MessageLengthLimitExceeded:
                        span.Status = Status.ResourceExhausted.WithDescription(exception.Message);
                        return;
                }
            }

            span.Status = Status.Unknown.WithDescription(exception.Message);
        }
    }
}
#endif

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Instrumentation.Http;

namespace System.Net.Http
{
    /// <summary>
    /// DiagnosticHandler notifies DiagnosticSource subscribers about outgoing Http requests
    /// </summary>
    public sealed class DiagnosticsHandler : DelegatingHandler
    {
        internal static bool IsEnabled()
        {
            // check if there is a parent Activity (and propagation is not suppressed)
            // or if someone listens to HttpHandlerDiagnosticListener
            return IsGloballyEnabled() && (Activity.Current != null || Settings.s_diagnosticListener.IsEnabled());
        }

        internal static bool IsGloballyEnabled()
        {
            return Settings.s_activityPropagationEnabled;
        }

#if NET5_0_OR_GREATER
        // SendAsyncCore returns already completed ValueTask for when async: false is passed.
        // Internally, it calls the synchronous Send method of the base class.
        protected override HttpResponseMessage Send(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            ValueTask<HttpResponseMessage> sendTask = this.SendAsyncCore(async: false, request, cancellationToken);
            Debug.Assert(sendTask.IsCompleted, "Task should completed");
            return sendTask.GetAwaiter().GetResult();
        }

        /// <inheritdoc/>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            this.SendAsyncCore(async: true, request, cancellationToken).AsTask();

        private async ValueTask<HttpResponseMessage> SendAsyncCore(
            bool async,
#else
        /// <inheritdoc/>
        protected override async Task<HttpResponseMessage> SendAsync(
#endif
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            // HttpClientHandler is responsible to call static DiagnosticsHandler.IsEnabled() before forwarding request here.
            // It will check if propagation is on (because parent Activity exists or there is a listener) or off (forcibly disabled)
            // This code won't be called unless consumer unsubscribes from DiagnosticListener right after the check.
            // So some requests happening right after subscription starts might not be instrumented. Similarly,
            // when consumer unsubscribes, extra requests might be instrumented

            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (HasDiagnosticsHandler(this.InnerHandler))
            {
                return
#if NET5_0_OR_GREATER
                    !async
                        ? base.Send(request, cancellationToken)
                        :
#endif
                        await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }

            Activity activity = null;
            DiagnosticListener diagnosticListener = Settings.s_diagnosticListener;

            // if there is no listener, but propagation is enabled (with previous IsEnabled() check)
            // do not write any events just start/stop Activity and propagate Ids
            if (!IsEnabled() &&
                Propagators.DefaultTextMapPropagator.Extract(default, request, HttpRequestMessageContextPropagation.HeaderValuesGetter) == default)
            {
                activity = new Activity(DiagnosticsHandlerLoggingStrings.ActivityName);
                activity.Start();

                try
                {
                    return
#if NET5_0_OR_GREATER
                        !async
                            ? base.Send(request, cancellationToken)
                            :
#endif
                            await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    activity.Stop();
                }
            }

            Guid loggingRequestId = Guid.Empty;

            // There is a listener. Check if listener wants to be notified about HttpClient Activities
            if (diagnosticListener.IsEnabled(DiagnosticsHandlerLoggingStrings.ActivityName, request))
            {
                activity = new Activity(DiagnosticsHandlerLoggingStrings.ActivityName);

                // Only send start event to users who subscribed for it, but start activity anyway
                if (diagnosticListener.IsEnabled(DiagnosticsHandlerLoggingStrings.ActivityStartName))
                {
                    diagnosticListener.StartActivity(activity, new ActivityStartData(request));
                }
                else
                {
                    activity.Start();
                }
            }

            // try to write System.Net.Http.Request event (deprecated)
            if (diagnosticListener.IsEnabled(DiagnosticsHandlerLoggingStrings.RequestWriteNameDeprecated))
            {
                long timestamp = Stopwatch.GetTimestamp();
                loggingRequestId = Guid.NewGuid();
                diagnosticListener.Write(
                    DiagnosticsHandlerLoggingStrings.RequestWriteNameDeprecated,
                    new RequestData(request, loggingRequestId, timestamp));
            }

            HttpResponseMessage response = null;
            TaskStatus taskStatus = TaskStatus.RanToCompletion;
            try
            {
                return response =
#if NET5_0_OR_GREATER
                    !async
                        ? base.Send(request, cancellationToken)
                        :
#endif
                        await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                taskStatus = TaskStatus.Canceled;

                // we'll report task status in HttpRequestOut.Stop
                throw;
            }
            catch (Exception ex)
            {
                taskStatus = TaskStatus.Faulted;

                if (diagnosticListener.IsEnabled(DiagnosticsHandlerLoggingStrings.ExceptionEventName))
                {
                    // If request was initially instrumented, Activity.Current has all necessary context for logging
                    // Request is passed to provide some context if instrumentation was disabled and to avoid
                    // extensive Activity.Tags usage to tunnel request properties
                    diagnosticListener.Write(DiagnosticsHandlerLoggingStrings.ExceptionEventName, new ExceptionData(ex, request));
                }
                throw;
            }
            finally
            {
                // always stop activity if it was started
                if (activity != null)
                {
                    diagnosticListener.StopActivity(activity, new ActivityStopData(
                        response,
                        // If request is failed or cancelled, there is no response, therefore no information about request;
                        // pass the request in the payload, so consumers can have it in Stop for failed/canceled requests
                        // and not retain all requests in Start
                        request,
                        taskStatus));
                }
                // Try to write System.Net.Http.Response event (deprecated)
                if (diagnosticListener.IsEnabled(DiagnosticsHandlerLoggingStrings.ResponseWriteNameDeprecated))
                {
                    long timestamp = Stopwatch.GetTimestamp();
                    diagnosticListener.Write(DiagnosticsHandlerLoggingStrings.ResponseWriteNameDeprecated,
                        new ResponseData(
                            response,
                            loggingRequestId,
                            timestamp,
                            taskStatus));
                }
            }
        }

        #region private

        private sealed class ActivityStartData
        {
            internal ActivityStartData(HttpRequestMessage request)
            {
                this.Request = request;
            }

            public HttpRequestMessage Request { get; }

            public override string ToString() => $"{{ {nameof(this.Request)} = {this.Request} }}";
        }

        private sealed class ActivityStopData
        {
            internal ActivityStopData(HttpResponseMessage response, HttpRequestMessage request, TaskStatus requestTaskStatus)
            {
                this.Response = response;
                this.Request = request;
                this.RequestTaskStatus = requestTaskStatus;
            }

            public HttpResponseMessage Response { get; }

            public HttpRequestMessage Request { get; }

            public TaskStatus RequestTaskStatus { get; }

            public override string ToString() => $"{{ {nameof(this.Response)} = {this.Response}, {nameof(this.Request)} = {this.Request}, {nameof(this.RequestTaskStatus)} = {this.RequestTaskStatus} }}";
        }

        private sealed class ExceptionData
        {
            internal ExceptionData(Exception exception, HttpRequestMessage request)
            {
                this.Exception = exception;
                this.Request = request;
            }

            public Exception Exception { get; }
            public HttpRequestMessage Request { get; }

            public override string ToString() => $"{{ {nameof(this.Exception)} = {this.Exception}, {nameof(this.Request)} = {this.Request} }}";
        }

        private sealed class RequestData
        {
            internal RequestData(HttpRequestMessage request, Guid loggingRequestId, long timestamp)
            {
                this.Request = request;
                this.LoggingRequestId = loggingRequestId;
                this.Timestamp = timestamp;
            }

            public HttpRequestMessage Request { get; }
            public Guid LoggingRequestId { get; }
            public long Timestamp { get; }

            public override string ToString() => $"{{ {nameof(this.Request)} = {this.Request}, {nameof(this.LoggingRequestId)} = {this.LoggingRequestId}, {nameof(this.Timestamp)} = {this.Timestamp} }}";
        }

        private sealed class ResponseData
        {
            internal ResponseData(HttpResponseMessage response, Guid loggingRequestId, long timestamp, TaskStatus requestTaskStatus)
            {
                this.Response = response;
                this.LoggingRequestId = loggingRequestId;
                this.Timestamp = timestamp;
                this.RequestTaskStatus = requestTaskStatus;
            }

            public HttpResponseMessage Response { get; }

            public Guid LoggingRequestId { get; }

            public long Timestamp { get; }

            public TaskStatus RequestTaskStatus { get; }

            public override string ToString() => $"{{ {nameof(this.Response)} = {this.Response}, {nameof(this.LoggingRequestId)} = {this.LoggingRequestId}, {nameof(this.Timestamp)} = {this.Timestamp}, {nameof(this.RequestTaskStatus)} = {this.RequestTaskStatus} }}";
        }

        private static class Settings
        {
            private const string EnableActivityPropagationEnvironmentVariableSettingName = "DOTNET_SYSTEM_NET_HTTP_ENABLEACTIVITYPROPAGATION";
            private const string EnableActivityPropagationAppCtxSettingName = "System.Net.Http.EnableActivityPropagation";

            public static readonly bool s_activityPropagationEnabled = GetEnableActivityPropagationValue();

            private static bool GetEnableActivityPropagationValue()
            {
                // First check for the AppContext switch, giving it priority over the environment variable.
                if (AppContext.TryGetSwitch(EnableActivityPropagationAppCtxSettingName, out bool enableActivityPropagation))
                {
                    return enableActivityPropagation;
                }

                // AppContext switch wasn't used. Check the environment variable to determine which handler should be used.
                string envVar = Environment.GetEnvironmentVariable(EnableActivityPropagationEnvironmentVariableSettingName);
                if (envVar != null && (envVar.Equals("false", StringComparison.OrdinalIgnoreCase) || envVar.Equals("0")))
                {
                    // Suppress Activity propagation.
                    return false;
                }

                // Defaults to enabling Activity propagation.
                return true;
            }

            public static readonly DiagnosticListener s_diagnosticListener =
                new DiagnosticListener(DiagnosticsHandlerLoggingStrings.DiagnosticListenerName);
        }

        #endregion

        private static bool HasDiagnosticsHandler(HttpMessageHandler handler)
        {
            while (handler != null)
            {
                switch (handler)
                {
                    // https://github.com/grpc/grpc-dotnet/blob/master/src/Shared/TelemetryHeaderHandler.cs
                    case DelegatingHandler dh when dh.GetType().FullName == "Grpc.Shared.TelemetryHeaderHandler":
                        return true;
                    case DelegatingHandler dh:
                        handler = dh.InnerHandler;
                        break;
                    case HttpClientHandler _:
                        return true;
                    default:
                        return false;
                }
            }

            return false;
        }
    }
}

// <copyright file="DiagnosticsMiddleware.cs" company="OpenTelemetry Authors">
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

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Owin;
using OpenTelemetry.Implementation;

namespace OpenTelemetry
{
    /// <summary>
    /// Instruments incoming request with System.Diagnostics.Activity and notifies listeners with DiagnosticsSource.
    /// </summary>
    public sealed class DiagnosticsMiddleware : OwinMiddleware
    {
        private const string ActivityName = "OpenTelemetry.Extensions.Owin.HttpRequestIn";
        private const string ActivityStartKey = ActivityName + ".Start";
        private const string ActivityStopKey = ActivityName + ".Stop";

        private readonly DiagnosticListener diagnosticListener = new DiagnosticListener("OpenTelemetry.Extensions.Owin");
        private readonly Context context = new Context();

        /// <summary>
        /// Initializes a new instance of the <see cref="DiagnosticsMiddleware"/> class.
        /// </summary>
        /// <param name="next">An optional pointer to the next component</param>
        public DiagnosticsMiddleware(OwinMiddleware next)
            : base(next)
        {
        }

        /// <inheritdoc />
        public override async Task Invoke(IOwinContext owinContext)
        {
            try
            {
                this.BeginRequest(owinContext);
                await this.Next.Invoke(owinContext).ConfigureAwait(false);
                this.RequestEnd(owinContext, null);
            }
            catch (Exception ex)
            {
                this.RequestEnd(owinContext, ex);
            }
        }

        // Based on https://github.com/dotnet/aspnetcore/blob/v5.0.0-rc.2.20475.17/src/Hosting/Hosting/src/Internal/HostingApplicationDiagnostics.cs#L37
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void BeginRequest(IOwinContext owinContext)
        {
            if (OwinExtensionsEventSource.Log.IsEnabled())
            {
                this.context.EventLogEnabled = true;
            }

            if (this.diagnosticListener.IsEnabled() && this.diagnosticListener.IsEnabled(ActivityName, owinContext))
            {
                this.context.Activity = this.StartActivity(owinContext, out var hasDiagnosticListener);
                this.context.HasDiagnosticListener = hasDiagnosticListener;
            }
        }

        // Based on https://github.com/dotnet/aspnetcore/blob/v5.0.0-rc.2.20475.17/src/Hosting/Hosting/src/Internal/HostingApplicationDiagnostics.cs#L89
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RequestEnd(IOwinContext owinContext, Exception exception)
        {
            var activity = this.context.Activity;
            // Always stop activity if it was started
            if (activity != null)
            {
                this.StopActivity(owinContext, activity, this.context.HasDiagnosticListener);
            }

            if (this.context.EventLogEnabled && exception != null)
            {
                // Non-inline
                OwinExtensionsEventSource.Log.UnhandledException();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private Activity StartActivity(IOwinContext owinContext, out bool hasDiagnosticListener)
        {
            hasDiagnosticListener = false;

            var activity = new Activity(ActivityName);

            // Based on https://github.com/microsoft/ApplicationInsights-dotnet/blob/2.15.0/WEB/Src/Web/Web/Implementation/RequestTrackingExtensions.cs#L41
            if (!activity.Extract(owinContext.Request.Headers))
            {
                // Force parsing Correlation-Context in absence of Request-Id or traceparent.
                owinContext.Request.Headers.ReadActivityBaggage(activity);
            }

            this.diagnosticListener.OnActivityImport(activity, owinContext);

            if (this.diagnosticListener.IsEnabled(ActivityStartKey))
            {
                hasDiagnosticListener = true;
                this.StartActivity(activity, owinContext);
            }
            else
            {
                activity.Start();
            }

            return activity;
        }

        // These are versions of DiagnosticSource.Start/StopActivity that don't allocate strings per call (see https://github.com/dotnet/corefx/issues/37055)
        private void StartActivity(Activity activity, IOwinContext owinContext)
        {
            activity.Start();
            this.diagnosticListener.Write(ActivityStartKey, owinContext);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void StopActivity(IOwinContext owinContext, Activity activity, bool hasDiagnosticListener)
        {
            if (hasDiagnosticListener)
            {
                this.StopActivity(activity, owinContext);
            }
            else
            {
                activity.Stop();
            }
        }

        private void StopActivity(Activity activity, IOwinContext owinContext)
        {
            // Stop sets the end time if it was unset, but we want it set before we issue the write
            // so we do it now.
            if (activity.Duration == TimeSpan.Zero)
            {
                activity.SetEndTime(DateTime.UtcNow);
            }

            this.diagnosticListener.Write(ActivityStopKey, owinContext);
            activity.Stop();    // Resets Activity.Current (we want this after the Write)
        }
    }
}

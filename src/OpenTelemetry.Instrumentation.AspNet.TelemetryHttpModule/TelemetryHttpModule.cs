// <copyright file="TelemetryHttpModule.cs" company="OpenTelemetry Authors">
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
using System.Reflection;
using System.Web;

namespace OpenTelemetry.Instrumentation.AspNet
{
    /// <summary>
    /// Http Module sets ambient state using Activity API from DiagnosticsSource package.
    /// </summary>
    public class TelemetryHttpModule : IHttpModule
    {
        /// <summary>
        /// OpenTelemetry.Instrumentation.AspNet <see cref="ActivitySource"/> name.
        /// </summary>
        public const string AspNetSourceName = "OpenTelemetry.Instrumentation.AspNet.Telemetry";

        /// <summary>
        /// <see cref="Activity.OperationName"/> for OpenTelemetry.Instrumentation.AspNet created <see cref="Activity"/> objects.
        /// </summary>
        public const string AspNetActivityName = "Microsoft.AspNet.HttpReqIn";

        // ServerVariable set only on rewritten HttpContext by URL Rewrite module.
        private const string URLRewriteRewrittenRequest = "IIS_WasUrlRewritten";

        // ServerVariable set on every request if URL module is registered in HttpModule pipeline.
        private const string URLRewriteModuleVersion = "IIS_UrlRewriteModule";

        private static readonly MethodInfo OnExecuteRequestStepMethodInfo = typeof(HttpApplication).GetMethod("OnExecuteRequestStep");

        /// <summary>
        /// Gets the <see cref="TelemetryHttpModuleOptions"/> applied to requests processed by the handler.
        /// </summary>
        public static TelemetryHttpModuleOptions Options { get; } = new TelemetryHttpModuleOptions();

        /// <inheritdoc />
        public void Dispose()
        {
        }

        /// <inheritdoc />
        public void Init(HttpApplication context)
        {
            context.BeginRequest += this.Application_BeginRequest;
            context.EndRequest += this.Application_EndRequest;
            context.Error += this.Application_Error;

            if (HttpRuntime.UsingIntegratedPipeline && OnExecuteRequestStepMethodInfo != null)
            {
                // OnExecuteRequestStep is availabile starting with 4.7.1
                try
                {
                    OnExecuteRequestStepMethodInfo.Invoke(context, new object[] { (Action<HttpContextBase, Action>)this.OnExecuteRequestStep });
                }
                catch (Exception e)
                {
                    AspNetTelemetryEventSource.Log.OnExecuteRequestStepInvokationError(e.Message);
                }
            }
        }

        private void Application_BeginRequest(object sender, EventArgs e)
        {
            AspNetTelemetryEventSource.Log.TraceCallback("Application_BeginRequest");
            ActivityHelper.StartAspNetActivity(Options.TextMapPropagator, ((HttpApplication)sender).Context, Options.OnRequestStartedCallback);
        }

        private void OnExecuteRequestStep(HttpContextBase context, Action step)
        {
            // Called only on 4.7.1+ runtimes

            if (context.CurrentNotification == RequestNotification.ExecuteRequestHandler && !context.IsPostNotification)
            {
                ActivityHelper.RestoreContextIfNeeded(context.ApplicationInstance.Context);
            }

            step();
        }

        private void Application_EndRequest(object sender, EventArgs e)
        {
            AspNetTelemetryEventSource.Log.TraceCallback("Application_EndRequest");
            bool trackActivity = true;

            var context = ((HttpApplication)sender).Context;

            if (!ActivityHelper.HasStarted(context, out Activity aspNetActivity))
            {
                // Rewrite: In case of rewrite, a new request context is created, called the child request, and it goes through the entire IIS/ASP.NET integrated pipeline.
                // The child request can be mapped to any of the handlers configured in IIS, and it's execution is no different than it would be if it was received via the HTTP stack.
                // The parent request jumps ahead in the pipeline to the end request notification, and waits for the child request to complete.
                // When the child request completes, the parent request executes the end request notifications and completes itself.
                // Do not create activity for parent request. Parent request has IIS_UrlRewriteModule ServerVariable with success response code.
                // Child request contains an additional ServerVariable named - IIS_WasUrlRewritten.
                // Track failed response activity: Different modules in the pipleline has ability to end the response. For example, authentication module could set HTTP 401 in OnBeginRequest and end the response.
                if (context.Request.ServerVariables != null && context.Request.ServerVariables[URLRewriteRewrittenRequest] == null && context.Request.ServerVariables[URLRewriteModuleVersion] != null && context.Response.StatusCode == 200)
                {
                    trackActivity = false;
                }
                else
                {
                    // Activity has never been started
                    aspNetActivity = ActivityHelper.StartAspNetActivity(Options.TextMapPropagator, context, Options.OnRequestStartedCallback);
                }
            }

            if (trackActivity)
            {
                ActivityHelper.StopAspNetActivity(Options.TextMapPropagator, aspNetActivity, context, Options.OnRequestStoppedCallback);
            }
        }

        private void Application_Error(object sender, EventArgs e)
        {
            AspNetTelemetryEventSource.Log.TraceCallback("Application_Error");

            var context = ((HttpApplication)sender).Context;

            var exception = context.Error;
            if (exception != null)
            {
                if (!ActivityHelper.HasStarted(context, out Activity aspNetActivity))
                {
                    aspNetActivity = ActivityHelper.StartAspNetActivity(Options.TextMapPropagator, context, Options.OnRequestStartedCallback);
                }

                ActivityHelper.WriteActivityException(aspNetActivity, context, exception, Options.OnExceptionCallback);
            }
        }
    }
}

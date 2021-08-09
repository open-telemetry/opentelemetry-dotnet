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
using System.ComponentModel;
using System.Reflection;
using System.Web;

namespace OpenTelemetry.Instrumentation.AspNet
{
    /// <summary>
    /// Http Module sets ambient state using Activity API from DiagnosticsSource package.
    /// </summary>
    public class TelemetryHttpModule : IHttpModule
    {
        private const string BeginCalledFlag = "OpenTelemetry.Instrumentation.AspNet.BeginCalled";

        // ServerVariable set only on rewritten HttpContext by URL Rewrite module.
        private const string URLRewriteRewrittenRequest = "IIS_WasUrlRewritten";

        // ServerVariable set on every request if URL module is registered in HttpModule pipeline.
        private const string URLRewriteModuleVersion = "IIS_UrlRewriteModule";

        private static readonly MethodInfo OnStepMethodInfo = typeof(HttpApplication).GetMethod("OnExecuteRequestStep");

        /// <summary>
        /// Gets or sets a value indicating whether TelemetryHttpModule should parse headers to get correlation ids.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool ParseHeaders { get; set; } = true;

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

            // OnExecuteRequestStep is availabile starting with 4.7.1
            // If this is executed in 4.7.1 runtime (regardless of targeted .NET version),
            // we will use it to restore lost activity, otherwise keep PreRequestHandlerExecute
            if (OnStepMethodInfo != null && HttpRuntime.UsingIntegratedPipeline)
            {
                try
                {
                    OnStepMethodInfo.Invoke(context, new object[] { (Action<HttpContextBase, Action>)this.OnExecuteRequestStep });
                }
                catch (Exception e)
                {
                    AspNetTelemetryEventSource.Log.OnExecuteRequestStepInvokationError(e.Message);
                }
            }
            else
            {
                context.PreRequestHandlerExecute += this.Application_PreRequestHandlerExecute;
            }
        }

        /// <summary>
        /// Restores Activity before each pipeline step if it was lost.
        /// </summary>
        /// <param name="context">HttpContext instance.</param>
        /// <param name="step">Step to be executed.</param>
        internal void OnExecuteRequestStep(HttpContextBase context, Action step)
        {
            // Once we have public Activity.Current setter (https://github.com/dotnet/corefx/issues/29207) this method will be
            // simplified to just assign Current if is was lost.
            // In the mean time, we are creating child Activity to restore the context. We have to send
            // event with this Activity to tracing system. It created a lot of issues for listeners as
            // we may potentially have a lot of them for different stages.
            // To reduce amount of events, we only care about ExecuteRequestHandler stage - restore activity here and
            // stop/report it to tracing system in EndRequest.
            if (context.CurrentNotification == RequestNotification.ExecuteRequestHandler && !context.IsPostNotification)
            {
                ActivityHelper.RestoreActivityIfNeeded(context.Items);
            }

            step();
        }

        private void Application_BeginRequest(object sender, EventArgs e)
        {
            var context = ((HttpApplication)sender).Context;
            AspNetTelemetryEventSource.Log.TraceCallback("Application_BeginRequest");
            ActivityHelper.CreateRootActivity(context, this.ParseHeaders);
            context.Items[BeginCalledFlag] = true;
        }

        private void Application_PreRequestHandlerExecute(object sender, EventArgs e)
        {
            AspNetTelemetryEventSource.Log.TraceCallback("Application_PreRequestHandlerExecute");
            ActivityHelper.RestoreActivityIfNeeded(((HttpApplication)sender).Context.Items);
        }

        private void Application_EndRequest(object sender, EventArgs e)
        {
            AspNetTelemetryEventSource.Log.TraceCallback("Application_EndRequest");
            bool trackActivity = true;

            var context = ((HttpApplication)sender).Context;

            // EndRequest does it's best effort to notify that request has ended
            // BeginRequest has never been called
            if (!context.Items.Contains(BeginCalledFlag))
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
                    ActivityHelper.CreateRootActivity(context, this.ParseHeaders);
                }
            }

            if (trackActivity)
            {
                ActivityHelper.StopAspNetActivity(context.Items);
            }
        }

        private void Application_Error(object sender, EventArgs e)
        {
            AspNetTelemetryEventSource.Log.TraceCallback("Application_Error");

            var context = ((HttpApplication)sender).Context;

            var exception = context.Error;
            if (exception != null)
            {
                if (!context.Items.Contains(BeginCalledFlag))
                {
                    ActivityHelper.CreateRootActivity(context, this.ParseHeaders);
                }

                ActivityHelper.WriteActivityException(context.Items, exception);
            }
        }
    }
}

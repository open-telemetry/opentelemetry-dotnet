// <copyright file="ActivityHelper.cs" company="OpenTelemetry Authors">
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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Web;
using OpenTelemetry.Context.Propagation;

namespace OpenTelemetry.Instrumentation.AspNet
{
    /// <summary>
    /// Activity helper class.
    /// </summary>
    internal static class ActivityHelper
    {
        /// <summary>
        /// Key to store the activity in HttpContext.
        /// </summary>
        internal const string ActivityKey = "__AspnetActivity__";

        internal static readonly object StartedButNotSampledObj = new object();
        private static readonly Func<HttpRequest, string, IEnumerable<string>> HttpRequestHeaderValuesGetter = (request, name) => request.Headers.GetValues(name);
        private static readonly ActivitySource AspNetSource = new ActivitySource(
            TelemetryHttpModule.AspNetSourceName,
            typeof(ActivityHelper).Assembly.GetName().Version.ToString());

        /// <summary>
        /// Try to get the started <see cref="Activity"/> for the running <see
        /// cref="HttpContext"/>.
        /// </summary>
        /// <param name="context"><see cref="HttpContext"/>.</param>
        /// <param name="aspNetActivity">Started <see cref="Activity"/> or <see
        /// langword="null"/> if 1) start has not been called or 2) start was
        /// called but sampling decided not to create an instance.</param>
        /// <returns><see langword="true"/> if start has been called.</returns>
        public static bool HasStarted(HttpContext context, out Activity aspNetActivity)
        {
            Debug.Assert(context != null, "Context is null.");

            object itemValue = context.Items[ActivityKey];
            if (itemValue is Activity activity)
            {
                aspNetActivity = activity;
                return true;
            }

            aspNetActivity = null;
            return itemValue == StartedButNotSampledObj;
        }

        /// <summary>
        /// Creates root (first level) activity that describes incoming request.
        /// </summary>
        /// <param name="textMapPropagator"><see cref="TextMapPropagator"/>.</param>
        /// <param name="context"><see cref="HttpContext"/>.</param>
        /// <param name="onRequestStartedCallback">Callback action.</param>
        /// <returns>New root activity.</returns>
        public static Activity StartAspNetActivity(TextMapPropagator textMapPropagator, HttpContext context, Action<Activity, HttpContext> onRequestStartedCallback)
        {
            Debug.Assert(context != null, "Context is null.");

            PropagationContext propagationContext = textMapPropagator.Extract(default, context.Request, HttpRequestHeaderValuesGetter);

            Activity activity = AspNetSource.StartActivity(TelemetryHttpModule.AspNetActivityName, ActivityKind.Server, propagationContext.ActivityContext);

            if (activity != null)
            {
                context.Items[ActivityKey] = activity;

                if (!(textMapPropagator is TraceContextPropagator))
                {
                    // todo: RestoreActivityIfNeeded below compensates for
                    // AsyncLocal Activity.Current being lost. Baggage
                    // potentially will suffer from the same issue, but we can't
                    // simply add it to context.Items because any change results
                    // in a new instance. Probably need to save it at the end of
                    // each OnExecuteRequestStep.
                    Baggage.Current = propagationContext.Baggage;
                }

                try
                {
                    onRequestStartedCallback?.Invoke(activity, context);
                }
                catch (Exception callbackEx)
                {
                    AspNetTelemetryEventSource.Log.CallbackException(activity, "OnStarted", callbackEx);
                }

                AspNetTelemetryEventSource.Log.ActivityStarted(activity);
            }
            else
            {
                context.Items[ActivityKey] = StartedButNotSampledObj;
            }

            return activity;
        }

        /// <summary>
        /// Stops the activity and notifies listeners about it.
        /// </summary>
        /// <param name="textMapPropagator"><see cref="TextMapPropagator"/>.</param>
        /// <param name="aspNetActivity"><see cref="Activity"/>.</param>
        /// <param name="context"><see cref="HttpContext"/>.</param>
        /// <param name="onRequestStoppedCallback">Callback action.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StopAspNetActivity(TextMapPropagator textMapPropagator, Activity aspNetActivity, HttpContext context, Action<Activity, HttpContext> onRequestStoppedCallback)
        {
            Debug.Assert(context != null, "Context is null.");

            if (aspNetActivity == null)
            {
                Debug.Assert(context.Items[ActivityKey] == StartedButNotSampledObj, "Context item is not StartedButNotSampledObj.");

                // This is the case where a start was called but no activity was
                // created due to a sampling decision.
                context.Items[ActivityKey] = null;
                return;
            }

            Debug.Assert(context.Items[ActivityKey] is Activity, "Context item is not an Activity instance.");

            var currentActivity = Activity.Current;

            aspNetActivity.Stop();
            context.Items[ActivityKey] = null;

            try
            {
                onRequestStoppedCallback?.Invoke(aspNetActivity, context);
            }
            catch (Exception callbackEx)
            {
                AspNetTelemetryEventSource.Log.CallbackException(aspNetActivity, "OnStopped", callbackEx);
            }

            AspNetTelemetryEventSource.Log.ActivityStopped(currentActivity);

            if (!(textMapPropagator is TraceContextPropagator))
            {
                Baggage.Current = default;
            }

            if (currentActivity != aspNetActivity)
            {
                Activity.Current = currentActivity;
            }
        }

        /// <summary>
        /// Notifies listeners about an unhandled exception thrown on the <see cref="HttpContext"/>.
        /// </summary>
        /// <param name="aspNetActivity"><see cref="Activity"/>.</param>
        /// <param name="context"><see cref="HttpContext"/>.</param>
        /// <param name="exception"><see cref="Exception"/>.</param>
        /// <param name="onExceptionCallback">Callback action.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteActivityException(Activity aspNetActivity, HttpContext context, Exception exception, Action<Activity, HttpContext, Exception> onExceptionCallback)
        {
            Debug.Assert(context != null, "Context is null.");
            Debug.Assert(exception != null, "Exception is null.");

            if (aspNetActivity != null)
            {
                try
                {
                    onExceptionCallback?.Invoke(aspNetActivity, context, exception);
                }
                catch (Exception callbackEx)
                {
                    AspNetTelemetryEventSource.Log.CallbackException(aspNetActivity, "OnException", callbackEx);
                }

                AspNetTelemetryEventSource.Log.ActivityException(aspNetActivity, exception);
            }
        }

        /// <summary>
        /// It's possible that a request is executed in both native threads and managed threads,
        /// in such case Activity.Current will be lost during native thread and managed thread switch.
        /// This method is intended to restore the current activity in order to correlate the child
        /// activities with the root activity of the request.
        /// </summary>
        /// <param name="contextItems">HttpContext.Items dictionary.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void RestoreActivityIfNeeded(IDictionary contextItems)
        {
            Debug.Assert(contextItems != null, "Context Items is null.");

            if (Activity.Current == null && contextItems[ActivityKey] is Activity aspNetActivity)
            {
                Activity.Current = aspNetActivity;
                AspNetTelemetryEventSource.Log.ActivityRestored(aspNetActivity);
            }
        }
    }
}

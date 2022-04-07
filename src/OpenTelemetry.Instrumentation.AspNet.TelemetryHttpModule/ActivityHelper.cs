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
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Web;
using OpenTelemetry.Context;
using OpenTelemetry.Context.Propagation;

namespace OpenTelemetry.Instrumentation.AspNet
{
    /// <summary>
    /// Activity helper class.
    /// </summary>
    internal static class ActivityHelper
    {
        /// <summary>
        /// Key to store the state in HttpContext.
        /// </summary>
        internal const string ContextKey = "__AspnetInstrumentationContext__";
        internal static readonly object StartedButNotSampledObj = new();

        private const string BaggageSlotName = "otel.baggage";
        private static readonly Func<HttpRequest, string, IEnumerable<string>> HttpRequestHeaderValuesGetter = (request, name) => request.Headers.GetValues(name);
        private static readonly ActivitySource AspNetSource = new(
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

            object itemValue = context.Items[ContextKey];
            if (itemValue is ContextHolder contextHolder)
            {
                aspNetActivity = contextHolder.Activity;
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
                if (textMapPropagator is not TraceContextPropagator)
                {
                    Baggage.Current = propagationContext.Baggage;

                    context.Items[ContextKey] = new ContextHolder { Activity = activity, Baggage = RuntimeContext.GetValue(BaggageSlotName) };
                }
                else
                {
                    context.Items[ContextKey] = new ContextHolder { Activity = activity };
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
                context.Items[ContextKey] = StartedButNotSampledObj;
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
                Debug.Assert(context.Items[ContextKey] == StartedButNotSampledObj, "Context item is not StartedButNotSampledObj.");

                // This is the case where a start was called but no activity was
                // created due to a sampling decision.
                context.Items[ContextKey] = null;
                return;
            }

            Debug.Assert(context.Items[ContextKey] is ContextHolder, "Context item is not an ContextHolder instance.");

            var currentActivity = Activity.Current;

            aspNetActivity.Stop();
            context.Items[ContextKey] = null;

            try
            {
                onRequestStoppedCallback?.Invoke(aspNetActivity, context);
            }
            catch (Exception callbackEx)
            {
                AspNetTelemetryEventSource.Log.CallbackException(aspNetActivity, "OnStopped", callbackEx);
            }

            AspNetTelemetryEventSource.Log.ActivityStopped(currentActivity);

            if (textMapPropagator is not TraceContextPropagator)
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
        /// <param name="context"><see cref="HttpContext"/>.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void RestoreContextIfNeeded(HttpContext context)
        {
            Debug.Assert(context != null, "Context is null.");

            if (context.Items[ContextKey] is ContextHolder contextHolder && Activity.Current != contextHolder.Activity)
            {
                Activity.Current = contextHolder.Activity;
                if (contextHolder.Baggage != null)
                {
                    RuntimeContext.SetValue(BaggageSlotName, contextHolder.Baggage);
                }

                AspNetTelemetryEventSource.Log.ActivityRestored(contextHolder.Activity);
            }
        }

        internal class ContextHolder
        {
            public Activity Activity;
            public object Baggage;
        }
    }
}

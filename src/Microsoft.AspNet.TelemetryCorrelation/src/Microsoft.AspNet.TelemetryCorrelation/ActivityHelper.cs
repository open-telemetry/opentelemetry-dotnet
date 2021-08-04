// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Diagnostics;
using System.Web;

namespace Microsoft.AspNet.TelemetryCorrelation
{
    /// <summary>
    /// Activity helper class
    /// </summary>
    internal static class ActivityHelper
    {
        /// <summary>
        /// Listener name.
        /// </summary>
        public const string AspNetListenerName = "Microsoft.AspNet.TelemetryCorrelation";

        /// <summary>
        /// Activity name for http request.
        /// </summary>
        public const string AspNetActivityName = "Microsoft.AspNet.HttpReqIn";

        /// <summary>
        /// Event name for the activity start event.
        /// </summary>
        public const string AspNetActivityStartName = "Microsoft.AspNet.HttpReqIn.Start";

        /// <summary>
        /// Key to store the activity in HttpContext.
        /// </summary>
        public const string ActivityKey = "__AspnetActivity__";

        private static readonly DiagnosticListener AspNetListener = new DiagnosticListener(AspNetListenerName);

        private static readonly object EmptyPayload = new object();

        /// <summary>
        /// Stops the activity and notifies listeners about it.
        /// </summary>
        /// <param name="contextItems">HttpContext.Items.</param>
        public static void StopAspNetActivity(IDictionary contextItems)
        {
            var currentActivity = Activity.Current;
            Activity aspNetActivity = (Activity)contextItems[ActivityKey];

            if (currentActivity != aspNetActivity)
            {
                Activity.Current = aspNetActivity;
                currentActivity = aspNetActivity;
            }

            if (currentActivity != null)
            {
                // stop Activity with Stop event
                AspNetListener.StopActivity(currentActivity, EmptyPayload);
                contextItems[ActivityKey] = null;
            }

            AspNetTelemetryCorrelationEventSource.Log.ActivityStopped(currentActivity?.Id, currentActivity?.OperationName);
        }

        /// <summary>
        /// Creates root (first level) activity that describes incoming request.
        /// </summary>
        /// <param name="context">Current HttpContext.</param>
        /// <param name="parseHeaders">Determines if headers should be parsed get correlation ids.</param>
        /// <returns>New root activity.</returns>
        public static Activity CreateRootActivity(HttpContext context, bool parseHeaders)
        {
            if (AspNetListener.IsEnabled() && AspNetListener.IsEnabled(AspNetActivityName))
            {
                var rootActivity = new Activity(AspNetActivityName);

                if (parseHeaders)
                {
                    rootActivity.Extract(context.Request.Unvalidated.Headers);
                }

                AspNetListener.OnActivityImport(rootActivity, null);

                if (StartAspNetActivity(rootActivity))
                {
                    context.Items[ActivityKey] = rootActivity;
                    AspNetTelemetryCorrelationEventSource.Log.ActivityStarted(rootActivity.Id);
                    return rootActivity;
                }
            }

            return null;
        }

        public static void WriteActivityException(IDictionary contextItems, Exception exception)
        {
            Activity aspNetActivity = (Activity)contextItems[ActivityKey];

            if (aspNetActivity != null)
            {
                if (Activity.Current != aspNetActivity)
                {
                    Activity.Current = aspNetActivity;
                }

                AspNetListener.Write(aspNetActivity.OperationName + ".Exception", exception);
                AspNetTelemetryCorrelationEventSource.Log.ActivityException(aspNetActivity.Id, aspNetActivity.OperationName, exception);
            }
        }

        /// <summary>
        /// It's possible that a request is executed in both native threads and managed threads,
        /// in such case Activity.Current will be lost during native thread and managed thread switch.
        /// This method is intended to restore the current activity in order to correlate the child
        /// activities with the root activity of the request.
        /// </summary>
        /// <param name="contextItems">HttpContext.Items dictionary.</param>
        internal static void RestoreActivityIfNeeded(IDictionary contextItems)
        {
            if (Activity.Current == null)
            {
                Activity aspNetActivity = (Activity)contextItems[ActivityKey];
                if (aspNetActivity != null)
                {
                    Activity.Current = aspNetActivity;
                }
            }
        }

        private static bool StartAspNetActivity(Activity activity)
        {
            if (AspNetListener.IsEnabled(AspNetActivityName, activity, EmptyPayload))
            {
                if (AspNetListener.IsEnabled(AspNetActivityStartName))
                {
                    AspNetListener.StartActivity(activity, EmptyPayload);
                }
                else
                {
                    activity.Start();
                }

                return true;
            }

            return false;
        }
    }
}

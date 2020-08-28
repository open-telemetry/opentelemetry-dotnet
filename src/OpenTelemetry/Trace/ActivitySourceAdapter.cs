﻿// <copyright file="ActivitySourceAdapter.cs" company="OpenTelemetry Authors">
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
using System.Linq.Expressions;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// This class encapsulates the logic for performing ActivitySource actions
    /// on Activities that are created using default ActivitySource.
    /// All activities created without using ActivitySource will have a
    /// default ActivitySource assigned to them with their name as empty string.
    /// This class is to be used by instrumentation adapters which converts/augments
    /// activies created without ActivitySource, into something which closely
    /// matches the one created using ActivitySource.
    /// </summary>
    /// <remarks>
    /// This class is meant to be only used when writing new Instrumentation for
    /// libraries which are already instrumented with DiagnosticSource/Activity
    /// following this doc:
    /// https://github.com/dotnet/runtime/blob/master/src/libraries/System.Diagnostics.DiagnosticSource/src/ActivityUserGuide.md.
    /// </remarks>
    public class ActivitySourceAdapter
    {
        private static readonly Action<Activity, ActivityKind> SetKindProperty = CreateActivityKindSetter();
        private readonly Sampler sampler;
        private readonly Resource resource;
        private ActivityProcessor activityProcessor;
        private Action<Activity> getRequestedDataAction;

        internal ActivitySourceAdapter(Sampler sampler, ActivityProcessor activityProcessor, Resource resource)
        {
            if (sampler == null)
            {
                throw new ArgumentNullException(nameof(sampler));
            }

            if (resource == null)
            {
                throw new ArgumentNullException(nameof(resource));
            }

            this.sampler = sampler;
            if (this.sampler is AlwaysOnSampler)
            {
                this.getRequestedDataAction = this.RunGetRequestedDataAlwaysOnSampler;
            }
            else if (this.sampler is AlwaysOffSampler)
            {
                this.getRequestedDataAction = this.RunGetRequestedDataAlwaysOffSampler;
            }
            else
            {
                this.getRequestedDataAction = this.RunGetRequestedDataOtherSampler;
            }

            this.activityProcessor = activityProcessor;
            this.resource = resource;
        }

        private ActivitySourceAdapter()
        {
        }

        /// <summary>
        /// Method that starts an <see cref="Activity"/>.
        /// </summary>
        /// <param name="activity"><see cref="Activity"/> to be started.</param>
        /// <param name="kind">ActivityKind to be set of the activity.</param>
        public void Start(Activity activity, ActivityKind kind)
        {
            SetKindProperty(activity, kind);
            this.getRequestedDataAction(activity);
            if (activity.IsAllDataRequested)
            {
                activity.SetResource(this.resource);
                this.activityProcessor?.OnStart(activity);
            }
        }

        /// <summary>
        /// Method that stops an <see cref="Activity"/>.
        /// </summary>
        /// <param name="activity"><see cref="Activity"/> to be stopped.</param>
        public void Stop(Activity activity)
        {
            if (activity?.IsAllDataRequested ?? false)
            {
                this.activityProcessor?.OnEnd(activity);
            }
        }

        internal void UpdateProcessor(ActivityProcessor processor)
        {
            this.activityProcessor = processor;
        }

        private static Action<Activity, ActivityKind> CreateActivityKindSetter()
        {
            ParameterExpression instance = Expression.Parameter(typeof(Activity), "instance");
            ParameterExpression propertyValue = Expression.Parameter(typeof(ActivityKind), "propertyValue");
            var body = Expression.Assign(Expression.Property(instance, "Kind"), propertyValue);
            return Expression.Lambda<Action<Activity, ActivityKind>>(body, instance, propertyValue).Compile();
        }

        private void RunGetRequestedDataAlwaysOnSampler(Activity activity)
        {
            activity.IsAllDataRequested = true;
            activity.ActivityTraceFlags |= ActivityTraceFlags.Recorded;
        }

        private void RunGetRequestedDataAlwaysOffSampler(Activity activity)
        {
            activity.IsAllDataRequested = false;
        }

        private void RunGetRequestedDataOtherSampler(Activity activity)
        {
            ActivityContext parentContext;
            if (string.IsNullOrEmpty(activity.ParentId))
            {
                parentContext = default;
            }
            else if (activity.Parent != null)
            {
                parentContext = activity.Parent.Context;
            }
            else
            {
                parentContext = new ActivityContext(
                    activity.TraceId,
                    activity.ParentSpanId,
                    activity.ActivityTraceFlags,
                    activity.TraceStateString,
                    isRemote: true);
            }

            var samplingParameters = new SamplingParameters(
                parentContext,
                activity.TraceId,
                activity.DisplayName,
                activity.Kind,
                activity.TagObjects,
                activity.Links);

            var samplingResult = this.sampler.ShouldSample(samplingParameters);

            switch (samplingResult.Decision)
            {
                case SamplingDecision.NotRecord:
                    activity.IsAllDataRequested = false;
                    break;
                case SamplingDecision.Record:
                    activity.IsAllDataRequested = true;
                    break;
                case SamplingDecision.RecordAndSampled:
                    activity.IsAllDataRequested = true;
                    activity.ActivityTraceFlags |= ActivityTraceFlags.Recorded;
                    break;
            }
        }
    }
}

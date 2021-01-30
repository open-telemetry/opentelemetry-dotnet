// <copyright file="ActivitySourceAdapter.cs" company="OpenTelemetry Authors">
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
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using OpenTelemetry.Internal;

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
    internal class ActivitySourceAdapter
    {
        private static readonly Action<Activity, ActivityKind> SetKindProperty = CreateActivityKindSetter();
        private static readonly Action<Activity, ActivitySource> SetActivitySourceProperty = CreateActivitySourceSetter();
        private static readonly Func<ActivitySource, Activity, ActivityContext, SampleResult> CallSamplersFunc = CreateCallSamplersFunc();
        private readonly Sampler sampler;
        private readonly Action<ActivitySource, Activity> getRequestedDataAction;
        private BaseProcessor<Activity> activityProcessor;

        internal ActivitySourceAdapter(Sampler sampler, BaseProcessor<Activity> activityProcessor)
        {
            this.sampler = sampler ?? throw new ArgumentNullException(nameof(sampler));
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
        }

        private ActivitySourceAdapter()
        {
        }

        /// <summary>
        /// Method that starts an <see cref="Activity"/>.
        /// </summary>
        /// <param name="activity"><see cref="Activity"/> to be started.</param>
        /// <param name="kind">ActivityKind to be set of the activity.</param>
        /// <param name="source">ActivitySource to be set of the activity.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1062:Validate arguments of public methods", Justification = "ActivityProcessor is hot path")]
        public void Start(Activity activity, ActivityKind kind, ActivitySource source)
        {
            OpenTelemetrySdkEventSource.Log.ActivityStarted(activity);

            SetActivitySourceProperty(activity, source);
            SetKindProperty(activity, kind);
            this.getRequestedDataAction(source, activity);
            if (activity.IsAllDataRequested)
            {
                this.activityProcessor?.OnStart(activity);
            }
        }

        /// <summary>
        /// Method that stops an <see cref="Activity"/>.
        /// </summary>
        /// <param name="activity"><see cref="Activity"/> to be stopped.</param>
        public void Stop(Activity activity)
        {
            OpenTelemetrySdkEventSource.Log.ActivityStopped(activity);

            if (activity?.IsAllDataRequested ?? false)
            {
                this.activityProcessor?.OnEnd(activity);
            }
        }

        internal void UpdateProcessor(BaseProcessor<Activity> processor)
        {
            this.activityProcessor = processor;
        }

        private static Action<Activity, ActivitySource> CreateActivitySourceSetter()
        {
            ParameterExpression instance = Expression.Parameter(typeof(Activity), "instance");
            ParameterExpression propertyValue = Expression.Parameter(typeof(ActivitySource), "propertyValue");
            var body = Expression.Assign(Expression.Property(instance, "Source"), propertyValue);
            return Expression.Lambda<Action<Activity, ActivitySource>>(body, instance, propertyValue).Compile();
        }

        private static Action<Activity, ActivityKind> CreateActivityKindSetter()
        {
            ParameterExpression instance = Expression.Parameter(typeof(Activity), "instance");
            ParameterExpression propertyValue = Expression.Parameter(typeof(ActivityKind), "propertyValue");
            var body = Expression.Assign(Expression.Property(instance, "Kind"), propertyValue);
            return Expression.Lambda<Action<Activity, ActivityKind>>(body, instance, propertyValue).Compile();
        }

        /*
            Build a dynamic method for invoking the sample method on all listeners.

            Based on this: https://github.com/dotnet/runtime/blob/f2148079d4476073bb5e79277b557807ed0c9984/src/libraries/System.Diagnostics.DiagnosticSource/src/System/Diagnostics/ActivitySource.cs#L194-L216

            SampleResult Sample(ActivitySource activitySource, Activity activity, ActivityContext activityContext)
            {
                SynchronizedList<ActivityListener>? listeners = _listeners;
                if (listeners == null || listeners.Count == 0)
                {
                    return new SampleResult(ActivitySamplingResult.None, null);
                }

                ActivitySamplingResult samplingResult = default;

                ActivityCreationOptions<ActivityContext> aco = new ActivityCreationOptions<ActivityContext>(activitySource, activity.DisplayName, activityContext, activity.Kind, activity.TagObjects, activity.Links);

                listeners.EnumWithFunc((ActivityListener listener, ref ActivityCreationOptions<ActivityContext> data, ref ActivitySamplingResult result, ref ActivityCreationOptions<ActivityContext> unused) => {
                    SampleActivity<ActivityContext>? sample = listener.Sample;
                    if (sample != null)
                    {
                        ActivitySamplingResult dr = sample(ref data);
                        if (dr > result)
                        {
                            result = dr;
                        }
                    }
                }, ref aco, ref samplingResult, ref aco);

                return new SampleResult(samplingResult, aco._samplerTags);
            }
        */
        private static Func<ActivitySource, Activity, ActivityContext, SampleResult> CreateCallSamplersFunc()
        {
            Type activitySourceType = typeof(ActivitySource);
            Type activityType = typeof(Activity);

            var dynamicMethod = new DynamicMethod(
                nameof(ActivitySourceAdapter),
                typeof(SampleResult),
                new[] { activitySourceType, typeof(Activity), typeof(ActivityContext) },
                typeof(ActivitySourceAdapter).Module,
                skipVisibility: true);

            FieldInfo listenersField = activitySourceType.GetField("_listeners", BindingFlags.Instance | BindingFlags.NonPublic);
            if (listenersField == null)
            {
                throw new InvalidOperationException("_listeners field could not be found on ActivitySource.");
            }

            Type functionType = null;
            MethodInfo enumWithFuncClosureMethod = null;
            FieldInfo compilerTypeField = null;
            FieldInfo enumWithFuncClosureDelegate = null;

            foreach (Type nestedType in activitySourceType.GetNestedTypes(BindingFlags.Instance | BindingFlags.NonPublic))
            {
                if (nestedType.Name.StartsWith("Function"))
                {
                    functionType = nestedType;
                    continue;
                }

                if (nestedType.Name != "<>c")
                {
                    continue;
                }

                foreach (MethodInfo compilerMethod in nestedType.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic))
                {
                    if (!compilerMethod.Name.StartsWith("<StartActivity>"))
                    {
                        continue;
                    }

                    ParameterInfo[] parameters = compilerMethod.GetParameters();
                    if (parameters.Length == 4 && parameters[3].Name == "unused")
                    {
                        enumWithFuncClosureMethod = compilerMethod;
                        break;
                    }
                }

                foreach (FieldInfo compilerField in nestedType.GetFields(BindingFlags.Static | BindingFlags.Public))
                {
                    if (compilerField.FieldType.Name == "<>c")
                    {
                        compilerTypeField = compilerField;
                        continue;
                    }

                    if (compilerField.FieldType.Name.StartsWith("Function"))
                    {
                        Type[] genericArguments = compilerField.FieldType.GetGenericArguments();
                        if (genericArguments.Length == 2 && genericArguments[0] == typeof(ActivityListener) && genericArguments[1] == typeof(ActivityContext))
                        {
                            enumWithFuncClosureDelegate = compilerField;
                            continue;
                        }
                    }
                }
            }

            if (functionType == null)
            {
                throw new InvalidOperationException("Function type could not be found on ActivitySource.");
            }

            if (enumWithFuncClosureMethod == null || compilerTypeField == null || enumWithFuncClosureDelegate == null)
            {
                throw new InvalidOperationException("StartActivity closure could not be found on ActivitySource.");
            }

            var generator = dynamicMethod.GetILGenerator();

            Label returnNullLabel = generator.DefineLabel();
            Label performLogicLabel = generator.DefineLabel();
            Label delegateExistsLabel = generator.DefineLabel();

            generator.DeclareLocal(listenersField.FieldType); // listeners
            generator.DeclareLocal(typeof(ActivityCreationOptions<ActivityContext>)); // aco
            generator.DeclareLocal(typeof(ActivitySamplingResult)); // samplingResult

            // SynchronizedList<ActivityListener>? listeners = _listeners;
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldfld, listenersField);
            generator.Emit(OpCodes.Stloc_0);

            // if (listeners == null || listeners.Count == 0) return ActivitySamplingResult.None
            generator.Emit(OpCodes.Ldloc_0);
            generator.Emit(OpCodes.Brfalse_S, returnNullLabel);
            generator.Emit(OpCodes.Ldloc_0);
            generator.Emit(OpCodes.Callvirt, listenersField.FieldType.GetProperty("Count").GetMethod);
            generator.Emit(OpCodes.Brtrue_S, performLogicLabel);

            generator.MarkLabel(returnNullLabel);
            generator.Emit(OpCodes.Ldc_I4_0); // ActivitySamplingResult.None
            generator.Emit(OpCodes.Ldnull); // null
            generator.Emit(OpCodes.Newobj, typeof(SampleResult).GetConstructor(new Type[] { typeof(ActivitySamplingResult), typeof(ActivityTagsCollection) }));
            generator.Emit(OpCodes.Ret);

            generator.MarkLabel(performLogicLabel);

            generator.Emit(OpCodes.Ldloca_S, 1); // &aco
            generator.Emit(OpCodes.Ldarg_0); // activitySource

            generator.Emit(OpCodes.Ldarg_1); // activity
            generator.Emit(OpCodes.Callvirt, activityType.GetProperty("DisplayName").GetMethod); // activity.DisplayName

            generator.Emit(OpCodes.Ldarg_2); // activityContext

            generator.Emit(OpCodes.Ldarg_1); // activity
            generator.Emit(OpCodes.Callvirt, activityType.GetProperty("Kind").GetMethod); // activity.Kind

            generator.Emit(OpCodes.Ldarg_1); // activity
            generator.Emit(OpCodes.Callvirt, activityType.GetProperty("TagObjects").GetMethod); // activity.TagObjects

            generator.Emit(OpCodes.Ldarg_1); // activity
            generator.Emit(OpCodes.Callvirt, activityType.GetProperty("Links").GetMethod); // activity.Links

            // aco = new ActivityCreationOptions<ActivityContext>(activitySource, activity.Name, activityContext, activity.Kind, activity.TagObjects, activity.Links)
            generator.Emit(OpCodes.Call, typeof(ActivityCreationOptions<ActivityContext>).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new Type[] { activitySourceType, typeof(string), typeof(ActivityContext), typeof(ActivityKind), typeof(IEnumerable<KeyValuePair<string, object>>), typeof(IEnumerable<ActivityLink>) },
                null));

            generator.Emit(OpCodes.Ldloc_0); // listeners

            generator.Emit(OpCodes.Ldsfld, enumWithFuncClosureDelegate); // loading the delegate for the closure
            generator.Emit(OpCodes.Dup);
            generator.Emit(OpCodes.Brtrue_S, delegateExistsLabel);
            generator.Emit(OpCodes.Pop);
            generator.Emit(OpCodes.Ldsfld, compilerTypeField);
            generator.Emit(OpCodes.Ldftn, enumWithFuncClosureMethod);
            generator.Emit(OpCodes.Newobj, functionType.MakeGenericType(typeof(ActivityListener), typeof(ActivityContext)).GetConstructors()[0]);
            generator.Emit(OpCodes.Dup);
            generator.Emit(OpCodes.Stsfld, enumWithFuncClosureDelegate);

            generator.MarkLabel(delegateExistsLabel);

            generator.Emit(OpCodes.Ldloca_S, 1); // &aco
            generator.Emit(OpCodes.Ldloca_S, 2); // &samplingResult
            generator.Emit(OpCodes.Ldloca_S, 1); // &aco
            generator.Emit(OpCodes.Callvirt, listenersField.FieldType.GetMethod("EnumWithFunc").MakeGenericMethod(typeof(ActivityContext))); // listeners.EnumWithFunc

            generator.Emit(OpCodes.Ldloc_2); // samplingResult
            generator.Emit(OpCodes.Ldloca_S, 1); // &aco
            generator.Emit(OpCodes.Ldfld, typeof(ActivityCreationOptions<ActivityContext>).GetField("_samplerTags", BindingFlags.Instance | BindingFlags.NonPublic)); // aco._samplerTags
            generator.Emit(OpCodes.Newobj, typeof(SampleResult).GetConstructor(new Type[] { typeof(ActivitySamplingResult), typeof(ActivityTagsCollection) }));
            generator.Emit(OpCodes.Ret);

            return (Func<ActivitySource, Activity, ActivityContext, SampleResult>)dynamicMethod.CreateDelegate(typeof(Func<ActivitySource, Activity, ActivityContext, SampleResult>));
        }

        private void RunGetRequestedDataAlwaysOnSampler(ActivitySource activitySource, Activity activity)
        {
            activity.IsAllDataRequested = true;
            activity.ActivityTraceFlags |= ActivityTraceFlags.Recorded;
        }

        private void RunGetRequestedDataAlwaysOffSampler(ActivitySource activitySource, Activity activity)
        {
            activity.IsAllDataRequested = false;
        }

        private void RunGetRequestedDataOtherSampler(ActivitySource activitySource, Activity activity)
        {
            ActivityContext parentContext;

            // Check activity.ParentId alone is sufficient to normally determine if a activity is root or not. But if one uses activity.SetParentId to override the TraceId (without intending to set an actual parent), then additional check of parentspanid being empty is required to confirm if an activity is root or not.
            // This checker can be removed, once Activity exposes an API to customize ID Generation (https://github.com/dotnet/runtime/issues/46704) or issue https://github.com/dotnet/runtime/issues/46706 is addressed.
            if (string.IsNullOrEmpty(activity.ParentId) || activity.ParentSpanId.ToHexString() == "0000000000000000")
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

            SampleResult samplingResult = CallSamplersFunc(activitySource, activity, parentContext);

            switch (samplingResult.ActivitySamplingResult)
            {
                case ActivitySamplingResult.PropagationData:
                    activity.IsAllDataRequested = false;
                    break;
                case ActivitySamplingResult.AllData:
                    activity.IsAllDataRequested = true;
                    break;
                case ActivitySamplingResult.AllDataAndRecorded:
                    activity.IsAllDataRequested = true;
                    activity.ActivityTraceFlags |= ActivityTraceFlags.Recorded;
                    break;
            }

            if (samplingResult.ActivityTagsCollection != null
                && samplingResult.ActivitySamplingResult != ActivitySamplingResult.None
                && samplingResult.ActivitySamplingResult != ActivitySamplingResult.None)
            {
                foreach (var att in samplingResult.ActivityTagsCollection)
                {
                    activity.SetTag(att.Key, att.Value);
                }
            }
        }

        private readonly struct SampleResult
        {
            public SampleResult(ActivitySamplingResult activitySamplingResult, ActivityTagsCollection samplingTags)
            {
                this.ActivitySamplingResult = activitySamplingResult;
                this.ActivityTagsCollection = samplingTags;
            }

            public ActivitySamplingResult ActivitySamplingResult { get; }

            public ActivityTagsCollection ActivityTagsCollection { get; }
        }
    }
}

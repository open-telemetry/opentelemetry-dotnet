// <copyright file="HttpWebRequestActivitySource.net461.cs" company="OpenTelemetry Authors">
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
#if NET461
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Instrumentation.Dependencies.Implementation
{
    /// <summary>
    /// Hooks into the <see cref="HttpWebRequest"/> class reflectively and writes diagnostic events as requests are processed.
    /// </summary>
    /// <remarks>
    /// Inspired from the System.Diagnostics.DiagnosticSource.HttpHandlerDiagnosticListener class which has some bugs and feature gaps.
    /// See https://github.com/dotnet/runtime/pull/33732 for details.
    /// </remarks>
    internal sealed class HttpWebRequestActivitySource
    {
        internal const string ActivitySourceName = "HttpWebRequest";
        internal const string ActivityName = ActivitySourceName + ".HttpRequestOut";

        internal static readonly HttpWebRequestActivitySource Instance = new HttpWebRequestActivitySource();

        private const string CorrelationContextHeaderName = "Correlation-Context";
        private const string TraceParentHeaderName = "traceparent";
        private const string TraceStateHeaderName = "tracestate";

        private static readonly Version Version = typeof(HttpWebRequestActivitySource).Assembly.GetName().Version;
        private static readonly ActivitySource WebRequestActivitySource = new ActivitySource(ActivitySourceName, Version.ToString());

        // Fields for reflection
        private static FieldInfo connectionGroupListField;
        private static Type connectionGroupType;
        private static FieldInfo connectionListField;
        private static Type connectionType;
        private static FieldInfo writeListField;
        private static Func<object, IAsyncResult> writeAResultAccessor;
        private static Func<object, IAsyncResult> readAResultAccessor;

        // LazyAsyncResult & ContextAwareResult
        private static Func<object, AsyncCallback> asyncCallbackAccessor;
        private static Action<object, AsyncCallback> asyncCallbackModifier;
        private static Func<object, object> asyncStateAccessor;
        private static Action<object, object> asyncStateModifier;
        private static Func<object, bool> endCalledAccessor;
        private static Func<object, object> resultAccessor;
        private static Func<object, bool> isContextAwareResultChecker;

        // HttpWebResponse
        private static Func<object[], HttpWebResponse> httpWebResponseCtor;
        private static Func<HttpWebResponse, Uri> uriAccessor;
        private static Func<HttpWebResponse, object> verbAccessor;
        private static Func<HttpWebResponse, string> mediaTypeAccessor;
        private static Func<HttpWebResponse, bool> usesProxySemanticsAccessor;
        private static Func<HttpWebResponse, object> coreResponseDataAccessor;
        private static Func<HttpWebResponse, bool> isWebSocketResponseAccessor;
        private static Func<HttpWebResponse, string> connectionGroupNameAccessor;

        internal HttpWebRequestActivitySource()
        {
            try
            {
                PrepareReflectionObjects();
                PerformInjection();
            }
            catch (Exception ex)
            {
                // If anything went wrong, just no-op. Write an event so at least we can find out.
                InstrumentationEventSource.Log.ExceptionInitializingInstrumentation(typeof(HttpWebRequestActivitySource).FullName, ex);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AddRequestTagsAndInstrumentRequest(HttpWebRequest request, Activity activity)
        {
            activity.DisplayName = HttpTagHelper.GetOperationNameForHttpMethod(request.Method);

            InstrumentRequest(request, activity);

            activity.SetCustomProperty("HttpWebRequest.Request", request);

            if (activity.IsAllDataRequested)
            {
                activity.AddTag(SpanAttributeConstants.ComponentKey, "http");
                activity.AddTag(SpanAttributeConstants.HttpMethodKey, request.Method);
                activity.AddTag(SpanAttributeConstants.HttpHostKey, HttpTagHelper.GetHostTagValueFromRequestUri(request.RequestUri));
                activity.AddTag(SpanAttributeConstants.HttpUrlKey, request.RequestUri.OriginalString);
                activity.AddTag(SpanAttributeConstants.HttpFlavorKey, HttpTagHelper.GetFlavorTagValueFromProtocolVersion(request.ProtocolVersion));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AddResponseTags(HttpWebResponse response, Activity activity)
        {
            activity.SetCustomProperty("HttpWebRequest.Response", response);

            if (activity.IsAllDataRequested)
            {
                activity.AddTag(SpanAttributeConstants.HttpStatusCodeKey, HttpTagHelper.GetStatusCodeTagValueFromHttpStatusCode(response.StatusCode));

                Status status = SpanHelper.ResolveSpanStatusForHttpStatusCode((int)response.StatusCode);

                activity.AddTag(SpanAttributeConstants.StatusCodeKey, SpanHelper.GetCachedCanonicalCodeString(status.CanonicalCode));
                activity.AddTag(SpanAttributeConstants.StatusDescriptionKey, response.StatusDescription);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AddExceptionTags(Exception exception, Activity activity)
        {
            activity.SetCustomProperty("HttpWebRequest.Exception", exception);

            if (!activity.IsAllDataRequested)
            {
                return;
            }

            Status status;
            if (exception is WebException wexc)
            {
                if (wexc.Response is HttpWebResponse response)
                {
                    activity.AddTag(SpanAttributeConstants.HttpStatusCodeKey, HttpTagHelper.GetStatusCodeTagValueFromHttpStatusCode(response.StatusCode));

                    status = SpanHelper.ResolveSpanStatusForHttpStatusCode((int)response.StatusCode).WithDescription(response.StatusDescription);
                }
                else
                {
                    switch (wexc.Status)
                    {
                        case WebExceptionStatus.Timeout:
                            status = Status.DeadlineExceeded;
                            break;
                        case WebExceptionStatus.NameResolutionFailure:
                            status = Status.InvalidArgument.WithDescription(exception.Message);
                            break;
                        case WebExceptionStatus.SendFailure:
                        case WebExceptionStatus.ConnectFailure:
                        case WebExceptionStatus.SecureChannelFailure:
                        case WebExceptionStatus.TrustFailure:
                            status = Status.FailedPrecondition.WithDescription(exception.Message);
                            break;
                        case WebExceptionStatus.ServerProtocolViolation:
                            status = Status.Unimplemented.WithDescription(exception.Message);
                            break;
                        case WebExceptionStatus.RequestCanceled:
                            status = Status.Cancelled;
                            break;
                        case WebExceptionStatus.MessageLengthLimitExceeded:
                            status = Status.ResourceExhausted.WithDescription(exception.Message);
                            break;
                        default:
                            status = Status.Unknown.WithDescription(exception.Message);
                            break;
                    }
                }
            }
            else
            {
                status = Status.Unknown.WithDescription(exception.Message);
            }

            activity.AddTag(SpanAttributeConstants.StatusCodeKey, SpanHelper.GetCachedCanonicalCodeString(status.CanonicalCode));
            activity.AddTag(SpanAttributeConstants.StatusDescriptionKey, status.Description);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void InstrumentRequest(HttpWebRequest request, Activity activity)
        {
            // do not inject header if it was injected already
            // perhaps tracing systems wants to override it
            if (request.Headers.Get(TraceParentHeaderName) == null)
            {
                request.Headers.Add(TraceParentHeaderName, activity.Id);

                string traceState = activity.TraceStateString;
                if (traceState != null)
                {
                    request.Headers.Add(TraceStateHeaderName, traceState);
                }
            }

            if (request.Headers.Get(CorrelationContextHeaderName) == null)
            {
                // we expect baggage to be empty or contain a few items
                using IEnumerator<KeyValuePair<string, string>> e = activity.Baggage.GetEnumerator();

                if (e.MoveNext())
                {
                    StringBuilder baggage = new StringBuilder();
                    do
                    {
                        KeyValuePair<string, string> item = e.Current;
                        baggage.Append(WebUtility.UrlEncode(item.Key)).Append('=').Append(WebUtility.UrlEncode(item.Value)).Append(',');
                    }
                    while (e.MoveNext());
                    baggage.Remove(baggage.Length - 1, 1);
                    request.Headers.Add(CorrelationContextHeaderName, baggage.ToString());
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsRequestInstrumented(HttpWebRequest request)
            => request.Headers.Get(TraceParentHeaderName) != null;

        private static void ProcessRequest(HttpWebRequest request)
        {
            if (!WebRequestActivitySource.HasListeners() || IsRequestInstrumented(request))
            {
                // No subscribers to the ActivitySource or this request was instrumented by previous
                // ProcessRequest, such is the case with redirect responses where the same request is sent again.
                return;
            }

            var activity = WebRequestActivitySource.StartActivity(ActivityName, ActivityKind.Client);

            if (activity == null)
            {
                // There is a listener but it decided not to sample the current request.
                return;
            }

            IAsyncResult asyncContext = writeAResultAccessor(request);
            if (asyncContext != null)
            {
                // Flow here is for [Begin]GetRequestStream[Async].

                AsyncCallbackWrapper callback = new AsyncCallbackWrapper(request, activity, asyncCallbackAccessor(asyncContext));
                asyncCallbackModifier(asyncContext, callback.AsyncCallback);
            }
            else
            {
                // Flow here is for [Begin]GetResponse[Async] without a prior call to [Begin]GetRequestStream[Async].

                asyncContext = readAResultAccessor(request);
                AsyncCallbackWrapper callback = new AsyncCallbackWrapper(request, activity, asyncCallbackAccessor(asyncContext));
                asyncCallbackModifier(asyncContext, callback.AsyncCallback);
            }

            AddRequestTagsAndInstrumentRequest(request, activity);
        }

        private static void HookOrProcessResult(HttpWebRequest request)
        {
            IAsyncResult writeAsyncContext = writeAResultAccessor(request);
            if (writeAsyncContext == null || !(asyncCallbackAccessor(writeAsyncContext)?.Target is AsyncCallbackWrapper writeAsyncContextCallback))
            {
                // If we already hooked into the read result during ProcessRequest or we hooked up after the fact already we don't need to do anything here.
                return;
            }

            // If we got here it means the user called [Begin]GetRequestStream[Async] and we have to hook the read result after the fact.

            IAsyncResult readAsyncContext = readAResultAccessor(request);
            if (readAsyncContext == null)
            {
                // We're still trying to establish the connection (no read has started).
                return;
            }

            // Clear our saved callback so we know not to process again.
            asyncCallbackModifier(writeAsyncContext, null);

            if (endCalledAccessor.Invoke(readAsyncContext) || readAsyncContext.CompletedSynchronously)
            {
                // We need to process the result directly because the read callback has already fired. Force a copy because response has likely already been disposed.
                ProcessResult(readAsyncContext, null, writeAsyncContextCallback.Activity, resultAccessor(readAsyncContext), true);
                return;
            }

            // Hook into the result callback if it hasn't already fired.
            AsyncCallbackWrapper callback = new AsyncCallbackWrapper(writeAsyncContextCallback.Request, writeAsyncContextCallback.Activity, asyncCallbackAccessor(readAsyncContext));
            asyncCallbackModifier(readAsyncContext, callback.AsyncCallback);
        }

        private static void ProcessResult(IAsyncResult asyncResult, AsyncCallback asyncCallback, Activity activity, object result, bool forceResponseCopy)
        {
            // We could be executing on a different thread now so set the activity.
            Debug.Assert(Activity.Current == null || Activity.Current == activity, "There was an unexpected active Activity on the result thread.");
            if (Activity.Current == null)
            {
                Activity.Current = activity;
            }

            try
            {
                if (result is Exception ex)
                {
                    AddExceptionTags(ex, activity);
                }
                else
                {
                    HttpWebResponse response = (HttpWebResponse)result;

                    if (forceResponseCopy || (asyncCallback == null && isContextAwareResultChecker(asyncResult)))
                    {
                        // For async calls (where asyncResult is ContextAwareResult)...
                        // If no callback was set assume the user is manually calling BeginGetResponse & EndGetResponse
                        // in which case they could dispose the HttpWebResponse before our listeners have a chance to work with it.
                        // Disposed HttpWebResponse throws when accessing properties, so let's make a copy of the data to ensure that doesn't happen.

                        HttpWebResponse responseCopy = httpWebResponseCtor(
                            new object[]
                            {
                                uriAccessor(response), verbAccessor(response), coreResponseDataAccessor(response), mediaTypeAccessor(response),
                                usesProxySemanticsAccessor(response), DecompressionMethods.None,
                                isWebSocketResponseAccessor(response), connectionGroupNameAccessor(response),
                            });

                        AddResponseTags(responseCopy, activity);
                    }
                    else
                    {
                        AddResponseTags(response, activity);
                    }
                }
            }
            catch
            {
            }

            activity.Stop();
        }

        private static void PrepareReflectionObjects()
        {
            // At any point, if the operation failed, it should just throw. The caller should catch all exceptions and swallow.

            Type servicePointType = typeof(ServicePoint);
            Assembly systemNetHttpAssembly = servicePointType.Assembly;
            connectionGroupListField = servicePointType.GetField("m_ConnectionGroupList", BindingFlags.Instance | BindingFlags.NonPublic);
            connectionGroupType = systemNetHttpAssembly?.GetType("System.Net.ConnectionGroup");
            connectionListField = connectionGroupType?.GetField("m_ConnectionList", BindingFlags.Instance | BindingFlags.NonPublic);
            connectionType = systemNetHttpAssembly?.GetType("System.Net.Connection");
            writeListField = connectionType?.GetField("m_WriteList", BindingFlags.Instance | BindingFlags.NonPublic);

            writeAResultAccessor = CreateFieldGetter<IAsyncResult>(typeof(HttpWebRequest), "_WriteAResult", BindingFlags.NonPublic | BindingFlags.Instance);
            readAResultAccessor = CreateFieldGetter<IAsyncResult>(typeof(HttpWebRequest), "_ReadAResult", BindingFlags.NonPublic | BindingFlags.Instance);

            // Double checking to make sure we have all the pieces initialized
            if (connectionGroupListField == null ||
                connectionGroupType == null ||
                connectionListField == null ||
                connectionType == null ||
                writeListField == null ||
                writeAResultAccessor == null ||
                readAResultAccessor == null ||
                !PrepareAsyncResultReflectionObjects(systemNetHttpAssembly) ||
                !PrepareHttpWebResponseReflectionObjects(systemNetHttpAssembly))
            {
                // If anything went wrong here, just return false. There is nothing we can do.
                throw new InvalidOperationException("Unable to initialize all required reflection objects");
            }
        }

        private static bool PrepareAsyncResultReflectionObjects(Assembly systemNetHttpAssembly)
        {
            Type lazyAsyncResultType = systemNetHttpAssembly?.GetType("System.Net.LazyAsyncResult");
            if (lazyAsyncResultType != null)
            {
                asyncCallbackAccessor = CreateFieldGetter<AsyncCallback>(lazyAsyncResultType, "m_AsyncCallback", BindingFlags.NonPublic | BindingFlags.Instance);
                asyncCallbackModifier = CreateFieldSetter<AsyncCallback>(lazyAsyncResultType, "m_AsyncCallback", BindingFlags.NonPublic | BindingFlags.Instance);
                asyncStateAccessor = CreateFieldGetter<object>(lazyAsyncResultType, "m_AsyncState", BindingFlags.NonPublic | BindingFlags.Instance);
                asyncStateModifier = CreateFieldSetter<object>(lazyAsyncResultType, "m_AsyncState", BindingFlags.NonPublic | BindingFlags.Instance);
                endCalledAccessor = CreateFieldGetter<bool>(lazyAsyncResultType, "m_EndCalled", BindingFlags.NonPublic | BindingFlags.Instance);
                resultAccessor = CreateFieldGetter<object>(lazyAsyncResultType, "m_Result", BindingFlags.NonPublic | BindingFlags.Instance);
            }

            Type contextAwareResultType = systemNetHttpAssembly?.GetType("System.Net.ContextAwareResult");
            if (contextAwareResultType != null)
            {
                isContextAwareResultChecker = CreateTypeChecker(contextAwareResultType);
            }

            return asyncCallbackAccessor != null
                && asyncCallbackModifier != null
                && asyncStateAccessor != null
                && asyncStateModifier != null
                && endCalledAccessor != null
                && resultAccessor != null
                && isContextAwareResultChecker != null;
        }

        private static bool PrepareHttpWebResponseReflectionObjects(Assembly systemNetHttpAssembly)
        {
            Type knownHttpVerbType = systemNetHttpAssembly?.GetType("System.Net.KnownHttpVerb");
            Type coreResponseData = systemNetHttpAssembly?.GetType("System.Net.CoreResponseData");

            if (knownHttpVerbType != null && coreResponseData != null)
            {
                var constructorParameterTypes = new Type[]
                {
                    typeof(Uri), knownHttpVerbType, coreResponseData, typeof(string),
                    typeof(bool), typeof(DecompressionMethods),
                    typeof(bool), typeof(string),
                };

                ConstructorInfo ctor = typeof(HttpWebResponse).GetConstructor(
                    BindingFlags.NonPublic | BindingFlags.Instance,
                    null,
                    constructorParameterTypes,
                    null);

                if (ctor != null)
                {
                    httpWebResponseCtor = CreateTypeInstance<HttpWebResponse>(ctor);
                }
            }

            uriAccessor = CreateFieldGetter<HttpWebResponse, Uri>("m_Uri", BindingFlags.NonPublic | BindingFlags.Instance);
            verbAccessor = CreateFieldGetter<HttpWebResponse, object>("m_Verb", BindingFlags.NonPublic | BindingFlags.Instance);
            mediaTypeAccessor = CreateFieldGetter<HttpWebResponse, string>("m_MediaType", BindingFlags.NonPublic | BindingFlags.Instance);
            usesProxySemanticsAccessor = CreateFieldGetter<HttpWebResponse, bool>("m_UsesProxySemantics", BindingFlags.NonPublic | BindingFlags.Instance);
            coreResponseDataAccessor = CreateFieldGetter<HttpWebResponse, object>("m_CoreResponseData", BindingFlags.NonPublic | BindingFlags.Instance);
            isWebSocketResponseAccessor = CreateFieldGetter<HttpWebResponse, bool>("m_IsWebSocketResponse", BindingFlags.NonPublic | BindingFlags.Instance);
            connectionGroupNameAccessor = CreateFieldGetter<HttpWebResponse, string>("m_ConnectionGroupName", BindingFlags.NonPublic | BindingFlags.Instance);

            return httpWebResponseCtor != null
                && uriAccessor != null
                && verbAccessor != null
                && mediaTypeAccessor != null
                && usesProxySemanticsAccessor != null
                && coreResponseDataAccessor != null
                && isWebSocketResponseAccessor != null
                && connectionGroupNameAccessor != null;
        }

        private static void PerformInjection()
        {
            FieldInfo servicePointTableField = typeof(ServicePointManager).GetField("s_ServicePointTable", BindingFlags.Static | BindingFlags.NonPublic);
            if (servicePointTableField == null)
            {
                // If anything went wrong here, just return false. There is nothing we can do.
                throw new InvalidOperationException("Unable to access the ServicePointTable field");
            }

            Hashtable originalTable = servicePointTableField.GetValue(null) as Hashtable;
            ServicePointHashtable newTable = new ServicePointHashtable(originalTable ?? new Hashtable());

            servicePointTableField.SetValue(null, newTable);
        }

        private static Func<TClass, TField> CreateFieldGetter<TClass, TField>(string fieldName, BindingFlags flags)
            where TClass : class
        {
            FieldInfo field = typeof(TClass).GetField(fieldName, flags);
            if (field != null)
            {
                string methodName = field.ReflectedType.FullName + ".get_" + field.Name;
                DynamicMethod getterMethod = new DynamicMethod(methodName, typeof(TField), new[] { typeof(TClass) }, true);
                ILGenerator generator = getterMethod.GetILGenerator();
                generator.Emit(OpCodes.Ldarg_0);
                generator.Emit(OpCodes.Ldfld, field);
                generator.Emit(OpCodes.Ret);
                return (Func<TClass, TField>)getterMethod.CreateDelegate(typeof(Func<TClass, TField>));
            }

            return null;
        }

        /// <summary>
        /// Creates getter for a field defined in private or internal type
        /// repesented with classType variable.
        /// </summary>
        private static Func<object, TField> CreateFieldGetter<TField>(Type classType, string fieldName, BindingFlags flags)
        {
            FieldInfo field = classType.GetField(fieldName, flags);
            if (field != null)
            {
                string methodName = classType.FullName + ".get_" + field.Name;
                DynamicMethod getterMethod = new DynamicMethod(methodName, typeof(TField), new[] { typeof(object) }, true);
                ILGenerator generator = getterMethod.GetILGenerator();
                generator.Emit(OpCodes.Ldarg_0);
                generator.Emit(OpCodes.Castclass, classType);
                generator.Emit(OpCodes.Ldfld, field);
                generator.Emit(OpCodes.Ret);

                return (Func<object, TField>)getterMethod.CreateDelegate(typeof(Func<object, TField>));
            }

            return null;
        }

        /// <summary>
        /// Creates setter for a field defined in private or internal type
        /// repesented with classType variable.
        /// </summary>
        private static Action<object, TField> CreateFieldSetter<TField>(Type classType, string fieldName, BindingFlags flags)
        {
            FieldInfo field = classType.GetField(fieldName, flags);
            if (field != null)
            {
                string methodName = classType.FullName + ".set_" + field.Name;
                DynamicMethod setterMethod = new DynamicMethod(methodName, null, new[] { typeof(object), typeof(TField) }, true);
                ILGenerator generator = setterMethod.GetILGenerator();
                generator.Emit(OpCodes.Ldarg_0);
                generator.Emit(OpCodes.Castclass, classType);
                generator.Emit(OpCodes.Ldarg_1);
                generator.Emit(OpCodes.Stfld, field);
                generator.Emit(OpCodes.Ret);

                return (Action<object, TField>)setterMethod.CreateDelegate(typeof(Action<object, TField>));
            }

            return null;
        }

        /// <summary>
        /// Creates an "is" method for the private or internal type.
        /// </summary>
        private static Func<object, bool> CreateTypeChecker(Type classType)
        {
            string methodName = classType.FullName + ".typeCheck";
            DynamicMethod setterMethod = new DynamicMethod(methodName, typeof(bool), new[] { typeof(object) }, true);
            ILGenerator generator = setterMethod.GetILGenerator();
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Isinst, classType);
            generator.Emit(OpCodes.Ldnull);
            generator.Emit(OpCodes.Cgt_Un);
            generator.Emit(OpCodes.Ret);

            return (Func<object, bool>)setterMethod.CreateDelegate(typeof(Func<object, bool>));
        }

        /// <summary>
        /// Creates an instance of T using a private or internal ctor.
        /// </summary>
        private static Func<object[], T> CreateTypeInstance<T>(ConstructorInfo constructorInfo)
        {
            Type classType = typeof(T);
            string methodName = classType.FullName + ".ctor";
            DynamicMethod setterMethod = new DynamicMethod(methodName, classType, new Type[] { typeof(object[]) }, true);
            ILGenerator generator = setterMethod.GetILGenerator();

            ParameterInfo[] ctorParams = constructorInfo.GetParameters();
            for (int i = 0; i < ctorParams.Length; i++)
            {
                generator.Emit(OpCodes.Ldarg_0);
                switch (i)
                {
                    case 0: generator.Emit(OpCodes.Ldc_I4_0); break;
                    case 1: generator.Emit(OpCodes.Ldc_I4_1); break;
                    case 2: generator.Emit(OpCodes.Ldc_I4_2); break;
                    case 3: generator.Emit(OpCodes.Ldc_I4_3); break;
                    case 4: generator.Emit(OpCodes.Ldc_I4_4); break;
                    case 5: generator.Emit(OpCodes.Ldc_I4_5); break;
                    case 6: generator.Emit(OpCodes.Ldc_I4_6); break;
                    case 7: generator.Emit(OpCodes.Ldc_I4_7); break;
                    case 8: generator.Emit(OpCodes.Ldc_I4_8); break;
                    default: generator.Emit(OpCodes.Ldc_I4, i); break;
                }

                generator.Emit(OpCodes.Ldelem_Ref);
                Type paramType = ctorParams[i].ParameterType;
                generator.Emit(paramType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, paramType);
            }

            generator.Emit(OpCodes.Newobj, constructorInfo);
            generator.Emit(OpCodes.Ret);

            return (Func<object[], T>)setterMethod.CreateDelegate(typeof(Func<object[], T>));
        }

        private class HashtableWrapper : Hashtable, IEnumerable
        {
            private readonly Hashtable table;

            internal HashtableWrapper(Hashtable table)
                : base()
            {
                this.table = table;
            }

            public override int Count => this.table.Count;

            public override bool IsReadOnly => this.table.IsReadOnly;

            public override bool IsFixedSize => this.table.IsFixedSize;

            public override bool IsSynchronized => this.table.IsSynchronized;

            public override object SyncRoot => this.table.SyncRoot;

            public override ICollection Keys => this.table.Keys;

            public override ICollection Values => this.table.Values;

            public override object this[object key]
            {
                get => this.table[key];
                set => this.table[key] = value;
            }

            public override void Add(object key, object value)
            {
                this.table.Add(key, value);
            }

            public override void Clear()
            {
                this.table.Clear();
            }

            public override bool Contains(object key)
            {
                return this.table.Contains(key);
            }

            public override bool ContainsKey(object key)
            {
                return this.table.ContainsKey(key);
            }

            public override bool ContainsValue(object key)
            {
                return this.table.ContainsValue(key);
            }

            public override void CopyTo(Array array, int arrayIndex)
            {
                this.table.CopyTo(array, arrayIndex);
            }

            public override object Clone()
            {
                return new HashtableWrapper((Hashtable)this.table.Clone());
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return this.table.GetEnumerator();
            }

            public override IDictionaryEnumerator GetEnumerator()
            {
                return this.table.GetEnumerator();
            }

            public override void Remove(object key)
            {
                this.table.Remove(key);
            }
        }

        /// <summary>
        /// Helper class used for ServicePointManager.s_ServicePointTable. The goal here is to
        /// intercept each new ServicePoint object being added to ServicePointManager.s_ServicePointTable
        /// and replace its ConnectionGroupList hashtable field.
        /// </summary>
        private sealed class ServicePointHashtable : HashtableWrapper
        {
            public ServicePointHashtable(Hashtable table)
                : base(table)
            {
            }

            public override object this[object key]
            {
                get => base[key];
                set
                {
                    if (value is WeakReference weakRef && weakRef.IsAlive)
                    {
                        if (weakRef.Target is ServicePoint servicePoint)
                        {
                            // Replace the ConnectionGroup hashtable inside this ServicePoint object,
                            // which allows us to intercept each new ConnectionGroup object added under
                            // this ServicePoint.
                            Hashtable originalTable = connectionGroupListField.GetValue(servicePoint) as Hashtable;
                            ConnectionGroupHashtable newTable = new ConnectionGroupHashtable(originalTable ?? new Hashtable());

                            connectionGroupListField.SetValue(servicePoint, newTable);
                        }
                    }

                    base[key] = value;
                }
            }
        }

        /// <summary>
        /// Helper class used for ServicePoint.m_ConnectionGroupList. The goal here is to
        /// intercept each new ConnectionGroup object being added to ServicePoint.m_ConnectionGroupList
        /// and replace its m_ConnectionList arraylist field.
        /// </summary>
        private sealed class ConnectionGroupHashtable : HashtableWrapper
        {
            public ConnectionGroupHashtable(Hashtable table)
                : base(table)
            {
            }

            public override object this[object key]
            {
                get => base[key];
                set
                {
                    if (connectionGroupType.IsInstanceOfType(value))
                    {
                        // Replace the Connection arraylist inside this ConnectionGroup object,
                        // which allows us to intercept each new Connection object added under
                        // this ConnectionGroup.
                        ArrayList originalArrayList = connectionListField.GetValue(value) as ArrayList;
                        ConnectionArrayList newArrayList = new ConnectionArrayList(originalArrayList ?? new ArrayList());

                        connectionListField.SetValue(value, newArrayList);
                    }

                    base[key] = value;
                }
            }
        }

        /// <summary>
        /// Helper class used to wrap the array list object. This class itself doesn't actually
        /// have the array elements, but rather access another array list that's given at
        /// construction time.
        /// </summary>
        private class ArrayListWrapper : ArrayList
        {
            private ArrayList list;

            internal ArrayListWrapper(ArrayList list)
                : base()
            {
                this.list = list;
            }

            public override int Capacity
            {
                get => this.list.Capacity;
                set => this.list.Capacity = value;
            }

            public override int Count => this.list.Count;

            public override bool IsReadOnly => this.list.IsReadOnly;

            public override bool IsFixedSize => this.list.IsFixedSize;

            public override bool IsSynchronized => this.list.IsSynchronized;

            public override object SyncRoot => this.list.SyncRoot;

            public override object this[int index]
            {
                get => this.list[index];
                set => this.list[index] = value;
            }

            public override int Add(object value)
            {
                return this.list.Add(value);
            }

            public override void AddRange(ICollection c)
            {
                this.list.AddRange(c);
            }

            public override int BinarySearch(object value)
            {
                return this.list.BinarySearch(value);
            }

            public override int BinarySearch(object value, IComparer comparer)
            {
                return this.list.BinarySearch(value, comparer);
            }

            public override int BinarySearch(int index, int count, object value, IComparer comparer)
            {
                return this.list.BinarySearch(index, count, value, comparer);
            }

            public override void Clear()
            {
                this.list.Clear();
            }

            public override object Clone()
            {
                return new ArrayListWrapper((ArrayList)this.list.Clone());
            }

            public override bool Contains(object item)
            {
                return this.list.Contains(item);
            }

            public override void CopyTo(Array array)
            {
                this.list.CopyTo(array);
            }

            public override void CopyTo(Array array, int index)
            {
                this.list.CopyTo(array, index);
            }

            public override void CopyTo(int index, Array array, int arrayIndex, int count)
            {
                this.list.CopyTo(index, array, arrayIndex, count);
            }

            public override IEnumerator GetEnumerator()
            {
                return this.list.GetEnumerator();
            }

            public override IEnumerator GetEnumerator(int index, int count)
            {
                return this.list.GetEnumerator(index, count);
            }

            public override int IndexOf(object value)
            {
                return this.list.IndexOf(value);
            }

            public override int IndexOf(object value, int startIndex)
            {
                return this.list.IndexOf(value, startIndex);
            }

            public override int IndexOf(object value, int startIndex, int count)
            {
                return this.list.IndexOf(value, startIndex, count);
            }

            public override void Insert(int index, object value)
            {
                this.list.Insert(index, value);
            }

            public override void InsertRange(int index, ICollection c)
            {
                this.list.InsertRange(index, c);
            }

            public override int LastIndexOf(object value)
            {
                return this.list.LastIndexOf(value);
            }

            public override int LastIndexOf(object value, int startIndex)
            {
                return this.list.LastIndexOf(value, startIndex);
            }

            public override int LastIndexOf(object value, int startIndex, int count)
            {
                return this.list.LastIndexOf(value, startIndex, count);
            }

            public override void Remove(object value)
            {
                this.list.Remove(value);
            }

            public override void RemoveAt(int index)
            {
                this.list.RemoveAt(index);
            }

            public override void RemoveRange(int index, int count)
            {
                this.list.RemoveRange(index, count);
            }

            public override void Reverse(int index, int count)
            {
                this.list.Reverse(index, count);
            }

            public override void SetRange(int index, ICollection c)
            {
                this.list.SetRange(index, c);
            }

            public override ArrayList GetRange(int index, int count)
            {
                return this.list.GetRange(index, count);
            }

            public override void Sort()
            {
                this.list.Sort();
            }

            public override void Sort(IComparer comparer)
            {
                this.list.Sort(comparer);
            }

            public override void Sort(int index, int count, IComparer comparer)
            {
                this.list.Sort(index, count, comparer);
            }

            public override object[] ToArray()
            {
                return this.list.ToArray();
            }

            public override Array ToArray(Type type)
            {
                return this.list.ToArray(type);
            }

            public override void TrimToSize()
            {
                this.list.TrimToSize();
            }

            public ArrayList Swap()
            {
                ArrayList old = this.list;
                this.list = new ArrayList(old.Capacity);
                return old;
            }
        }

        /// <summary>
        /// Helper class used for ConnectionGroup.m_ConnectionList. The goal here is to
        /// intercept each new Connection object being added to ConnectionGroup.m_ConnectionList
        /// and replace its m_WriteList arraylist field.
        /// </summary>
        private sealed class ConnectionArrayList : ArrayListWrapper
        {
            public ConnectionArrayList(ArrayList list)
                : base(list)
            {
            }

            public override int Add(object value)
            {
                if (connectionType.IsInstanceOfType(value))
                {
                    // Replace the HttpWebRequest arraylist inside this Connection object,
                    // which allows us to intercept each new HttpWebRequest object added under
                    // this Connection.
                    ArrayList originalArrayList = writeListField.GetValue(value) as ArrayList;
                    HttpWebRequestArrayList newArrayList = new HttpWebRequestArrayList(originalArrayList ?? new ArrayList());

                    writeListField.SetValue(value, newArrayList);
                }

                return base.Add(value);
            }
        }

        /// <summary>
        /// Helper class used for Connection.m_WriteList. The goal here is to
        /// intercept all new HttpWebRequest objects being added to Connection.m_WriteList
        /// and notify the listener about the HttpWebRequest that's about to send a request.
        /// It also intercepts all HttpWebRequest objects that are about to get removed from
        /// Connection.m_WriteList as they have completed the request.
        /// </summary>
        private sealed class HttpWebRequestArrayList : ArrayListWrapper
        {
            public HttpWebRequestArrayList(ArrayList list)
                : base(list)
            {
            }

            public override int Add(object value)
            {
                // Add before firing events so if some user code cancels/aborts the request it will be found in the outstanding list.
                int index = base.Add(value);

                if (value is HttpWebRequest request)
                {
                    ProcessRequest(request);
                }

                return index;
            }

            public override void RemoveAt(int index)
            {
                object request = this[index];

                base.RemoveAt(index);

                if (request is HttpWebRequest webRequest)
                {
                    HookOrProcessResult(webRequest);
                }
            }

            public override void Clear()
            {
                ArrayList oldList = this.Swap();
                for (int i = 0; i < oldList.Count; i++)
                {
                    if (oldList[i] is HttpWebRequest request)
                    {
                        HookOrProcessResult(request);
                    }
                }
            }
        }

        /// <summary>
        /// A closure object so our state is available when our callback executes.
        /// </summary>
        private sealed class AsyncCallbackWrapper
        {
            public AsyncCallbackWrapper(HttpWebRequest request, Activity activity, AsyncCallback originalCallback)
            {
                this.Request = request;
                this.Activity = activity;
                this.OriginalCallback = originalCallback;
            }

            public HttpWebRequest Request { get; }

            public Activity Activity { get; }

            public AsyncCallback OriginalCallback { get; }

            public void AsyncCallback(IAsyncResult asyncResult)
            {
                object result = resultAccessor(asyncResult);
                if (result is Exception || result is HttpWebResponse)
                {
                    ProcessResult(asyncResult, this.OriginalCallback, this.Activity, result, false);
                }

                this.OriginalCallback?.Invoke(asyncResult);
            }
        }
    }
}
#endif

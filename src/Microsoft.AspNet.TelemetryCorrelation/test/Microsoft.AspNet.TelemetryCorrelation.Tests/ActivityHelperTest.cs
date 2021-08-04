// <copyright file="ActivityHelperTest.cs" company="Microsoft">
// Copyright (c) .NET Foundation. All rights reserved.
//
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
// </copyright>

namespace Microsoft.AspNet.TelemetryCorrelation.Tests
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Diagnostics;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web;
    using Xunit;

    public class ActivityHelperTest : IDisposable
    {
        private const string TestActivityName = "Activity.Test";
        private readonly List<KeyValuePair<string, string>> baggageItems;
        private readonly string baggageInHeader;
        private IDisposable subscriptionAllListeners;
        private IDisposable subscriptionAspNetListener;

        public ActivityHelperTest()
        {
            this.baggageItems = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("TestKey1", "123"),
                new KeyValuePair<string, string>("TestKey2", "456"),
                new KeyValuePair<string, string>("TestKey1", "789")
            };

            this.baggageInHeader = "TestKey1=123,TestKey2=456,TestKey1=789";

            // reset static fields
            var allListenerField = typeof(DiagnosticListener).
                GetField("s_allListenerObservable", BindingFlags.Static | BindingFlags.NonPublic);
            allListenerField.SetValue(null, null);
            var aspnetListenerField = typeof(ActivityHelper).
                GetField("AspNetListener", BindingFlags.Static | BindingFlags.NonPublic);
            aspnetListenerField.SetValue(null, new DiagnosticListener(ActivityHelper.AspNetListenerName));
        }

        public void Dispose()
        {
            this.subscriptionAspNetListener?.Dispose();
            this.subscriptionAllListeners?.Dispose();
        }

        [Fact]
        public void Can_Restore_Activity()
        {
            this.EnableAll();
            var context = HttpContextHelper.GetFakeHttpContext();
            var rootActivity = ActivityHelper.CreateRootActivity(context, false);
            rootActivity.AddTag("k1", "v1");
            rootActivity.AddTag("k2", "v2");

            Activity.Current = null;

            ActivityHelper.RestoreActivityIfNeeded(context.Items);

            Assert.Same(Activity.Current, rootActivity);
        }

        [Fact]
        public void Can_Stop_Lost_Activity()
        {
            this.EnableAll(pair =>
            {
                Assert.NotNull(Activity.Current);
                Assert.Equal(ActivityHelper.AspNetActivityName, Activity.Current.OperationName);
            });
            var context = HttpContextHelper.GetFakeHttpContext();
            var rootActivity = ActivityHelper.CreateRootActivity(context, false);
            rootActivity.AddTag("k1", "v1");
            rootActivity.AddTag("k2", "v2");

            Activity.Current = null;

            ActivityHelper.StopAspNetActivity(context.Items);
            Assert.True(rootActivity.Duration != TimeSpan.Zero);
            Assert.Null(Activity.Current);
            Assert.Null(context.Items[ActivityHelper.ActivityKey]);
        }

        [Fact]
        public void Can_Not_Stop_Lost_Activity_If_Not_In_Context()
        {
            this.EnableAll(pair =>
            {
                Assert.NotNull(Activity.Current);
                Assert.Equal(ActivityHelper.AspNetActivityName, Activity.Current.OperationName);
            });
            var context = HttpContextHelper.GetFakeHttpContext();
            var rootActivity = ActivityHelper.CreateRootActivity(context, false);
            context.Items.Remove(ActivityHelper.ActivityKey);
            rootActivity.AddTag("k1", "v1");
            rootActivity.AddTag("k2", "v2");

            Activity.Current = null;

            ActivityHelper.StopAspNetActivity(context.Items);
            Assert.True(rootActivity.Duration == TimeSpan.Zero);
            Assert.Null(Activity.Current);
            Assert.Null(context.Items[ActivityHelper.ActivityKey]);
        }

        [Fact]
        public void Do_Not_Restore_Activity_When_There_Is_No_Activity_In_Context()
        {
            this.EnableAll();
            ActivityHelper.RestoreActivityIfNeeded(HttpContextHelper.GetFakeHttpContext().Items);

            Assert.Null(Activity.Current);
        }

        [Fact]
        public void Do_Not_Restore_Activity_When_It_Is_Not_Lost()
        {
            this.EnableAll();
            var root = new Activity("root").Start();

            var context = HttpContextHelper.GetFakeHttpContext();
            context.Items[ActivityHelper.ActivityKey] = root;

            var module = new TelemetryCorrelationHttpModule();

            ActivityHelper.RestoreActivityIfNeeded(context.Items);

            Assert.Equal(root, Activity.Current);
        }

        [Fact]
        public void Can_Stop_Activity_Without_AspNetListener_Enabled()
        {
            var context = HttpContextHelper.GetFakeHttpContext();
            var rootActivity = this.CreateActivity();
            rootActivity.Start();
            context.Items[ActivityHelper.ActivityKey] = rootActivity;
            Thread.Sleep(100);
            ActivityHelper.StopAspNetActivity(context.Items);

            Assert.True(rootActivity.Duration != TimeSpan.Zero);
            Assert.Null(rootActivity.Parent);
            Assert.Null(context.Items[ActivityHelper.ActivityKey]);
        }

        [Fact]
        public void Can_Stop_Activity_With_AspNetListener_Enabled()
        {
            var context = HttpContextHelper.GetFakeHttpContext();
            var rootActivity = this.CreateActivity();
            rootActivity.Start();
            context.Items[ActivityHelper.ActivityKey] = rootActivity;
            Thread.Sleep(100);
            this.EnableAspNetListenerOnly();
            ActivityHelper.StopAspNetActivity(context.Items);

            Assert.True(rootActivity.Duration != TimeSpan.Zero);
            Assert.Null(rootActivity.Parent);
            Assert.Null(context.Items[ActivityHelper.ActivityKey]);
        }

        [Fact]
        public void Can_Stop_Root_Activity_With_All_Children()
        {
            this.EnableAll();
            var context = HttpContextHelper.GetFakeHttpContext();
            var rootActivity = ActivityHelper.CreateRootActivity(context, false);

            var child = new Activity("child").Start();
            new Activity("grandchild").Start();

            ActivityHelper.StopAspNetActivity(context.Items);

            Assert.True(rootActivity.Duration != TimeSpan.Zero);
            Assert.True(child.Duration == TimeSpan.Zero);
            Assert.Null(rootActivity.Parent);
            Assert.Null(context.Items[ActivityHelper.ActivityKey]);
        }

        [Fact]
        public void Can_Stop_Root_While_Child_Is_Current()
        {
            this.EnableAll();
            var context = HttpContextHelper.GetFakeHttpContext();
            var rootActivity = ActivityHelper.CreateRootActivity(context, false);
            var child = new Activity("child").Start();

            ActivityHelper.StopAspNetActivity(context.Items);

            Assert.True(child.Duration == TimeSpan.Zero);
            Assert.Null(Activity.Current);
            Assert.Null(context.Items[ActivityHelper.ActivityKey]);
        }

        [Fact]
        public void OnImportActivity_Is_Called()
        {
            bool onImportIsCalled = false;
            Activity importedActivity = null;
            this.EnableAll(onImport: (activity, _) =>
            {
                onImportIsCalled = true;
                importedActivity = activity;
                Assert.Null(Activity.Current);
            });

            var context = HttpContextHelper.GetFakeHttpContext();
            var rootActivity = ActivityHelper.CreateRootActivity(context, false);
            Assert.True(onImportIsCalled);
            Assert.NotNull(importedActivity);
            Assert.Equal(importedActivity, Activity.Current);
            Assert.Equal(importedActivity, rootActivity);
        }

        [Fact]
        public void OnImportActivity_Can_Set_Parent()
        {
            this.EnableAll(onImport: (activity, _) =>
            {
                Assert.Null(activity.ParentId);
                activity.SetParentId("|guid.123.");
            });

            var context = HttpContextHelper.GetFakeHttpContext();
            var rootActivity = ActivityHelper.CreateRootActivity(context, false);

            Assert.Equal("|guid.123.", Activity.Current.ParentId);
        }

        [Fact]
        public async Task Can_Stop_Root_Activity_If_It_Is_Broken()
        {
            this.EnableAll();
            var context = HttpContextHelper.GetFakeHttpContext();
            var root = ActivityHelper.CreateRootActivity(context, false);
            new Activity("child").Start();

            for (int i = 0; i < 2; i++)
            {
                await Task.Run(() =>
                {
                    // when we enter this method, Current is 'child' activity
                    Activity.Current.Stop();

                    // here Current is 'parent', but only in this execution context
                });
            }

            // when we return back here, in the 'parent' execution context
            // Current is still 'child' activity - changes in child context (inside Task.Run)
            // do not affect 'parent' context in which Task.Run is called.
            // But 'child' Activity is stopped, thus consequent calls to Stop will
            // not update Current
            ActivityHelper.StopAspNetActivity(context.Items);
            Assert.True(root.Duration != TimeSpan.Zero);
            Assert.Null(context.Items[ActivityHelper.ActivityKey]);
            Assert.Null(Activity.Current);
        }

        [Fact]
        public void Stop_Root_Activity_With_129_Nesting_Depth()
        {
            this.EnableAll();
            var context = HttpContextHelper.GetFakeHttpContext();
            var root = ActivityHelper.CreateRootActivity(context, false);

            for (int i = 0; i < 129; i++)
            {
                new Activity("child" + i).Start();
            }

            // can stop any activity regardless of the stack depth
            ActivityHelper.StopAspNetActivity(context.Items);

            Assert.True(root.Duration != TimeSpan.Zero);
            Assert.Null(context.Items[ActivityHelper.ActivityKey]);
            Assert.Null(Activity.Current);
        }

        [Fact]
        public void Should_Not_Create_RootActivity_If_AspNetListener_Not_Enabled()
        {
            var context = HttpContextHelper.GetFakeHttpContext();
            var rootActivity = ActivityHelper.CreateRootActivity(context, true);

            Assert.Null(rootActivity);
        }

        [Fact]
        public void Should_Not_Create_RootActivity_If_AspNetActivity_Not_Enabled()
        {
            var context = HttpContextHelper.GetFakeHttpContext();
            this.EnableAspNetListenerOnly();
            var rootActivity = ActivityHelper.CreateRootActivity(context, true);

            Assert.Null(rootActivity);
        }

        [Fact]
        public void Should_Not_Create_RootActivity_If_AspNetActivity_Not_Enabled_With_Arguments()
        {
            var context = HttpContextHelper.GetFakeHttpContext();
            this.EnableAspNetListenerAndDisableActivity();
            var rootActivity = ActivityHelper.CreateRootActivity(context, true);

            Assert.Null(rootActivity);
        }

        [Fact]
        public void Can_Create_RootActivity_And_Restore_Info_From_Request_Header()
        {
            this.EnableAll();
            var requestHeaders = new Dictionary<string, string>
            {
                { ActivityExtensions.RequestIdHeaderName, "|aba2f1e978b2cab6.1." },
                { ActivityExtensions.CorrelationContextHeaderName, this.baggageInHeader }
            };

            var context = HttpContextHelper.GetFakeHttpContext(headers: requestHeaders);
            this.EnableAspNetListenerAndActivity();
            var rootActivity = ActivityHelper.CreateRootActivity(context, true);

            Assert.NotNull(rootActivity);
            Assert.True(rootActivity.ParentId == "|aba2f1e978b2cab6.1.");
            var expectedBaggage = this.baggageItems.OrderBy(item => item.Value);
            var actualBaggage = rootActivity.Baggage.OrderBy(item => item.Value);
            Assert.Equal(expectedBaggage, actualBaggage);
        }

        [Fact]
        public void Can_Create_RootActivity_From_W3C_Traceparent()
        {
            this.EnableAll();
            var requestHeaders = new Dictionary<string, string>
            {
                { ActivityExtensions.TraceparentHeaderName, "00-0123456789abcdef0123456789abcdef-0123456789abcdef-00" },
            };

            var context = HttpContextHelper.GetFakeHttpContext(headers: requestHeaders);
            this.EnableAspNetListenerAndActivity();
            var rootActivity = ActivityHelper.CreateRootActivity(context, true);

            Assert.NotNull(rootActivity);
            Assert.Equal(ActivityIdFormat.W3C, rootActivity.IdFormat);
            Assert.Equal("00-0123456789abcdef0123456789abcdef-0123456789abcdef-00", rootActivity.ParentId);
            Assert.Equal("0123456789abcdef0123456789abcdef", rootActivity.TraceId.ToHexString());
            Assert.Equal("0123456789abcdef", rootActivity.ParentSpanId.ToHexString());
            Assert.False(rootActivity.Recorded);

            Assert.Null(rootActivity.TraceStateString);
            Assert.Empty(rootActivity.Baggage);
        }

        [Fact]
        public void Can_Create_RootActivityWithTraceState_From_W3C_TraceContext()
        {
            this.EnableAll();
            var requestHeaders = new Dictionary<string, string>
            {
                { ActivityExtensions.TraceparentHeaderName, "00-0123456789abcdef0123456789abcdef-0123456789abcdef-01" },
                { ActivityExtensions.TracestateHeaderName, "ts1=v1,ts2=v2" },
            };

            var context = HttpContextHelper.GetFakeHttpContext(headers: requestHeaders);
            this.EnableAspNetListenerAndActivity();
            var rootActivity = ActivityHelper.CreateRootActivity(context, true);

            Assert.NotNull(rootActivity);
            Assert.Equal(ActivityIdFormat.W3C, rootActivity.IdFormat);
            Assert.Equal("00-0123456789abcdef0123456789abcdef-0123456789abcdef-01", rootActivity.ParentId);
            Assert.Equal("0123456789abcdef0123456789abcdef", rootActivity.TraceId.ToHexString());
            Assert.Equal("0123456789abcdef", rootActivity.ParentSpanId.ToHexString());
            Assert.True(rootActivity.Recorded);

            Assert.Equal("ts1=v1,ts2=v2", rootActivity.TraceStateString);
            Assert.Empty(rootActivity.Baggage);
        }

        [Fact]
        public void Can_Create_RootActivity_And_Ignore_Info_From_Request_Header_If_ParseHeaders_Is_False()
        {
            this.EnableAll();
            var requestHeaders = new Dictionary<string, string>
            {
                { ActivityExtensions.RequestIdHeaderName, "|aba2f1e978b2cab6.1." },
                { ActivityExtensions.CorrelationContextHeaderName, this.baggageInHeader }
            };

            var context = HttpContextHelper.GetFakeHttpContext(headers: requestHeaders);
            this.EnableAspNetListenerAndActivity();
            var rootActivity = ActivityHelper.CreateRootActivity(context, parseHeaders: false);

            Assert.NotNull(rootActivity);
            Assert.Null(rootActivity.ParentId);
            Assert.Empty(rootActivity.Baggage);
        }

        [Fact]
        public void Can_Create_RootActivity_And_Start_Activity()
        {
            this.EnableAll();
            var context = HttpContextHelper.GetFakeHttpContext();
            this.EnableAspNetListenerAndActivity();
            var rootActivity = ActivityHelper.CreateRootActivity(context, true);

            Assert.NotNull(rootActivity);
            Assert.True(!string.IsNullOrEmpty(rootActivity.Id));
        }

        [Fact]
        public void Can_Create_RootActivity_And_Saved_In_HttContext()
        {
            this.EnableAll();
            var context = HttpContextHelper.GetFakeHttpContext();
            this.EnableAspNetListenerAndActivity();
            var rootActivity = ActivityHelper.CreateRootActivity(context, true);

            Assert.NotNull(rootActivity);
            Assert.Same(rootActivity, context.Items[ActivityHelper.ActivityKey]);
        }

        private Activity CreateActivity()
        {
            var activity = new Activity(TestActivityName);
            this.baggageItems.ForEach(kv => activity.AddBaggage(kv.Key, kv.Value));

            return activity;
        }

        private void EnableAll(Action<KeyValuePair<string, object>> onNext = null, Action<Activity, object> onImport = null)
        {
            this.subscriptionAllListeners = DiagnosticListener.AllListeners.Subscribe(listener =>
            {
                // if AspNetListener has subscription, then it is enabled
                if (listener.Name == ActivityHelper.AspNetListenerName)
                {
                    this.subscriptionAspNetListener = listener.Subscribe(
                        new TestDiagnosticListener(onNext),
                        (name, a1, a2) => true,
                        (a, o) => onImport?.Invoke(a, o),
                        (a, o) => { });
                }
            });
        }

        private void EnableAspNetListenerAndDisableActivity(
            Action<KeyValuePair<string, object>> onNext = null,
            string activityName = ActivityHelper.AspNetActivityName)
        {
            this.subscriptionAllListeners = DiagnosticListener.AllListeners.Subscribe(listener =>
            {
                // if AspNetListener has subscription, then it is enabled
                if (listener.Name == ActivityHelper.AspNetListenerName)
                {
                    this.subscriptionAspNetListener = listener.Subscribe(
                        new TestDiagnosticListener(onNext),
                        (name, arg1, arg2) => name == activityName && arg1 == null);
                }
            });
        }

        private void EnableAspNetListenerAndActivity(
            Action<KeyValuePair<string, object>> onNext = null,
            string activityName = ActivityHelper.AspNetActivityName)
        {
            this.subscriptionAllListeners = DiagnosticListener.AllListeners.Subscribe(listener =>
            {
                // if AspNetListener has subscription, then it is enabled
                if (listener.Name == ActivityHelper.AspNetListenerName)
                {
                    this.subscriptionAspNetListener = listener.Subscribe(
                        new TestDiagnosticListener(onNext),
                        (name, arg1, arg2) => name == activityName);
                }
            });
        }

        private void EnableAspNetListenerOnly(Action<KeyValuePair<string, object>> onNext = null)
        {
            this.subscriptionAllListeners = DiagnosticListener.AllListeners.Subscribe(listener =>
            {
                // if AspNetListener has subscription, then it is enabled
                if (listener.Name == ActivityHelper.AspNetListenerName)
                {
                    this.subscriptionAspNetListener = listener.Subscribe(
                        new TestDiagnosticListener(onNext),
                        activityName => false);
                }
            });
        }

        private class TestHttpRequest : HttpRequestBase
        {
            private readonly NameValueCollection headers = new NameValueCollection();

            public override NameValueCollection Headers => this.headers;

            public override UnvalidatedRequestValuesBase Unvalidated => new TestUnvalidatedRequestValues(this.headers);
        }

        private class TestUnvalidatedRequestValues : UnvalidatedRequestValuesBase
        {
            public TestUnvalidatedRequestValues(NameValueCollection headers)
            {
                this.Headers = headers;
            }

            public override NameValueCollection Headers { get; }
        }

        private class TestHttpResponse : HttpResponseBase
        {
        }

        private class TestHttpServerUtility : HttpServerUtilityBase
        {
            private readonly HttpContextBase context;

            public TestHttpServerUtility(HttpContextBase context)
            {
                this.context = context;
            }

            public override Exception GetLastError()
            {
                return this.context.Error;
            }
        }

        private class TestHttpContext : HttpContextBase
        {
            private readonly Hashtable items;

            public TestHttpContext(Exception error = null)
            {
                this.Server = new TestHttpServerUtility(this);
                this.items = new Hashtable();
                this.Error = error;
            }

            public override HttpRequestBase Request { get; } = new TestHttpRequest();

            /// <inheritdoc />
            public override IDictionary Items => this.items;

            public override Exception Error { get; }

            public override HttpServerUtilityBase Server { get; }
        }
    }
}

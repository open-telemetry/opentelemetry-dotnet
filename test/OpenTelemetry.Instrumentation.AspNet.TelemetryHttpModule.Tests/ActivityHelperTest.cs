// <copyright file="ActivityHelperTest.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Instrumentation.AspNet.Tests
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web;
    using OpenTelemetry.Context.Propagation;
    using Xunit;

    public class ActivityHelperTest : IDisposable
    {
        private const string TraceParentHeaderName = "traceparent";
        private const string TraceStateHeaderName = "tracestate";
        private const string BaggageHeaderName = "baggage";
        private const string BaggageInHeader = "TestKey1=123,TestKey2=456,TestKey1=789";
        private const string TestActivityName = "Activity.Test";
        private readonly TextMapPropagator noopTextMapPropagator = new NoopTextMapPropagator();
        private ActivityListener activitySourceListener;

        public void Dispose()
        {
            this.activitySourceListener?.Dispose();
        }

        [Fact]
        public void Has_Started_Returns_Correctly()
        {
            var context = HttpContextHelper.GetFakeHttpContext();

            bool result = ActivityHelper.HasStarted(context, out Activity aspNetActivity);

            Assert.False(result);
            Assert.Null(aspNetActivity);

            context.Items[ActivityHelper.ContextKey] = ActivityHelper.StartedButNotSampledObj;

            result = ActivityHelper.HasStarted(context, out aspNetActivity);

            Assert.True(result);
            Assert.Null(aspNetActivity);

            Activity activity = new Activity(TestActivityName);
            context.Items[ActivityHelper.ContextKey] = new ActivityHelper.ContextHolder { Activity = activity };

            result = ActivityHelper.HasStarted(context, out aspNetActivity);

            Assert.True(result);
            Assert.NotNull(aspNetActivity);
            Assert.Equal(activity, aspNetActivity);
        }

        [Fact]
        public async Task Can_Restore_Activity()
        {
            this.EnableListener();
            var context = HttpContextHelper.GetFakeHttpContext();
            using var rootActivity = ActivityHelper.StartAspNetActivity(this.noopTextMapPropagator, context, null);
            rootActivity.AddTag("k1", "v1");
            rootActivity.AddTag("k2", "v2");

            Task testTask;
            using (ExecutionContext.SuppressFlow())
            {
                testTask = Task.Run(() =>
                {
                    Task.Yield();

                    Assert.Null(Activity.Current);

                    ActivityHelper.RestoreContextIfNeeded(context);

                    Assert.Same(Activity.Current, rootActivity);
                });
            }

            await testTask.ConfigureAwait(false);
        }

        [Fact(Skip = "Temporarily disable until stable.")]
        public async Task Can_Restore_Baggage()
        {
            this.EnableListener();

            var requestHeaders = new Dictionary<string, string>
            {
                { BaggageHeaderName, BaggageInHeader },
            };

            var context = HttpContextHelper.GetFakeHttpContext(headers: requestHeaders);
            using var rootActivity = ActivityHelper.StartAspNetActivity(new CompositeTextMapPropagator(new TextMapPropagator[] { new TraceContextPropagator(), new BaggagePropagator() }), context, null);

            rootActivity.AddTag("k1", "v1");
            rootActivity.AddTag("k2", "v2");

            Task testTask;
            using (ExecutionContext.SuppressFlow())
            {
                testTask = Task.Run(() =>
                {
                    Task.Yield();

                    Assert.Null(Activity.Current);
                    Assert.Equal(0, Baggage.Current.Count);

                    ActivityHelper.RestoreContextIfNeeded(context);

                    Assert.Same(Activity.Current, rootActivity);
                    Assert.Empty(rootActivity.Baggage);

                    Assert.Equal(2, Baggage.Current.Count);
                    Assert.Equal("789", Baggage.Current.GetBaggage("TestKey1"));
                    Assert.Equal("456", Baggage.Current.GetBaggage("TestKey2"));
                });
            }

            await testTask.ConfigureAwait(false);
        }

        [Fact]
        public void Can_Stop_Lost_Activity()
        {
            this.EnableListener(a =>
            {
                Assert.NotNull(Activity.Current);
                Assert.Equal(Activity.Current, a);
                Assert.Equal(TelemetryHttpModule.AspNetActivityName, Activity.Current.OperationName);
            });
            var context = HttpContextHelper.GetFakeHttpContext();
            using var rootActivity = ActivityHelper.StartAspNetActivity(this.noopTextMapPropagator, context, null);
            rootActivity.AddTag("k1", "v1");
            rootActivity.AddTag("k2", "v2");

            Activity.Current = null;

            ActivityHelper.StopAspNetActivity(this.noopTextMapPropagator, rootActivity, context, null);
            Assert.True(rootActivity.Duration != TimeSpan.Zero);
            Assert.Null(Activity.Current);
            Assert.Null(context.Items[ActivityHelper.ContextKey]);
        }

        [Fact]
        public void Do_Not_Restore_Activity_When_There_Is_No_Activity_In_Context()
        {
            this.EnableListener();
            ActivityHelper.RestoreContextIfNeeded(HttpContextHelper.GetFakeHttpContext());

            Assert.Null(Activity.Current);
        }

        [Fact]
        public void Do_Not_Restore_Activity_When_It_Is_Not_Lost()
        {
            this.EnableListener();
            var root = new Activity("root").Start();

            var context = HttpContextHelper.GetFakeHttpContext();
            context.Items[ActivityHelper.ContextKey] = new ActivityHelper.ContextHolder { Activity = root };

            ActivityHelper.RestoreContextIfNeeded(context);

            Assert.Equal(root, Activity.Current);
        }

        [Fact]
        public void Can_Stop_Activity_Without_AspNetListener_Enabled()
        {
            var context = HttpContextHelper.GetFakeHttpContext();
            var rootActivity = new Activity(TestActivityName);
            rootActivity.Start();
            context.Items[ActivityHelper.ContextKey] = new ActivityHelper.ContextHolder { Activity = rootActivity };
            Thread.Sleep(100);
            ActivityHelper.StopAspNetActivity(this.noopTextMapPropagator, rootActivity, context, null);

            Assert.True(rootActivity.Duration != TimeSpan.Zero);
            Assert.Null(rootActivity.Parent);
            Assert.Null(context.Items[ActivityHelper.ContextKey]);
        }

        [Fact]
        public void Can_Stop_Activity_With_AspNetListener_Enabled()
        {
            var context = HttpContextHelper.GetFakeHttpContext();
            var rootActivity = new Activity(TestActivityName);
            rootActivity.Start();
            context.Items[ActivityHelper.ContextKey] = new ActivityHelper.ContextHolder { Activity = rootActivity };
            Thread.Sleep(100);
            this.EnableListener();
            ActivityHelper.StopAspNetActivity(this.noopTextMapPropagator, rootActivity, context, null);

            Assert.True(rootActivity.Duration != TimeSpan.Zero);
            Assert.Null(rootActivity.Parent);
            Assert.Null(context.Items[ActivityHelper.ContextKey]);
        }

        [Fact]
        public void Can_Stop_Root_Activity_With_All_Children()
        {
            this.EnableListener();
            var context = HttpContextHelper.GetFakeHttpContext();
            using var rootActivity = ActivityHelper.StartAspNetActivity(this.noopTextMapPropagator, context, null);

            var child = new Activity("child").Start();
            new Activity("grandchild").Start();

            ActivityHelper.StopAspNetActivity(this.noopTextMapPropagator, rootActivity, context, null);

            Assert.True(rootActivity.Duration != TimeSpan.Zero);
            Assert.True(child.Duration == TimeSpan.Zero);
            Assert.Null(rootActivity.Parent);
            Assert.Null(context.Items[ActivityHelper.ContextKey]);
        }

        [Fact]
        public void Can_Stop_Root_While_Child_Is_Current()
        {
            this.EnableListener();
            var context = HttpContextHelper.GetFakeHttpContext();
            using var rootActivity = ActivityHelper.StartAspNetActivity(this.noopTextMapPropagator, context, null);
            var child = new Activity("child").Start();

            ActivityHelper.StopAspNetActivity(this.noopTextMapPropagator, rootActivity, context, null);

            Assert.True(child.Duration == TimeSpan.Zero);
            Assert.NotNull(Activity.Current);
            Assert.Equal(Activity.Current, child);
            Assert.Null(context.Items[ActivityHelper.ContextKey]);
        }

        [Fact]
        public async Task Can_Stop_Root_Activity_If_It_Is_Broken()
        {
            this.EnableListener();
            var context = HttpContextHelper.GetFakeHttpContext();
            using var root = ActivityHelper.StartAspNetActivity(this.noopTextMapPropagator, context, null);
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
            ActivityHelper.StopAspNetActivity(this.noopTextMapPropagator, root, context, null);
            Assert.True(root.Duration != TimeSpan.Zero);
            Assert.Null(context.Items[ActivityHelper.ContextKey]);
            Assert.Null(Activity.Current);
        }

        [Fact]
        public void Stop_Root_Activity_With_129_Nesting_Depth()
        {
            this.EnableListener();
            var context = HttpContextHelper.GetFakeHttpContext();
            using var root = ActivityHelper.StartAspNetActivity(this.noopTextMapPropagator, context, null);

            for (int i = 0; i < 129; i++)
            {
                new Activity("child" + i).Start();
            }

            // can stop any activity regardless of the stack depth
            ActivityHelper.StopAspNetActivity(this.noopTextMapPropagator, root, context, null);

            Assert.True(root.Duration != TimeSpan.Zero);
            Assert.Null(context.Items[ActivityHelper.ContextKey]);
            Assert.NotNull(Activity.Current);
        }

        [Fact]
        public void Should_Not_Create_RootActivity_If_AspNetListener_Not_Enabled()
        {
            var context = HttpContextHelper.GetFakeHttpContext();
            using var rootActivity = ActivityHelper.StartAspNetActivity(this.noopTextMapPropagator, context, null);

            Assert.Null(rootActivity);
            Assert.Equal(ActivityHelper.StartedButNotSampledObj, context.Items[ActivityHelper.ContextKey]);

            ActivityHelper.StopAspNetActivity(this.noopTextMapPropagator, rootActivity, context, null);
            Assert.Null(context.Items[ActivityHelper.ContextKey]);
        }

        [Fact]
        public void Should_Not_Create_RootActivity_If_AspNetActivity_Not_Enabled()
        {
            var context = HttpContextHelper.GetFakeHttpContext();
            this.EnableListener(onSample: (context) => ActivitySamplingResult.None);
            using var rootActivity = ActivityHelper.StartAspNetActivity(this.noopTextMapPropagator, context, null);

            Assert.Null(rootActivity);
            Assert.Equal(ActivityHelper.StartedButNotSampledObj, context.Items[ActivityHelper.ContextKey]);

            ActivityHelper.StopAspNetActivity(this.noopTextMapPropagator, rootActivity, context, null);
            Assert.Null(context.Items[ActivityHelper.ContextKey]);
        }

        [Fact]
        public void Can_Create_RootActivity_From_W3C_Traceparent()
        {
            this.EnableListener();
            var requestHeaders = new Dictionary<string, string>
            {
                { TraceParentHeaderName, "00-0123456789abcdef0123456789abcdef-0123456789abcdef-00" },
            };

            var context = HttpContextHelper.GetFakeHttpContext(headers: requestHeaders);
            using var rootActivity = ActivityHelper.StartAspNetActivity(new TraceContextPropagator(), context, null);

            Assert.NotNull(rootActivity);
            Assert.Equal(ActivityIdFormat.W3C, rootActivity.IdFormat);
            Assert.Equal("00-0123456789abcdef0123456789abcdef-0123456789abcdef-00", rootActivity.ParentId);
            Assert.Equal("0123456789abcdef0123456789abcdef", rootActivity.TraceId.ToHexString());
            Assert.Equal("0123456789abcdef", rootActivity.ParentSpanId.ToHexString());
            Assert.True(rootActivity.Recorded); // note: We're not using a parent-based sampler in this test so the recorded flag of traceparent is ignored.

            Assert.Null(rootActivity.TraceStateString);
            Assert.Empty(rootActivity.Baggage);

            Assert.Equal(0, Baggage.Current.Count);
        }

        [Fact]
        public void Can_Create_RootActivityWithTraceState_From_W3C_TraceContext()
        {
            this.EnableListener();
            var requestHeaders = new Dictionary<string, string>
            {
                { TraceParentHeaderName, "00-0123456789abcdef0123456789abcdef-0123456789abcdef-01" },
                { TraceStateHeaderName, "ts1=v1,ts2=v2" },
            };

            var context = HttpContextHelper.GetFakeHttpContext(headers: requestHeaders);
            using var rootActivity = ActivityHelper.StartAspNetActivity(new TraceContextPropagator(), context, null);

            Assert.NotNull(rootActivity);
            Assert.Equal(ActivityIdFormat.W3C, rootActivity.IdFormat);
            Assert.Equal("00-0123456789abcdef0123456789abcdef-0123456789abcdef-01", rootActivity.ParentId);
            Assert.Equal("0123456789abcdef0123456789abcdef", rootActivity.TraceId.ToHexString());
            Assert.Equal("0123456789abcdef", rootActivity.ParentSpanId.ToHexString());
            Assert.True(rootActivity.Recorded);

            Assert.Equal("ts1=v1,ts2=v2", rootActivity.TraceStateString);
            Assert.Empty(rootActivity.Baggage);

            Assert.Equal(0, Baggage.Current.Count);
        }

        [Fact]
        public void Can_Create_RootActivity_From_W3C_Traceparent_With_Baggage()
        {
            this.EnableListener();
            var requestHeaders = new Dictionary<string, string>
            {
                { TraceParentHeaderName, "00-0123456789abcdef0123456789abcdef-0123456789abcdef-00" },
                { BaggageHeaderName, BaggageInHeader },
            };

            var context = HttpContextHelper.GetFakeHttpContext(headers: requestHeaders);
            using var rootActivity = ActivityHelper.StartAspNetActivity(new CompositeTextMapPropagator(new TextMapPropagator[] { new TraceContextPropagator(), new BaggagePropagator() }), context, null);

            Assert.NotNull(rootActivity);
            Assert.Equal(ActivityIdFormat.W3C, rootActivity.IdFormat);
            Assert.Equal("00-0123456789abcdef0123456789abcdef-0123456789abcdef-00", rootActivity.ParentId);
            Assert.Equal("0123456789abcdef0123456789abcdef", rootActivity.TraceId.ToHexString());
            Assert.Equal("0123456789abcdef", rootActivity.ParentSpanId.ToHexString());
            Assert.True(rootActivity.Recorded); // note: We're not using a parent-based sampler in this test so the recorded flag of traceparent is ignored.

            Assert.Null(rootActivity.TraceStateString);
            Assert.Empty(rootActivity.Baggage);

            Assert.Equal(2, Baggage.Current.Count);
            Assert.Equal("789", Baggage.Current.GetBaggage("TestKey1"));
            Assert.Equal("456", Baggage.Current.GetBaggage("TestKey2"));

            ActivityHelper.StopAspNetActivity(this.noopTextMapPropagator, rootActivity, context, null);

            Assert.Equal(0, Baggage.Current.Count);
        }

        [Fact]
        public void Can_Create_RootActivity_And_Start_Activity()
        {
            this.EnableListener();
            var context = HttpContextHelper.GetFakeHttpContext();
            using var rootActivity = ActivityHelper.StartAspNetActivity(this.noopTextMapPropagator, context, null);

            Assert.NotNull(rootActivity);
            Assert.True(!string.IsNullOrEmpty(rootActivity.Id));
        }

        [Fact]
        public void Can_Create_RootActivity_And_Saved_In_HttContext()
        {
            this.EnableListener();
            var context = HttpContextHelper.GetFakeHttpContext();
            using var rootActivity = ActivityHelper.StartAspNetActivity(this.noopTextMapPropagator, context, null);

            Assert.NotNull(rootActivity);
            Assert.Same(rootActivity, ((ActivityHelper.ContextHolder)context.Items[ActivityHelper.ContextKey])?.Activity);
        }

        [Fact]
        public void Fire_Exception_Events()
        {
            int callbacksFired = 0;

            var context = HttpContextHelper.GetFakeHttpContext();

            Activity activity = new Activity(TestActivityName);

            ActivityHelper.WriteActivityException(activity, context, new InvalidOperationException(), (a, c, e) => { callbacksFired++; });

            ActivityHelper.WriteActivityException(null, context, new InvalidOperationException(), (a, c, e) => { callbacksFired++; });

            // Callback should fire only for non-null activity
            Assert.Equal(1, callbacksFired);
        }

        private void EnableListener(Action<Activity> onStarted = null, Action<Activity> onStopped = null, Func<ActivityContext, ActivitySamplingResult> onSample = null)
        {
            Debug.Assert(this.activitySourceListener == null, "Cannot attach multiple listeners in tests.");

            this.activitySourceListener = new ActivityListener
            {
                ShouldListenTo = (activitySource) => activitySource.Name == TelemetryHttpModule.AspNetSourceName,
                ActivityStarted = (a) => onStarted?.Invoke(a),
                ActivityStopped = (a) => onStopped?.Invoke(a),
                Sample = (ref ActivityCreationOptions<ActivityContext> options) =>
                {
                    if (onSample != null)
                    {
                        return onSample(options.Parent);
                    }

                    return ActivitySamplingResult.AllDataAndRecorded;
                },
            };

            ActivitySource.AddActivityListener(this.activitySourceListener);
        }

        private class TestHttpRequest : HttpRequestBase
        {
            private readonly NameValueCollection headers = new();

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

        private class NoopTextMapPropagator : TextMapPropagator
        {
            private static readonly PropagationContext DefaultPropagationContext = default;

            public override ISet<string> Fields => null;

            public override PropagationContext Extract<T>(PropagationContext context, T carrier, Func<T, string, IEnumerable<string>> getter)
            {
                return DefaultPropagationContext;
            }

            public override void Inject<T>(PropagationContext context, T carrier, Action<T, string, string> setter)
            {
            }
        }
    }
}

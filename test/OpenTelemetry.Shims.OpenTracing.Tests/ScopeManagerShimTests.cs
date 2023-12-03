// <copyright file="ScopeManagerShimTests.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics;
using OpenTelemetry.Shims.OpenTracing.Tests.Mock;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Shims.OpenTracing.Tests;

[Collection(nameof(ListenAndSampleAllActivitySources))]
public class ScopeManagerShimTests
{
    private const string SpanName = "MySpanName/1";
    private const string TracerName = "defaultactivitysource";

    [Fact]
    public void Active_IsNull()
    {
        var shim = new ScopeManagerShim();

        Assert.Null(Activity.Current);
        Assert.Null(shim.Active);
    }

    [Fact]
    public void Active_IsNotNull()
    {
        var tracer = TracerProvider.Default.GetTracer(TracerName);
        var shim = new ScopeManagerShim();
        var openTracingSpan = new SpanShim(tracer.StartSpan(SpanName));

        var scope = shim.Activate(openTracingSpan, true);
        Assert.NotNull(scope);

        var activeScope = shim.Active;
        Assert.Equal(scope.Span.Context.SpanId, activeScope.Span.Context.SpanId);
        openTracingSpan.Finish();
    }

    [Fact]
    public void Activate_SpanMustBeShim()
    {
        var shim = new ScopeManagerShim();

        Assert.Throws<InvalidCastException>(() => shim.Activate(new MockSpan(), true));
    }

    [Fact]
    public void Activate()
    {
        var tracer = TracerProvider.Default.GetTracer(TracerName);
        var shim = new ScopeManagerShim();
        var spanShim = new SpanShim(tracer.StartSpan(SpanName));

        using (shim.Activate(spanShim, true))
        {
#if DEBUG
            Assert.Equal(1, shim.SpanScopeTableCount);
#endif
        }

#if DEBUG
        Assert.Equal(0, shim.SpanScopeTableCount);
#endif

        spanShim.Finish();
        Assert.NotEqual(default, spanShim.Span.Activity.Duration);
    }
}

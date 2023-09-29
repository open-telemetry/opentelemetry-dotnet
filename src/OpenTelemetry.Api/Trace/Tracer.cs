// <copyright file="Tracer.cs" company="OpenTelemetry Authors">
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

#nullable enable

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace OpenTelemetry.Trace;

/// <summary>
/// Tracer is the class responsible for creating <see cref="TelemetrySpan"/>.
/// </summary>
/// <remarks>Tracer is a wrapper around <see cref="System.Diagnostics.ActivitySource"/> class.</remarks>
public class Tracer
{
    internal ActivitySource? ActivitySource;

    internal Tracer(ActivitySource activitySource)
    {
        this.ActivitySource = activitySource;
    }

    [Flags]
    private enum StartSpanBehaviors
    {
        ActivateNewSpan = 0b1,
        DeactivateNewSpan = 0b10,
        NewSpanAsRoot = 0b100,
    }

    /// <summary>
    /// Gets the current span from the context.
    /// </summary>
    public static TelemetrySpan CurrentSpan
    {
        get
        {
            var currentActivity = Activity.Current;
            if (currentActivity == null)
            {
                return TelemetrySpan.NoopInstance;
            }
            else
            {
                return new TelemetrySpan(currentActivity);
            }
        }
    }

    /// <summary>
    /// Sets the given span as the current one in the context.
    /// </summary>
    /// <param name="span">The span to be made current.</param>
    /// <returns>The supplied span for call chaining.</returns>
#if NET6_0_OR_GREATER
    [return: NotNullIfNotNull(nameof(span))]
#endif
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TelemetrySpan? WithSpan(TelemetrySpan? span)
    {
        span?.Activate();
        return span;
    }

    /// <summary>
    /// Starts root span.
    /// </summary>
    /// <param name="name">Span name.</param>
    /// <param name="kind">Kind.</param>
    /// <param name="initialAttributes">Initial attributes for the span.</param>
    /// <param name="links"> <see cref="Link"/> for the span.</param>
    /// <param name="startTime"> Start time for the span.</param>
    /// <returns>Span instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TelemetrySpan StartRootSpan(
        string name,
        SpanKind kind = SpanKind.Internal,
        SpanAttributes? initialAttributes = null,
        IEnumerable<Link>? links = null,
        DateTimeOffset startTime = default)
    {
        return this.StartSpanHelper(StartSpanBehaviors.NewSpanAsRoot | StartSpanBehaviors.DeactivateNewSpan, name, kind, default, initialAttributes, links, startTime);
    }

    /// <summary>
    /// Starts a span and does not make it as current span.
    /// </summary>
    /// <param name="name">Span name.</param>
    /// <param name="kind">Kind.</param>
    /// <param name="parentSpan">Parent for new span.</param>
    /// <param name="initialAttributes">Initial attributes for the span.</param>
    /// <param name="links"> <see cref="Link"/> for the span.</param>
    /// <param name="startTime"> Start time for the span.</param>
    /// <returns>Span instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SuppressMessage("roslyn", "RS0026", Justification = "TODO: fix APIs that violate the backcompt requirement - multiple overloads with optional parameters: https://github.com/dotnet/roslyn/blob/main/docs/Adding%20Optional%20Parameters%20in%20Public%20API.md.")]
    public TelemetrySpan StartSpan(
        string name,
        SpanKind kind,
        in TelemetrySpan? parentSpan, // <- TODO: Clean this up if we ever do a major release w/ breaking changes. The "in" here was probably a copy/paste mistake. Removing would be source compatible but binary breaking.
        SpanAttributes? initialAttributes = null,
        IEnumerable<Link>? links = null,
        DateTimeOffset startTime = default)
    {
        return this.StartSpan(name, kind, parentSpan?.Context ?? default, initialAttributes, links, startTime);
    }

    /// <summary>
    /// Starts a span and does not make it as current span.
    /// </summary>
    /// <param name="name">Span name.</param>
    /// <param name="kind">Kind.</param>
    /// <param name="parentContext">Parent Context for new span.</param>
    /// <param name="initialAttributes">Initial attributes for the span.</param>
    /// <param name="links"> <see cref="Link"/> for the span.</param>
    /// <param name="startTime"> Start time for the span.</param>
    /// <returns>Span instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SuppressMessage("roslyn", "RS0026", Justification = "TODO: fix APIs that violate the backcompt requirement - multiple overloads with optional parameters: https://github.com/dotnet/roslyn/blob/main/docs/Adding%20Optional%20Parameters%20in%20Public%20API.md.")]
    public TelemetrySpan StartSpan(
        string name,
        SpanKind kind = SpanKind.Internal,
        in SpanContext parentContext = default,
        SpanAttributes? initialAttributes = null,
        IEnumerable<Link>? links = null,
        DateTimeOffset startTime = default)
    {
        return this.StartSpanHelper(StartSpanBehaviors.DeactivateNewSpan, name, kind, in parentContext, initialAttributes, links, startTime);
    }

    /// <summary>
    /// Starts a span and make it the current active span.
    /// </summary>
    /// <param name="name">Span name.</param>
    /// <param name="kind">Kind.</param>
    /// <param name="parentSpan">Parent for new span.</param>
    /// <param name="initialAttributes">Initial attributes for the span.</param>
    /// <param name="links"> <see cref="Link"/> for the span.</param>
    /// <param name="startTime"> Start time for the span.</param>
    /// <returns>Span instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SuppressMessage("roslyn", "RS0026", Justification = "TODO: fix APIs that violate the backcompt requirement - multiple overloads with optional parameters: https://github.com/dotnet/roslyn/blob/main/docs/Adding%20Optional%20Parameters%20in%20Public%20API.md.")]
    public TelemetrySpan StartActiveSpan(
        string name,
        SpanKind kind,
        in TelemetrySpan? parentSpan, // <- TODO: Clean this up if we ever do a major release w/ breaking changes. The "in" here was probably a copy/paste mistake. Removing would be source compatible but binary breaking.
        SpanAttributes? initialAttributes = null,
        IEnumerable<Link>? links = null,
        DateTimeOffset startTime = default)
    {
        return this.StartActiveSpan(name, kind, parentSpan?.Context ?? default, initialAttributes, links, startTime);
    }

    /// <summary>
    /// Starts a span and make it the current active span.
    /// </summary>
    /// <param name="name">Span name.</param>
    /// <param name="kind">Kind.</param>
    /// <param name="parentContext">Parent Context for new span.</param>
    /// <param name="initialAttributes">Initial attributes for the span.</param>
    /// <param name="links"> <see cref="Link"/> for the span.</param>
    /// <param name="startTime"> Start time for the span.</param>
    /// <returns>Span instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SuppressMessage("roslyn", "RS0026", Justification = "TODO: fix APIs that violate the backcompt requirement - multiple overloads with optional parameters: https://github.com/dotnet/roslyn/blob/main/docs/Adding%20Optional%20Parameters%20in%20Public%20API.md.")]
    public TelemetrySpan StartActiveSpan(
        string name,
        SpanKind kind = SpanKind.Internal,
        in SpanContext parentContext = default,
        SpanAttributes? initialAttributes = null,
        IEnumerable<Link>? links = null,
        DateTimeOffset startTime = default)
    {
        return this.StartSpanHelper(StartSpanBehaviors.ActivateNewSpan, name, kind, in parentContext, initialAttributes, links, startTime);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ActivityKind ConvertToActivityKind(SpanKind kind)
    {
        return kind switch
        {
            SpanKind.Client => ActivityKind.Client,
            SpanKind.Consumer => ActivityKind.Consumer,
            SpanKind.Internal => ActivityKind.Internal,
            SpanKind.Producer => ActivityKind.Producer,
            SpanKind.Server => ActivityKind.Server,
            _ => ActivityKind.Internal,
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private TelemetrySpan StartSpanHelper(
        StartSpanBehaviors startSpanBehavior,
        string name,
        SpanKind kind,
        in SpanContext parentContext = default,
        SpanAttributes? initialAttributes = null,
        IEnumerable<Link>? links = null,
        DateTimeOffset startTime = default)
    {
        var activitySource = this.ActivitySource;

        if (!(activitySource?.HasListeners() ?? false))
        {
            return TelemetrySpan.NoopInstance;
        }

        var activityKind = ConvertToActivityKind(kind);
        var activityLinks = links?.Select(l => l.ActivityLink);
        var previousActivity = Activity.Current;

        if (startSpanBehavior.HasFlag(StartSpanBehaviors.NewSpanAsRoot)
            && previousActivity != null)
        {
            Activity.Current = null;
        }

        try
        {
            var activity = activitySource.StartActivity(name, activityKind, parentContext.ActivityContext, initialAttributes?.Attributes ?? null, activityLinks, startTime);
            return activity == null
                ? TelemetrySpan.NoopInstance
                : new TelemetrySpan(activity);
        }
        finally
        {
            if (startSpanBehavior.HasFlag(StartSpanBehaviors.DeactivateNewSpan)
                && Activity.Current != previousActivity)
            {
                Activity.Current = previousActivity;
            }
        }
    }
}

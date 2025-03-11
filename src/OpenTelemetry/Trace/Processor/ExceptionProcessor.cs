// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Runtime.InteropServices;
#if !NET && !NETFRAMEWORK
using System.Linq.Expressions;
using System.Reflection;
#endif

namespace OpenTelemetry.Trace;

internal sealed class ExceptionProcessor : BaseProcessor<Activity>
{
    private const string ExceptionPointersKey = "otel.exception_pointers";

    private readonly Func<IntPtr> fnGetExceptionPointers;

    public ExceptionProcessor()
    {
#if NET || NETFRAMEWORK
        this.fnGetExceptionPointers = Marshal.GetExceptionPointers;
#else
        // When running on netstandard or similar the Marshal class is not a part of the netstandard API
        // but it would still most likely be available in the underlying framework, so use reflection to retrieve it.
        var flags = BindingFlags.Static | BindingFlags.Public;
        var method = typeof(Marshal).GetMethod("GetExceptionPointers", flags, null, Type.EmptyTypes, null)
            ?? throw new InvalidOperationException("Marshal.GetExceptionPointers method could not be resolved reflectively.");
        var lambda = Expression.Lambda<Func<IntPtr>>(Expression.Call(method));
        this.fnGetExceptionPointers = lambda.Compile();
#endif
        this.fnGetExceptionPointers(); // attempt to access pointers to test for platform support
    }

    /// <inheritdoc />
    public override void OnStart(Activity activity)
    {
        var pointers = this.fnGetExceptionPointers();

        if (pointers != IntPtr.Zero)
        {
            activity.SetTag(ExceptionPointersKey, pointers);
        }
    }

    /// <inheritdoc />
    public override void OnEnd(Activity activity)
    {
        var pointers = this.fnGetExceptionPointers();

        if (pointers == IntPtr.Zero)
        {
            return;
        }

        var snapshot = activity.GetTagValue(ExceptionPointersKey) as IntPtr?;

        if (snapshot != null)
        {
            activity.SetTag(ExceptionPointersKey, null);
        }

        if (snapshot != pointers)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            activity.SetStatus(Status.Error);
#pragma warning restore CS0618 // Type or member is obsolete
        }
    }
}

// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if !NET
namespace OpenTelemetry;

// Note: .NET 9 added the System.Threading.Lock class. The goal here is when
// compiling against .NET 9+ code should use System.Threading.Lock class for
// better perf. Legacy code can use this class which will perform a classic
// monitor-based lock against a reference of this class. This type is not in the
// System.Threading namespace so that the compiler doesn't get confused when it
// sees it used. It is in OpenTelemetry namespace and not OpenTelemetry.Internal
// namespace so that code should be able to use it without the presence of a
// dedicated "using OpenTelemetry.Internal" just for the shim. The members below
// mirror those of System.Threading.Lock which are needed where a lock statement
// is not sufficient, so that they can be used without conditional compilation
// (calling Monitor with a System.Threading.Lock would use the wrong kind of lock).
internal sealed class Lock
{
    // The instance is the monitor, as it is for the lock statement, so that both
    // can be used with the same instance as they can with System.Threading.Lock.
#pragma warning disable CA2002 // Do not lock on objects with weak identity - this is the type's purpose
    public bool TryEnter(int millisecondsTimeout)
        => Monitor.TryEnter(this, millisecondsTimeout);
#pragma warning restore CA2002 // Do not lock on objects with weak identity

    public void Exit()
        => Monitor.Exit(this);
}
#endif

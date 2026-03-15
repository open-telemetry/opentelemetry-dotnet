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
// dedicated "using OpenTelemetry.Internal" just for the shim.
internal sealed class Lock
{
}
#endif

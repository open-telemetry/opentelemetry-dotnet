// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Extensions.ProcessContext.Internal;

/// <summary>
/// <see cref="IProcessContextPlatform"/> for platforms without process context
/// support (anything other than Linux on x64 or arm64, and the .NET Framework
/// and .NET Standard builds). <see cref="ProcessContextWriter"/> checks <see
/// cref="IsSupported"/> first and never calls the other members.
/// </summary>
internal sealed class UnsupportedProcessContextPlatform : IProcessContextPlatform
{
    public static readonly UnsupportedProcessContextPlatform Instance = new();

    public bool IsSupported => false;

    public int MappingSize => 0;

    public nint CreateMapping(int size, out bool memfdBacked, out string? error)
        => throw new NotSupportedException();

    public bool PreventForkInheritance(nint address, int size, out string? error)
        => throw new NotSupportedException();

    public bool NameMapping(nint address, int size)
        => throw new NotSupportedException();

    public void ReleaseMapping(nint address, int size)
        => throw new NotSupportedException();

    public nint AllocatePayload(int size)
        => throw new NotSupportedException();

    public void FreePayload(nint address)
        => throw new NotSupportedException();

    public ulong GetBootTimeNanoseconds()
        => throw new NotSupportedException();

    public int GetProcessId()
        => throw new NotSupportedException();
}

// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.InteropServices;
using OpenTelemetry.Extensions.ProcessContext.Internal;

namespace OpenTelemetry.Extensions.ProcessContext.Tests;

/// <summary>
/// Stands in for the operating system: "mappings" and payloads are plain
/// native allocations, so the protocol can be checked on any platform.
/// </summary>
internal sealed class FakeProcessContextPlatform : IProcessContextPlatform, IDisposable
{
    private readonly HashSet<nint> liveMappings = [];
    private readonly HashSet<nint> livePayloads = [];

    public bool IsSupported { get; set; } = true;

    public int MappingSize => 4096;

    public bool MemfdAvailable { get; set; } = true;

    public bool MappingFails { get; set; }

    public bool MadviseFails { get; set; }

    public bool NamingFails { get; set; }

    public int ProcessId { get; set; } = 1234;

    /// <summary>
    /// Gets the values returned by successive clock reads; the last one repeats.
    /// </summary>
    public Queue<ulong> ClockValues { get; } = new([1_000UL]);

    public List<string> Calls { get; } = [];

    public IReadOnlyCollection<nint> LiveMappings => this.liveMappings;

    public IReadOnlyCollection<nint> LivePayloads => this.livePayloads;

    private ulong LastClockValue { get; set; }

    public nint CreateMapping(int size, out bool memfdBacked, out string? error)
    {
        this.Calls.Add("CreateMapping");
        memfdBacked = this.MemfdAvailable;
        error = this.MemfdAvailable ? null : "memfd_create failed with errno 1";
        if (this.MappingFails)
        {
            error = "mmap failed with errno 12";
            return 0;
        }

        var address = Marshal.AllocHGlobal(size);
        for (var i = 0; i < size; i++)
        {
            Marshal.WriteByte(address, i, 0);
        }

        this.liveMappings.Add(address);
        return address;
    }

    public bool PreventForkInheritance(nint address, int size, out string? error)
    {
        this.Calls.Add("Madvise");
        error = this.MadviseFails ? "madvise(MADV_DONTFORK) failed with errno 22" : null;
        return !this.MadviseFails;
    }

    public bool NameMapping(nint address, int size)
    {
        this.Calls.Add("NameMapping");
        return !this.NamingFails;
    }

    public void ReleaseMapping(nint address, int size)
    {
        this.Calls.Add("ReleaseMapping");
        Assert.True(this.liveMappings.Remove(address), "Released a mapping which is not live.");
        Marshal.FreeHGlobal(address);
    }

    public nint AllocatePayload(int size)
    {
        var address = Marshal.AllocHGlobal(size);
        this.livePayloads.Add(address);
        return address;
    }

    public void FreePayload(nint address)
    {
        Assert.True(this.livePayloads.Remove(address), "Freed a payload which is not live.");
        Marshal.FreeHGlobal(address);
    }

    public ulong GetBootTimeNanoseconds()
    {
        if (this.ClockValues.Count > 0)
        {
            this.LastClockValue = this.ClockValues.Dequeue();
        }

        return this.LastClockValue;
    }

    public int GetProcessId() => this.ProcessId;

    public void Dispose()
    {
        foreach (var address in this.liveMappings)
        {
            Marshal.FreeHGlobal(address);
        }

        foreach (var address in this.livePayloads)
        {
            Marshal.FreeHGlobal(address);
        }

        this.liveMappings.Clear();
        this.livePayloads.Clear();
    }
}

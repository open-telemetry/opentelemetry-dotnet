// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.InteropServices;

namespace OpenTelemetry.Extensions.ProcessContext.Internal;

/// <summary>
/// Owns the single process context mapping of the process and implements the
/// specification's publication, update and drop protocols. All operations are
/// serialized by a lock, as the specification requires, and none of them ever
/// throws: failures are reported through <see cref="ProcessContextEventSource"/>.
/// </summary>
internal sealed class ProcessContextWriter
{
    /// <summary>
    /// The largest payload published. The specification asks readers to bound
    /// the payload size without naming a value; the OpenTelemetry eBPF Profiler
    /// and the Go SDK both use 64 KiB.
    /// </summary>
    internal const int MaxPayloadSize = 65536;

    private readonly Lock syncRoot = new();
    private readonly IProcessContextPlatform platform;

    private nint mapping;
    private int mappingSize;
    private int publisherProcessId;
    private nint payload;
    private byte[]? publishedPayload;
    private ulong lastPublishedAt;

    internal ProcessContextWriter(IProcessContextPlatform platform)
    {
        this.platform = platform;
    }

    public static ProcessContextWriter Default { get; } = new(CreateDefaultPlatform());

    internal bool IsSupported => this.platform.IsSupported;

    /// <summary>
    /// Gets the address of the header mapping, or zero when nothing is
    /// published. For tests.
    /// </summary>
    internal nint MappingAddress => this.mapping;

    /// <summary>
    /// Gets or sets a hook invoked during an update while the timestamp is zero. For tests.
    /// </summary>
    internal Action? OnMutatingForTesting { get; set; }

    /// <summary>
    /// Publishes <paramref name="payloadBytes"/>, or updates the existing
    /// context if one was already published by this process. Publishing the
    /// same bytes again is a no-op.
    /// </summary>
    /// <param name="payloadBytes">An encoded <c>ProcessContext</c> message.</param>
    /// <returns><see langword="true"/> when the context is published.</returns>
    internal bool Publish(byte[] payloadBytes)
    {
        if (!this.platform.IsSupported)
        {
            return false;
        }

        lock (this.syncRoot)
        {
            try
            {
                if (payloadBytes.Length == 0 || payloadBytes.Length > MaxPayloadSize)
                {
                    ProcessContextEventSource.Log.PublishFailedOnce(
                        $"payload size {payloadBytes.Length} is outside the supported range 1..{MaxPayloadSize}");
                    return false;
                }

                if (this.IsPublishedByThisProcess())
                {
                    return this.UpdateLocked(payloadBytes);
                }

                // A mapping recorded by a parent process is not present in a
                // forked child (MADV_DONTFORK), so it is forgotten, not unmapped.
                this.ForgetLocked();
                return this.PublishLocked(payloadBytes);
            }
            catch (Exception ex)
            {
                ProcessContextEventSource.Log.PublishFailedOnce(ex);
                return false;
            }
        }
    }

    /// <summary>
    /// Drops the published context: zeroes the timestamp, unmaps the header
    /// and frees the payload.
    /// </summary>
    internal void Drop()
    {
        if (!this.platform.IsSupported)
        {
            return;
        }

        lock (this.syncRoot)
        {
            try
            {
                if (this.IsPublishedByThisProcess())
                {
                    ProcessContextHeader.Invalidate(this.mapping);
                    this.platform.ReleaseMapping(this.mapping, this.mappingSize);
                    ProcessContextEventSource.Log.Dropped();
                }

                this.ForgetLocked();
            }
            catch (Exception ex)
            {
                ProcessContextEventSource.Log.PublishFailedOnce(ex);
            }
        }
    }

#pragma warning disable CA1859 // The concrete type depends on the target framework and platform.
    private static IProcessContextPlatform CreateDefaultPlatform()
#pragma warning restore CA1859
    {
#if NET
        if (OperatingSystem.IsLinux()
            && Environment.Is64BitProcess
            && RuntimeInformation.ProcessArchitecture is Architecture.X64 or Architecture.Arm64)
        {
            return new LinuxProcessContextPlatform();
        }
#endif
        return UnsupportedProcessContextPlatform.Instance;
    }

    private bool IsPublishedByThisProcess()
        => this.mapping != 0 && this.publisherProcessId == this.platform.GetProcessId();

    private bool PublishLocked(byte[] payloadBytes)
    {
        var timestamp = this.NextTimestamp();
        if (timestamp == 0)
        {
            ProcessContextEventSource.Log.PublishFailedOnce("clock_gettime(CLOCK_BOOTTIME) failed");
            return false;
        }

        // Steps 2 to 4: memfd (with fallbacks) or anonymous mapping.
        var size = this.platform.MappingSize;
        var address = this.platform.CreateMapping(size, out var memfdBacked, out var mappingError);
        if (address == 0)
        {
            ProcessContextEventSource.Log.PublishFailedOnce(mappingError ?? "mapping could not be created");
            return false;
        }

        // Step 5: never let a forked child inherit (stale) context.
        if (!this.platform.PreventForkInheritance(address, size, out var madviseError))
        {
            this.platform.ReleaseMapping(address, size);
            ProcessContextEventSource.Log.PublishFailedOnce(madviseError ?? "madvise(MADV_DONTFORK) failed");
            return false;
        }

        // Step 6: the payload lives in its own allocation, so that an update
        // can prepare the new payload before touching the header.
        var payloadAddress = this.CopyPayload(payloadBytes);

        // Steps 7 to 9: header fields, fence, timestamp last.
        ProcessContextHeader.Publish(address, payloadAddress, payloadBytes.Length, timestamp);

        // Step 10: always attempt to name the mapping, even where naming is
        // known to fail, so readers hooking prctl can see the attempt.
        var named = this.platform.NameMapping(address, size);
        if (!named && !memfdBacked)
        {
            // An unnamed anonymous mapping cannot be found by readers.
            ProcessContextHeader.Invalidate(address);
            this.platform.ReleaseMapping(address, size);
            this.platform.FreePayload(payloadAddress);
            ProcessContextEventSource.Log.PublishFailedOnce(
                $"{mappingError ?? "memfd_create failed"}, and prctl(PR_SET_VMA_ANON_NAME) failed, so the mapping could not be made discoverable");
            return false;
        }

        this.mapping = address;
        this.mappingSize = size;
        this.publisherProcessId = this.platform.GetProcessId();
        this.payload = payloadAddress;
        this.publishedPayload = payloadBytes;
        this.lastPublishedAt = timestamp;

        ProcessContextEventSource.Log.Published(payloadBytes.Length, memfdBacked, named);
        return true;
    }

    private bool UpdateLocked(byte[] payloadBytes)
    {
        if (this.publishedPayload != null && payloadBytes.AsSpan().SequenceEqual(this.publishedPayload))
        {
            return true;
        }

        var timestamp = this.NextTimestamp();
        if (timestamp == 0)
        {
            ProcessContextEventSource.Log.PublishFailedOnce("clock_gettime(CLOCK_BOOTTIME) failed");
            return false;
        }

        // Step 1: prepare the new payload before readers are told anything.
        var newPayload = this.CopyPayload(payloadBytes);

        // Steps 2 to 6.
        ProcessContextHeader.Update(this.mapping, newPayload, payloadBytes.Length, timestamp, this.OnMutatingForTesting);

        // Step 7: re-issue the name unconditionally; the result is ignored.
        this.platform.NameMapping(this.mapping, this.mappingSize);

        // A reader still copying the old payload sees a changed timestamp on
        // its re-check and retries, so the old payload can be freed now.
        this.platform.FreePayload(this.payload);
        this.payload = newPayload;
        this.publishedPayload = payloadBytes;
        this.lastPublishedAt = timestamp;

        ProcessContextEventSource.Log.Updated(payloadBytes.Length);
        return true;
    }

    private void ForgetLocked()
    {
        if (this.payload != 0)
        {
            this.platform.FreePayload(this.payload);
        }

        this.mapping = 0;
        this.mappingSize = 0;
        this.publisherProcessId = 0;
        this.payload = 0;
        this.publishedPayload = null;
    }

    private nint CopyPayload(byte[] payloadBytes)
    {
        var address = this.platform.AllocatePayload(payloadBytes.Length);
        Marshal.Copy(payloadBytes, 0, address, payloadBytes.Length);
        return address;
    }

    /// <summary>
    /// Returns a <c>CLOCK_BOOTTIME</c> timestamp strictly greater than any
    /// previously published one, so that every update is visible to readers
    /// even when the clock has not advanced (or the context was dropped and
    /// published again).
    /// </summary>
    private ulong NextTimestamp()
    {
        var now = this.platform.GetBootTimeNanoseconds();
        if (now == 0)
        {
            return 0;
        }

        if (now <= this.lastPublishedAt)
        {
            now = this.lastPublishedAt + 1;
        }

        return now;
    }
}

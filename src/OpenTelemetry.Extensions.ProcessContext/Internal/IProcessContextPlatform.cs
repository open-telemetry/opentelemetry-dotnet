// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Extensions.ProcessContext.Internal;

/// <summary>
/// The operating system primitives used by <see cref="ProcessContextWriter"/>.
/// Abstracted so that the publication and update protocol can be tested on
/// any platform with ordinary native memory standing in for the mapping.
/// </summary>
internal interface IProcessContextPlatform
{
    /// <summary>
    /// Gets a value indicating whether process context publication is
    /// supported on this platform.
    /// </summary>
    bool IsSupported { get; }

    /// <summary>
    /// Gets the size, in bytes, to allocate for the header mapping.
    /// </summary>
    int MappingSize { get; }

    /// <summary>
    /// Publication protocol steps 2 to 4: creates a readable and writable
    /// private mapping, backed by a memfd named <c>OTEL_CTX</c> where
    /// possible, otherwise anonymous.
    /// </summary>
    /// <param name="size">Size of the mapping in bytes.</param>
    /// <param name="memfdBacked">Set to <see langword="true"/> when the mapping is backed by a memfd.</param>
    /// <param name="error">A description of the failure (or of a fallback taken), if any.</param>
    /// <returns>The address of the mapping, or zero on failure.</returns>
    nint CreateMapping(int size, out bool memfdBacked, out string? error);

    /// <summary>
    /// Publication protocol step 5: <c>madvise(MADV_DONTFORK)</c>.
    /// </summary>
    /// <param name="address">Address of the mapping.</param>
    /// <param name="size">Size of the mapping in bytes.</param>
    /// <param name="error">A description of the failure, if any.</param>
    /// <returns><see langword="true"/> on success.</returns>
    bool PreventForkInheritance(nint address, int size, out string? error);

    /// <summary>
    /// Publication protocol step 10 and update protocol step 7: names the
    /// mapping <c>OTEL_CTX</c> with <c>prctl(PR_SET_VMA_ANON_NAME)</c>.
    /// </summary>
    /// <param name="address">Address of the mapping.</param>
    /// <param name="size">Size of the mapping in bytes.</param>
    /// <returns><see langword="true"/> when the kernel accepted the name.</returns>
    bool NameMapping(nint address, int size);

    /// <summary>
    /// Releases a mapping created by <see cref="CreateMapping"/>.
    /// </summary>
    /// <param name="address">Address of the mapping.</param>
    /// <param name="size">Size of the mapping in bytes.</param>
    void ReleaseMapping(nint address, int size);

    /// <summary>
    /// Allocates native memory, at a stable address, to hold a payload.
    /// </summary>
    /// <param name="size">Size in bytes.</param>
    /// <returns>The address of the allocation.</returns>
    nint AllocatePayload(int size);

    /// <summary>
    /// Frees memory returned by <see cref="AllocatePayload"/>.
    /// </summary>
    /// <param name="address">The address to free.</param>
    void FreePayload(nint address);

    /// <summary>
    /// Reads <c>CLOCK_BOOTTIME</c> in nanoseconds.
    /// </summary>
    /// <returns>The current time, or zero if the clock could not be read.</returns>
    ulong GetBootTimeNanoseconds();

    /// <summary>
    /// Gets the id of the calling process, read from the operating system
    /// on every call so that a forked child is never mistaken for its parent.
    /// </summary>
    /// <returns>The process id.</returns>
    int GetProcessId();
}

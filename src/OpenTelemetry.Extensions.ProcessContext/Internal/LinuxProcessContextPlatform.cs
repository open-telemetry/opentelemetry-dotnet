// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
using System.Globalization;
using System.Runtime.InteropServices;
using static OpenTelemetry.Extensions.ProcessContext.Internal.LinuxNativeMethods;

namespace OpenTelemetry.Extensions.ProcessContext.Internal;

/// <summary>
/// <see cref="IProcessContextPlatform"/> for Linux on x64 and arm64.
/// </summary>
internal sealed class LinuxProcessContextPlatform : IProcessContextPlatform
{
    private const string MappingName = "OTEL_CTX";

    public bool IsSupported => true;

    // The header needs 32 bytes, but mappings are made of whole pages and
    // prctl(PR_SET_VMA_ANON_NAME) needs a page aligned start address.
    public int MappingSize => Environment.SystemPageSize;

    public nint CreateMapping(int size, out bool memfdBacked, out string? error)
    {
        error = null;
        memfdBacked = false;

        // Step 2: memfd_create, retrying without MFD_NOEXEC_SEAL (Linux 6.3+)
        // for kernels which reject the flag.
        var fd = -1;
        var memfdErrno = 0;
        try
        {
            fd = MemfdCreate(MappingName, MFD_CLOEXEC | MFD_ALLOW_SEALING | MFD_NOEXEC_SEAL);
            if (fd < 0)
            {
                fd = MemfdCreate(MappingName, MFD_CLOEXEC | MFD_ALLOW_SEALING);
            }

            if (fd < 0)
            {
                memfdErrno = Marshal.GetLastPInvokeError();
            }
        }
        catch (EntryPointNotFoundException)
        {
            // C libraries older than glibc 2.27 or musl 1.1.20 do not export
            // memfd_create. The spec treats this like any other memfd failure.
            fd = -1;
        }

        nint address;
        if (fd >= 0)
        {
            if (Ftruncate(fd, size) != 0)
            {
                var errno = Marshal.GetLastPInvokeError();
                Close(fd);
                error = Describe("ftruncate", errno);
                return 0;
            }

            // Step 3: map the memfd, then close it. The mapping keeps the
            // memfd alive and makes it visible in /proc/<pid>/maps.
            address = Mmap(0, (nuint)size, PROT_READ | PROT_WRITE, MAP_PRIVATE, fd, 0);
            var mmapErrno = Marshal.GetLastPInvokeError();
            Close(fd);

            if (address == MapFailed)
            {
                error = Describe("mmap (memfd)", mmapErrno);
                return 0;
            }

            memfdBacked = true;
            return address;
        }

        // Step 4: memfd is not available (seccomp, old kernel), fall back to
        // an anonymous mapping which can only be found if naming it works.
        error = memfdErrno != 0
            ? Describe("memfd_create", memfdErrno)
            : "memfd_create is not available";

        address = Mmap(0, (nuint)size, PROT_READ | PROT_WRITE, MAP_PRIVATE | MAP_ANONYMOUS, -1, 0);
        if (address == MapFailed)
        {
            error = $"{error}; {Describe("mmap (anonymous)", Marshal.GetLastPInvokeError())}";
            return 0;
        }

        return address;
    }

    public bool PreventForkInheritance(nint address, int size, out string? error)
    {
        if (Madvise(address, (nuint)size, MADV_DONTFORK) != 0)
        {
            error = Describe("madvise(MADV_DONTFORK)", Marshal.GetLastPInvokeError());
            return false;
        }

        error = null;
        return true;
    }

    public bool NameMapping(nint address, int size)
        => Prctl(PR_SET_VMA, PR_SET_VMA_ANON_NAME, address, (nuint)size, MappingName) == 0;

    public void ReleaseMapping(nint address, int size)
        => Munmap(address, (nuint)size);

    public nint AllocatePayload(int size)
        => Marshal.AllocHGlobal(size);

    public void FreePayload(nint address)
        => Marshal.FreeHGlobal(address);

    public ulong GetBootTimeNanoseconds()
    {
        if (ClockGettime(CLOCK_BOOTTIME, out var timespec) != 0)
        {
            return 0;
        }

        return ((ulong)timespec.Seconds * 1_000_000_000UL) + (ulong)timespec.Nanoseconds;
    }

    public int GetProcessId()
        => Getpid();

    private static string Describe(string call, int errno)
        => string.Create(CultureInfo.InvariantCulture, $"{call} failed with errno {errno}");
}
#endif

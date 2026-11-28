// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
using System.Reflection;
using System.Runtime.InteropServices;

namespace OpenTelemetry.Extensions.ProcessContext.Internal;

/// <summary>
/// The libc functions used to publish the process context, bound through
/// <see cref="LibraryImportAttribute"/> so that they are trim and Native AOT
/// safe. Only called on Linux (x64 and arm64).
/// </summary>
internal static partial class LinuxNativeMethods
{
    // From include/uapi/linux/memfd.h.
    internal const uint MFD_CLOEXEC = 0x0001;
    internal const uint MFD_ALLOW_SEALING = 0x0002;
    internal const uint MFD_NOEXEC_SEAL = 0x0008;

    // From include/uapi/asm-generic/mman-common.h. These values are the same
    // on x64 and arm64.
    internal const int PROT_READ = 0x1;
    internal const int PROT_WRITE = 0x2;
    internal const int MAP_PRIVATE = 0x02;
    internal const int MAP_ANONYMOUS = 0x20;
    internal const int MADV_DONTFORK = 10;

    // From include/uapi/linux/prctl.h.
    internal const int PR_SET_VMA = 0x53564d41;
    internal const nuint PR_SET_VMA_ANON_NAME = 0;

    // From include/uapi/linux/time.h.
    internal const int CLOCK_BOOTTIME = 7;

    internal static readonly nint MapFailed = -1;

    /// <summary>
    /// The library name used by every import below. It is resolved by <see
    /// cref="ResolveLibC"/> to the handle of the main program, whose global
    /// symbol scope always contains the C library the runtime itself was
    /// loaded with (glibc or musl), so no file name has to be guessed.
    /// </summary>
    private const string LibC = "libc";

    static LinuxNativeMethods()
    {
        NativeLibrary.SetDllImportResolver(typeof(LinuxNativeMethods).Assembly, ResolveLibC);
    }

    /// <summary>memfd_create(2).</summary>
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [LibraryImport(LibC, EntryPoint = "memfd_create", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int MemfdCreate(string name, uint flags);

    /// <summary>ftruncate(2).</summary>
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [LibraryImport(LibC, EntryPoint = "ftruncate", SetLastError = true)]
    internal static partial int Ftruncate(int fd, long length);

    /// <summary>close(2).</summary>
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [LibraryImport(LibC, EntryPoint = "close", SetLastError = true)]
    internal static partial int Close(int fd);

    /// <summary>mmap(2).</summary>
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [LibraryImport(LibC, EntryPoint = "mmap", SetLastError = true)]
    internal static partial nint Mmap(nint addr, nuint length, int prot, int flags, int fd, long offset);

    /// <summary>munmap(2).</summary>
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [LibraryImport(LibC, EntryPoint = "munmap", SetLastError = true)]
    internal static partial int Munmap(nint addr, nuint length);

    /// <summary>madvise(2).</summary>
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [LibraryImport(LibC, EntryPoint = "madvise", SetLastError = true)]
    internal static partial int Madvise(nint addr, nuint length, int advice);

    // prctl is declared variadic in C (int prctl(int option, ...)). Every
    // argument here is an integer or a pointer, which the x64 and arm64 Linux
    // calling conventions pass in the same registers whether or not the
    // callee is variadic.
    /// <summary>prctl(2) with PR_SET_VMA, PR_SET_VMA_ANON_NAME.</summary>
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [LibraryImport(LibC, EntryPoint = "prctl", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int Prctl(int option, nuint arg2, nint addr, nuint length, string name);

    /// <summary>clock_gettime(2).</summary>
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [LibraryImport(LibC, EntryPoint = "clock_gettime", SetLastError = true)]
    internal static partial int ClockGettime(int clockId, out Timespec timespec);

    /// <summary>getpid(2).</summary>
    /// <returns>The process id.</returns>
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [LibraryImport(LibC, EntryPoint = "getpid")]
    internal static partial int Getpid();

    private static nint ResolveLibC(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
        => libraryName == LibC ? NativeLibrary.GetMainProgramHandle() : 0;

    [StructLayout(LayoutKind.Sequential)]
    internal struct Timespec
    {
        public long Seconds;
        public long Nanoseconds;
    }
}
#endif

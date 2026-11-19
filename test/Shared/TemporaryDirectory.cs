// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
using System.Runtime.Versioning;
#endif

namespace OpenTelemetry.Tests;

/// <summary>
/// Creates a unique temporary directory and deletes it on disposal.
/// </summary>
internal sealed class TemporaryDirectory : IDisposable
{
#if NET
    private const UnixFileMode OwnerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    private readonly DirectoryInfo directory = Create();
#else
    private readonly string directoryPath = System.IO.Path.Combine(
        System.IO.Path.GetTempPath(),
        "otel-tests-" + Guid.NewGuid().ToString("N"));
#endif

    internal TemporaryDirectory()
    {
#if !NET
        Directory.CreateDirectory(this.directoryPath);
#endif
    }

    internal string Path =>
#if NET
        this.directory.FullName;
#else
        this.directoryPath;
#endif

    public void Dispose()
    {
        try
        {
#if NET
            if (this.directory.Exists)
            {
                this.directory.Delete(recursive: true);
            }
#else
            if (Directory.Exists(this.directoryPath))
            {
                Directory.Delete(this.directoryPath, recursive: true);
            }
#endif
        }
        catch
        {
        }
    }

#if NET
    private static DirectoryInfo Create()
    {
        if (OperatingSystem.IsWindows() || CanOnlyBeChangedByOwner(System.IO.Path.GetTempPath()))
        {
            return Directory.CreateTempSubdirectory("otel-tests-");
        }

        // The temporary directory of the system, or one of the directories that it is in, can be written to by other
        // users (such as /tmp, even if TMPDIR is a private directory within it), which some of the code under test
        // refuses, so the directory is created in one that only the current user can write to instead. If there is
        // no such directory, the system temporary directory is used, as most users of this class only need a scratch
        // directory. Those that require a private directory fail with an error about the permissions of the directory.
        if (!CanOnlyBeChangedByOwner(AppContext.BaseDirectory))
        {
            return Directory.CreateTempSubdirectory("otel-tests-");
        }

        var root = System.IO.Path.Combine(AppContext.BaseDirectory, "temp");

        Directory.CreateDirectory(root, OwnerOnly);

        return Directory.CreateDirectory(System.IO.Path.Combine(root, "otel-tests-" + Guid.NewGuid().ToString("N")), OwnerOnly);
    }

    /// <summary>
    /// Gets whether a directory, and every directory that is traversed to reach it (resolving symbolic links),
    /// cannot be written to by users other than its owner. This mirrors the check of the code under test, ignoring
    /// the sticky bit and ownership, so a directory that is accepted here is not rejected for being writable.
    /// </summary>
    [UnsupportedOSPlatform("windows")]
    private static bool CanOnlyBeChangedByOwner(string path, int links = 0)
    {
        const int MaximumLinks = 40;

        if (links > MaximumLinks || !Directory.Exists(path))
        {
            return false;
        }

        var fullPath = System.IO.Path.GetFullPath(path);
        var current = System.IO.Path.GetPathRoot(fullPath)!;

        if (!IsNotWritableByOthers(current))
        {
            return false;
        }

        foreach (var name in fullPath.Substring(current.Length).Split(System.IO.Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = System.IO.Path.Combine(current, name);

            if (!IsNotWritableByOthers(current))
            {
                return false;
            }

            // The mode is that of the target of a symbolic link, but the directories that the target is in must be checked too.
            // Only the next target is resolved, so that the target of every link in a chain of links is checked.
            var target = new DirectoryInfo(current).ResolveLinkTarget(returnFinalTarget: false);

            if (target is not null && !CanOnlyBeChangedByOwner(target.FullName, links + 1))
            {
                return false;
            }
        }

        return true;
    }

    [UnsupportedOSPlatform("windows")]
    private static bool IsNotWritableByOthers(string path)
        => (File.GetUnixFileMode(path) & (UnixFileMode.GroupWrite | UnixFileMode.OtherWrite)) == 0;
#endif
}

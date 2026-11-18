// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

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
        if (OperatingSystem.IsWindows()
            || (File.GetUnixFileMode(System.IO.Path.GetTempPath()) & (UnixFileMode.GroupWrite | UnixFileMode.OtherWrite)) == 0)
        {
            return Directory.CreateTempSubdirectory("otel-tests-");
        }

        // The temporary directory of the system can be written to by other users (such as /tmp), which some of the
        // code under test refuses, so the directory is created in one that only the current user can write to instead
        var root = System.IO.Path.Combine(AppContext.BaseDirectory, "temp");

        Directory.CreateDirectory(root, OwnerOnly);

        return Directory.CreateDirectory(System.IO.Path.Combine(root, "otel-tests-" + Guid.NewGuid().ToString("N")), OwnerOnly);
    }
#endif
}

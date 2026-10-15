// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Tests;

/// <summary>
/// Creates a unique temporary directory and deletes it on disposal.
/// </summary>
internal sealed class TemporaryDirectory : IDisposable
{
#if NET
    private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("otel-tests-");
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
}

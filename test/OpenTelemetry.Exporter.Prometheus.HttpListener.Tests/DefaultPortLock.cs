// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Exporter.Prometheus.Tests;

/// <summary>
/// Serializes, across processes, the tests which need to bind the exporter's
/// well-known default port (or another fixed port), so that the same tests
/// running concurrently in another test process (for example for a different
/// target framework) cannot race each other for it.
/// </summary>
/// <remarks>
/// Most tests use a random free port and do not need this. It is only for the
/// tests whose purpose is to verify what happens when no specific port is configured.
/// </remarks>
internal sealed class DefaultPortLock(FileStream lockFile) : IDisposable
{
    private static readonly string LockFilePath = Path.Combine(
        Path.GetTempPath(),
        "opentelemetry-dotnet-prometheus-httplistener-default-port.lock");

    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan AcquireTimeout = TimeSpan.FromMinutes(2);

    private readonly FileStream lockFile = lockFile;

    /// <summary>
    /// Acquires the lock, waiting for any other process which holds it to release it.
    /// </summary>
    /// <returns>An <see cref="IDisposable"/> which releases the lock when disposed.</returns>
    public static DefaultPortLock Acquire()
    {
        var stopwatch = Stopwatch.StartNew();

        while (true)
        {
            try
            {
                // Opening the file with FileShare.None takes an exclusive lock on it
                // which the operating system releases when the file is closed, which
                // includes the process exiting, so a crashed test run cannot leave the
                // lock held.
                var stream = new FileStream(
                    LockFilePath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 1);

                return new(stream);
            }
            catch (IOException) when (stopwatch.Elapsed < AcquireTimeout)
            {
                Thread.Sleep(RetryDelay);
            }
        }
    }

    public void Dispose()
        => this.lockFile.Dispose();
}

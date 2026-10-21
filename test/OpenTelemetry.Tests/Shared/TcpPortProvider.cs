// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Net.Sockets;

namespace OpenTelemetry.Tests;

/// <summary>
/// Helper class that tries to provide unique ports numbers across processes and threads in the same machine.
/// This class cannot guarantee a port is actually available, but should help avoid most conflicts.
/// </summary>
internal static class TcpPortProvider
{
    private const int MaximumAttempts = 100;

    private static readonly string ReservationDirectory = Path.Combine(Path.GetTempPath(), "opentelemetry-dotnet-tests", "ports");

    // Held for the lifetime of the process (the operating system releases them when it exits).
    private static readonly List<FileStream> Reservations = [];

    public static int GetOpenPort()
    {
        for (var attempt = 0; attempt < MaximumAttempts; attempt++)
        {
            var port = GetPortFromOperatingSystem();

            // The operating system can hand the port it has just released to another process
            // which asks for a free port at the same time (for example these same tests running
            // for another target framework), and that process may then bind it first. Reserve
            // the port towards the other test processes on this machine until this one exits.
            var reservation = TryReserve(port);

            if (reservation is not null)
            {
                lock (Reservations)
                {
                    Reservations.Add(reservation);
                }

                return port;
            }
        }

        throw new InvalidOperationException($"Could not find a port which is not reserved by another test process within {MaximumAttempts} attempts.");
    }

    private static int GetPortFromOperatingSystem()
    {
        TcpListener? tcpListener = null;

        try
        {
            tcpListener = new TcpListener(IPAddress.Loopback, 0);
            tcpListener.Start();

            var port = ((IPEndPoint)tcpListener.LocalEndpoint).Port;

            return port;
        }
        finally
        {
#if NET
            tcpListener?.Dispose();
#else
            tcpListener?.Stop();
#endif

        }
    }

    private static FileStream? TryReserve(int port)
    {
        try
        {
            Directory.CreateDirectory(ReservationDirectory);

            // FileShare.None takes an exclusive lock which other processes see as a sharing
            // violation, and DeleteOnClose removes the file once this process lets go of it.
            return new FileStream(
                Path.Combine(ReservationDirectory, port + ".lock"),
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 1,
                FileOptions.DeleteOnClose);
        }
        catch (IOException)
        {
            // Reserved by another test process
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            // Reserved by another test process which is deleting the file right now
            return null;
        }
    }
}

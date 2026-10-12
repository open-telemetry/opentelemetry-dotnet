// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Runtime.ExceptionServices;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;

// Keeps a client alive until every send that selected it has finished.
internal sealed class ReloadableExportClient : IExportClient, IDisposable
{
    private readonly Lock stateLock = new();
    private readonly Lock reloadGate = new();
    private readonly OtlpExportClientRegistration registration;
    private readonly List<ClientState> liveStates = [];
#pragma warning disable CA2213 // Disposed in OnClientReleased after the last in-flight send finishes.
    private readonly CancellationTokenSource shutdownCancellation = new();
#pragma warning restore CA2213
    private ClientState current;
    private double timeoutMilliseconds;
    private bool stopped;
    private bool disposed;

    private ReloadableExportClient(
        OtlpExportClientRegistration registration,
        (IExportClient Client, bool OwnsHttpClient, double TimeoutMilliseconds) initialClient)
    {
        this.registration = registration;
        this.current = new ClientState(initialClient.Client, initialClient.OwnsHttpClient, this.OnClientReleased);
        this.liveStates.Add(this.current);
        this.timeoutMilliseconds = initialClient.TimeoutMilliseconds;
    }

    internal double TimeoutMilliseconds => Volatile.Read(ref this.timeoutMilliseconds);

    public ExportClientResponse SendExportRequest(byte[] buffer, int contentLength, DateTime deadlineUtc, CancellationToken cancellationToken = default)
    {
        ClientState selected;
        lock (this.stateLock)
        {
#if NET
            ObjectDisposedException.ThrowIf(this.disposed, this);
#else
            if (this.disposed)
            {
                throw new ObjectDisposedException(nameof(ReloadableExportClient));
            }
#endif

            if (this.stopped)
            {
                throw new InvalidOperationException("The export client has been shut down.");
            }

            selected = this.current;
            var acquired = selected.TryAddReference();
            Debug.Assert(acquired, "The current client must still be alive.");
        }

        try
        {
            using var cancellation = cancellationToken.CanBeCanceled
                ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, this.shutdownCancellation.Token)
                : null;
            return selected.Client.SendExportRequest(
                buffer,
                contentLength,
                deadlineUtc,
                cancellation?.Token ?? this.shutdownCancellation.Token);
        }
        finally
        {
            selected.ReleaseReference();
        }
    }

    public bool Shutdown(int timeoutMilliseconds)
    {
        Guard.ThrowIfInvalidTimeout(timeoutMilliseconds);
        long? timestamp = timeoutMilliseconds == Timeout.Infinite ? null : Stopwatch.GetTimestamp();
        ClientState[] selected;
        lock (this.stateLock)
        {
            if (this.stopped)
            {
                return true;
            }

            this.stopped = true;
            selected = this.liveStates.Where(state => state.TryAddReference()).ToArray();
        }

        ExceptionDispatchInfo? failure = null;
        try
        {
            // Also cancels sends selected before shutdown whose HTTP request has not started yet.
            this.shutdownCancellation.Cancel();
        }
        catch (Exception ex)
        {
            failure = ExceptionDispatchInfo.Capture(ex);
        }

        try
        {
            this.registration.Dispose();
        }
        catch (Exception ex)
        {
            failure ??= ExceptionDispatchInfo.Capture(ex);
        }

        var result = true;
        foreach (var state in selected)
        {
            try
            {
                var remaining = timestamp is { } startedAt
                    ? Stopwatch.Remaining(timeoutMilliseconds, startedAt)
                    : Timeout.Infinite;
                result &= state.Shutdown(remaining);
            }
            catch (Exception ex)
            {
                // One failure must not prevent cancellation of the other generations.
                failure ??= ExceptionDispatchInfo.Capture(ex);
            }
        }

        failure?.Throw();
        return result;
    }

    public void Dispose()
    {
        lock (this.stateLock)
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
        }

        this.Shutdown(Timeout.Infinite);
    }

    internal static ReloadableExportClient Create(
        OtlpExporterOptions options,
        IServiceProvider serviceProvider,
        string optionsName,
        OtlpSignalType signalType,
        bool useOtlpExporter,
        bool usesHttpClientFactory,
        Action<OtlpExporterOptions>? configureOnReload = null)
    {
        var registration = new OtlpExportClientRegistration(
            options,
            serviceProvider,
            optionsName,
            signalType,
            useOtlpExporter,
            configureOnReload);
        var initialClient = registration.CreateInitialClient(options, usesHttpClientFactory);
        var client = new ReloadableExportClient(registration, initialClient);
        try
        {
            registration.Subscribe(client.Reload);
            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    private void Reload()
    {
        lock (this.reloadGate)
        {
            lock (this.stateLock)
            {
                if (this.stopped)
                {
                    return;
                }
            }

            try
            {
                if (this.registration.CreateExportClient() is not { } replacement)
                {
                    return;
                }

                var next = new ClientState(replacement.Client, replacement.OwnsHttpClient, this.OnClientReleased);
                ClientState? old = null;

                lock (this.stateLock)
                {
                    if (!this.stopped)
                    {
                        old = this.current;
                        this.current = next;
                        this.liveStates.Add(next);
                        Volatile.Write(ref this.timeoutMilliseconds, replacement.TimeoutMilliseconds);
                    }
                }

                if (old is null)
                {
                    next.Retire();
                }
                else
                {
                    old.Retire();
                }
            }
            catch (Exception ex)
            {
                OpenTelemetryProtocolExporterEventSource.Log.ExportClientReloadFailed(
                    this.registration.SignalType,
                    this.registration.OptionsName,
                    ex);
            }
        }
    }

    private void OnClientReleased(ClientState state)
    {
        lock (this.stateLock)
        {
            this.liveStates.Remove(state);
            if (this.stopped && this.liveStates.Count == 0)
            {
                this.shutdownCancellation.Dispose();
            }
        }
    }

    private sealed class ClientState(IExportClient client, bool ownsHttpClient, Action<ClientState> onReleased)
    {
        private readonly Lock sendLock = new();
        private readonly bool ownsHttpClient = ownsHttpClient;
        private int references;
        private bool retired;

        internal IExportClient Client { get; } = client;

        internal bool TryAddReference()
        {
            lock (this.sendLock)
            {
                if (this.retired && this.references == 0)
                {
                    return false;
                }

                this.references++;
                return true;
            }
        }

        internal void ReleaseReference()
        {
            bool release;
            lock (this.sendLock)
            {
                release = --this.references == 0 && this.retired;
            }

            if (release)
            {
                this.ReleaseClient();
            }
        }

        internal void Retire()
        {
            bool release;
            lock (this.sendLock)
            {
                if (this.retired)
                {
                    return;
                }

                this.retired = true;
                release = this.references == 0;
            }

            if (release)
            {
                this.ReleaseClient();
            }
        }

        internal bool Shutdown(int timeoutMilliseconds)
        {
            try
            {
                return this.Client.Shutdown(timeoutMilliseconds);
            }
            finally
            {
                this.Retire();
                this.ReleaseReference();
            }
        }

        private void ReleaseClient()
        {
            try
            {
                if (this.ownsHttpClient && this.Client is OtlpExportClient exportClient)
                {
                    exportClient.HttpClient.Dispose();
                }
                else if (this.ownsHttpClient && this.Client is LazyExportClient lazyClient)
                {
                    lazyClient.DisposeHttpClient();
                }
            }
            finally
            {
                onReleased(this);
            }
        }
    }
}

// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Runtime.ExceptionServices;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;

internal sealed class ReloadableExportClient : IExportClient, IDisposable
{
    private readonly Lock stateLock = new();
    private readonly Lock reloadGate = new();
    private readonly OtlpExportClientRegistration registration;
#pragma warning disable CA2213 // Disposed in ReleaseCancellationReference after shutdown and all sends finish.
    private readonly CancellationTokenSource shutdownCancellation = new();
#pragma warning restore CA2213
    private ClientState current;

    // The initial reference keeps cancellation alive until Shutdown finishes canceling callbacks.
    private int cancellationReferences = 1;
    private double timeoutMilliseconds;
    private bool stopped;
    private bool disposed;

    private ReloadableExportClient(
        OtlpExportClientRegistration registration,
        (IExportClient Client, bool OwnsHttpClient, double TimeoutMilliseconds) initialClient)
    {
        this.registration = registration;
        this.current = new ClientState(initialClient.Client, initialClient.OwnsHttpClient);
        this.timeoutMilliseconds = initialClient.TimeoutMilliseconds;
    }

    internal double TimeoutMilliseconds => Volatile.Read(ref this.timeoutMilliseconds);

    public ExportClientResponse SendExportRequest(
        byte[] buffer,
        int contentLength,
        DateTime deadlineUtc,
        CancellationToken cancellationToken = default)
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
            selected.AddReference();
            this.cancellationReferences++;
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
            try
            {
                selected.ReleaseReference();
            }
            finally
            {
                this.ReleaseCancellationReference();
            }
        }
    }

    public bool Shutdown(int timeoutMilliseconds)
    {
        Guard.ThrowIfInvalidTimeout(timeoutMilliseconds);
        lock (this.stateLock)
        {
            if (this.stopped)
            {
                return true;
            }

            this.stopped = true;
        }

        ExceptionDispatchInfo? failure = null;
        try
        {
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

        try
        {
            this.current.ReleaseReference();
        }
        catch (Exception ex)
        {
            failure ??= ExceptionDispatchInfo.Capture(ex);
        }
        finally
        {
            this.ReleaseCancellationReference();
        }

        failure?.Throw();
        return true;
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
                var replacement = this.registration.CreateExportClient();

                var next = new ClientState(replacement.Client, replacement.OwnsHttpClient);
                ClientState? old = null;

                lock (this.stateLock)
                {
                    if (!this.stopped)
                    {
                        old = this.current;
                        this.current = next;
                        Volatile.Write(ref this.timeoutMilliseconds, replacement.TimeoutMilliseconds);
                    }
                }

                (old ?? next).ReleaseReference();
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

    private void ReleaseCancellationReference()
    {
        bool release;
        lock (this.stateLock)
        {
            release = --this.cancellationReferences == 0;
        }

        if (release)
        {
            this.shutdownCancellation.Dispose();
        }
    }

    private sealed class ClientState(IExportClient client, bool ownsHttpClient)
    {
        private readonly Lock sendLock = new();
        private readonly bool ownsHttpClient = ownsHttpClient;

        // One reference belongs to the exporter until this client is replaced or shut down.
        private int references = 1;

        internal IExportClient Client { get; } = client;

        internal void AddReference()
        {
            lock (this.sendLock)
            {
                Debug.Assert(this.references > 0, "The current client must still be alive.");
                this.references++;
            }
        }

        internal void ReleaseReference()
        {
            bool release;
            lock (this.sendLock)
            {
                release = --this.references == 0;
            }

            if (release && this.ownsHttpClient)
            {
                if (this.Client is OtlpExportClient exportClient)
                {
                    exportClient.HttpClient.Dispose();
                }
                else if (this.Client is LazyExportClient lazyClient)
                {
                    lazyClient.DisposeHttpClient();
                }
            }
        }
    }
}

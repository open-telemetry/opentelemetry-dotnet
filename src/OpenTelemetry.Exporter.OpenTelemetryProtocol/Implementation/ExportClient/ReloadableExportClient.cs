// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;

// Keeps a client alive until every send that selected it has finished.
internal sealed class ReloadableExportClient : IExportClient, IDisposable
{
    private readonly Lock stateLock = new();
    private readonly Lock reloadGate = new();
    private readonly OtlpExportClientRegistration registration;
    private ClientState current;
    private double timeoutMilliseconds;
    private bool stopped;
    private bool disposed;

    private ReloadableExportClient(
        OtlpExportClientRegistration registration,
        ClientState initialClient,
        double timeoutMilliseconds)
    {
        this.registration = registration;
        this.current = initialClient;
        this.timeoutMilliseconds = timeoutMilliseconds;
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
            selected.BeginSend();
        }

        try
        {
            return selected.Client.SendExportRequest(buffer, contentLength, deadlineUtc, cancellationToken);
        }
        finally
        {
            selected.EndSend();
        }
    }

    public bool Shutdown(int timeoutMilliseconds)
    {
        ClientState selected;
        lock (this.stateLock)
        {
            if (this.stopped)
            {
                return true;
            }

            this.stopped = true;
            selected = this.current;
        }

        this.registration.Dispose();

        bool result;
        try
        {
            result = selected.Client.Shutdown(timeoutMilliseconds);
        }
        finally
        {
            selected.Retire();
        }

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
        var ownsHttpClient = usesHttpClientFactory || ReferenceEquals(options.HttpClientFactory, options.DefaultHttpClientFactory);
        IExportClient initialClient = usesHttpClientFactory && signalType == OtlpSignalType.Logs
            ? new LazyExportClient(() => options.GetExportClient(signalType, ownsHttpClient))
            : options.GetExportClient(signalType, ownsHttpClient);

        var initialState = new ClientState(initialClient, ownsHttpClient);
        ReloadableExportClient? client = null;
        try
        {
            client = new(
                registration,
                initialState,
                OtlpExportClientRegistration.GetTimeout(options, initialClient));
            registration.Subscribe(client.Reload);
            return client;
        }
        catch
        {
            if (client is null)
            {
                initialState.Retire();
            }
            else
            {
                client.Dispose();
            }

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

                var next = new ClientState(replacement.Client, replacement.OwnsHttpClient);
                ClientState old;

                lock (this.stateLock)
                {
                    if (this.stopped)
                    {
                        next.Retire();
                        return;
                    }

                    old = this.current;
                    this.current = next;
                    Volatile.Write(ref this.timeoutMilliseconds, replacement.TimeoutMilliseconds);
                }

                old.Retire();
            }
            catch (Exception ex)
            {
                OpenTelemetryProtocolExporterEventSource.Log.ExportClientReloadFailed(ex);
            }
        }
    }

    private sealed class ClientState(IExportClient client, bool ownsHttpClient)
    {
        private readonly Lock sendLock = new();
        private readonly bool ownsHttpClient = ownsHttpClient;
        private int activeSends;
        private bool retired;

        internal IExportClient Client { get; } = client;

        internal void BeginSend()
        {
            lock (this.sendLock)
            {
                this.activeSends++;
            }
        }

        internal void EndSend()
        {
            bool release;
            lock (this.sendLock)
            {
                release = --this.activeSends == 0 && this.retired;
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
                release = this.activeSends == 0;
            }

            if (release)
            {
                this.ReleaseClient();
            }
        }

        private void ReleaseClient()
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
    }
}

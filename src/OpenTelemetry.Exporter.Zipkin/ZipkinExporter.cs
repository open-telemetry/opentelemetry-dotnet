// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Net;
#if NETFRAMEWORK
using System.Net.Http;
#endif
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text.Json;
using OpenTelemetry.Exporter.Zipkin.Implementation;
using OpenTelemetry.Internal;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter;

/// <summary>
/// Zipkin exporter.
/// </summary>
[Obsolete(ObsoleteNote)]
public class ZipkinExporter : BaseExporter<Activity>
{
    internal const string ObsoleteNote =
        "The Zipkin exporter is obsolete and will be removed in a future release. Consider using the OpenTelemetry.Exporter.OpenTelemetryProtocol NuGet package instead. See https://opentelemetry.io/blog/2025/deprecating-zipkin-exporters/ for more information.";

    private readonly ZipkinExporterOptions options;
    private readonly int maxPayloadSizeInBytes;
    private readonly HttpClient httpClient;
#if NET
    private readonly bool synchronousSendSupportedByCurrentPlatform;
#endif

    /// <summary>
    /// Initializes a new instance of the <see cref="ZipkinExporter"/> class.
    /// </summary>
    /// <param name="options">Configuration options.</param>
    /// <param name="client">Http client to use to upload telemetry.</param>
    public ZipkinExporter(ZipkinExporterOptions options, HttpClient? client = null)
    {
        Guard.ThrowIfNull(options);

        this.options = options;
        this.maxPayloadSizeInBytes = (!options.MaxPayloadSizeInBytes.HasValue || options.MaxPayloadSizeInBytes <= 0)
            ? ZipkinExporterOptions.DefaultMaxPayloadSizeInBytes
            : options.MaxPayloadSizeInBytes.Value;
        this.httpClient = client ?? options.HttpClientFactory?.Invoke() ?? throw new InvalidOperationException("ZipkinExporter was missing HttpClientFactory or it returned null.");

#if NET
        // See: https://github.com/dotnet/runtime/blob/280f2a0c60ce0378b8db49adc0eecc463d00fe5d/src/libraries/System.Net.Http/src/System/Net/Http/HttpClientHandler.AnyMobile.cs#L767
        this.synchronousSendSupportedByCurrentPlatform = !OperatingSystem.IsAndroid()
            && !OperatingSystem.IsIOS()
            && !OperatingSystem.IsTvOS()
            && !OperatingSystem.IsBrowser();
#endif
    }

    internal ZipkinEndpoint? LocalEndpoint { get; private set; }

    /// <inheritdoc/>
    public override ExportResult Export(in Batch<Activity> batch)
    {
        // Prevent Zipkin's HTTP operations from being instrumented.
        using var scope = SuppressInstrumentationScope.Begin();

        try
        {
            if (this.LocalEndpoint == null)
            {
                this.SetLocalEndpointFromResource(this.ParentProvider.GetResource());
            }

            var requestUri = this.options.Endpoint;

            using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = new JsonContent(this, batch),
            };

#if NET
            using var response = this.synchronousSendSupportedByCurrentPlatform
            ? this.httpClient.Send(request, CancellationToken.None)
            : this.httpClient.SendAsync(request, CancellationToken.None).GetAwaiter().GetResult();
#else
            using var response = this.httpClient.SendAsync(request, CancellationToken.None).GetAwaiter().GetResult();
#endif

            response.EnsureSuccessStatusCode();

            return ExportResult.Success;
        }
        catch (Exception ex)
        {
            ZipkinExporterEventSource.Log.FailedExport(ex);

            return ExportResult.Failure;
        }
    }

    internal void SetLocalEndpointFromResource(Resource resource)
    {
        var hostName = ResolveHostName();

        string? ipv4 = null;
        string? ipv6 = null;
        if (!string.IsNullOrEmpty(hostName))
        {
            ipv4 = ResolveHostAddress(hostName!, AddressFamily.InterNetwork);
            ipv6 = ResolveHostAddress(hostName!, AddressFamily.InterNetworkV6);
        }

        string? serviceName = null;
        foreach (var label in resource.Attributes)
        {
            if (label.Key == ResourceSemanticConventions.AttributeServiceName)
            {
                serviceName = label.Value as string;
                break;
            }
        }

        if (string.IsNullOrEmpty(serviceName))
        {
            serviceName = (string)this.ParentProvider.GetDefaultResource().Attributes.Where(
                pair => pair.Key == ResourceSemanticConventions.AttributeServiceName).FirstOrDefault().Value;
        }

        this.LocalEndpoint = new ZipkinEndpoint(
            serviceName,
            ipv4,
            ipv6,
            port: null,
            tags: null);
    }

    private static string? ResolveHostAddress(string hostName, AddressFamily family)
    {
        string? result = null;

        try
        {
            var results = Dns.GetHostAddresses(hostName);

            if (results != null && results.Length > 0)
            {
                foreach (var addr in results)
                {
                    if (addr.AddressFamily.Equals(family))
                    {
                        var sanitizedAddress = new IPAddress(addr.GetAddressBytes()); // Construct address sans ScopeID
                        result = sanitizedAddress.ToString();

                        break;
                    }
                }
            }
        }
        catch (Exception)
        {
            // Ignore
        }

        return result;
    }

    private static string? ResolveHostName()
    {
        string? result = null;

        try
        {
            result = Dns.GetHostName();

            if (!string.IsNullOrEmpty(result))
            {
                var response = Dns.GetHostEntry(result);

                if (response != null)
                {
                    return response.HostName;
                }
            }
        }
        catch (Exception)
        {
            // Ignore
        }

        return result;
    }

    private sealed class JsonContent : HttpContent
    {
        private static readonly MediaTypeHeaderValue JsonHeader = new("application/json")
        {
            CharSet = "utf-8",
        };

        private readonly ZipkinExporter exporter;
        private readonly Batch<Activity> batch;
        private Utf8JsonWriter? writer;

        public JsonContent(ZipkinExporter exporter, in Batch<Activity> batch)
        {
            this.exporter = exporter;
            this.batch = batch;

            this.Headers.ContentType = JsonHeader;
        }

#if NET
        protected override void SerializeToStream(Stream stream, TransportContext? context, CancellationToken cancellationToken)
        {
            this.SerializeToStreamInternal(stream);
        }
#endif

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            this.SerializeToStreamInternal(stream);
            return Task.CompletedTask;
        }

        protected override bool TryComputeLength(out long length)
        {
            // We can't know the length of the content being pushed to the output stream.
            length = -1;
            return false;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.writer?.Dispose();
                this.writer = null;
            }

            base.Dispose(disposing);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SerializeToStreamInternal(Stream stream)
        {
            if (this.writer == null)
            {
                this.writer = new Utf8JsonWriter(stream);
            }
            else
            {
                this.writer.Reset(stream);
            }

            this.writer.WriteStartArray();

            foreach (var activity in this.batch)
            {
                var zipkinSpan = activity.ToZipkinSpan(this.exporter.LocalEndpoint!, this.exporter.options.UseShortTraceIds);

                zipkinSpan.Write(this.writer);

                zipkinSpan.Return();
                if (this.writer.BytesPending >= this.exporter.maxPayloadSizeInBytes)
                {
                    this.writer.Flush();
                }
            }

            this.writer.WriteEndArray();

            this.writer.Flush();
        }
    }
}

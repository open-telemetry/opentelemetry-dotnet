// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics.Tracing;
using System.Net;
#if NETFRAMEWORK
using System.Net.Http;
#endif
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Transmission;
using OpenTelemetry.PersistentStorage.FileSystem;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

[Collection(EventSourceCollectionDefinition.Name)]
public class OtlpExporterOptionsExtensionsTests
{
    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void GetHeaders_NoOptionHeaders_ReturnsStandardHeaders(string? optionHeaders)
    {
        var options = new OtlpExporterOptions
        {
            Headers = optionHeaders,
        };

        var headers = options.GetHeaders<Dictionary<string, string>>((d, k, v) => d.Add(k, v));

        Assert.Equal(options.StandardHeaders.Length, headers.Count);

        for (var i = 0; i < options.StandardHeaders.Length; i++)
        {
            Assert.Contains(headers, entry => entry.Key == options.StandardHeaders[i].Key && entry.Value == options.StandardHeaders[i].Value);
        }
    }

    [Theory]
    [InlineData(" ")]
    [InlineData(",key1=value1,key2=value2,")]
    [InlineData(",,key1=value1,,key2=value2,,")]
    [InlineData("key1")]
    public void GetHeaders_InvalidOptionHeaders_ThrowsArgumentException(string inputOptionHeaders)
        => VerifyHeaders(inputOptionHeaders, string.Empty, true);

    [Theory]
    [InlineData("", "")]
    [InlineData("key1=value1", "key1=value1")]
    [InlineData("key1=value1,key2=value2", "key1=value1,key2=value2")]
    [InlineData("key1=value1,key2=value2,key3=value3", "key1=value1,key2=value2,key3=value3")]
    [InlineData(" key1 = value1 , key2=value2 ", "key1=value1,key2=value2")]
    [InlineData("key1= value with spaces ,key2=another value", "key1=value with spaces,key2=another value")]
    [InlineData("=value1", "=value1")]
    [InlineData("key1=", "key1=")]
    [InlineData("key1=value1%2Ckey2=value2", "key1=value1,key2=value2")]
    [InlineData("key1=value1%2Ckey2=value2%2Ckey3=value3", "key1=value1,key2=value2,key3=value3")]
    [InlineData("Authorization=Basic Zm9vOmJhcg==", "Authorization=Basic Zm9vOmJhcg==")]
    public void GetHeaders_ValidAndUrlEncodedHeaders_ReturnsCorrectHeaders(string inputOptionHeaders, string expectedNormalizedOptional)
        => VerifyHeaders(inputOptionHeaders, expectedNormalizedOptional);

    [Theory]
#pragma warning disable CS0618 // Suppressing gRPC obsolete warning
    [InlineData(OtlpExportProtocol.Grpc, typeof(OtlpGrpcExportClient))]
#pragma warning restore CS0618 // Suppressing gRPC obsolete warning
    [InlineData(OtlpExportProtocol.HttpProtobuf, typeof(OtlpHttpExportClient))]
    public void GetTraceExportClient_SupportedProtocol_ReturnsCorrectExportClient(OtlpExportProtocol protocol, Type expectedExportClientType)
    {
        var options = new OtlpExporterOptions
        {
            Protocol = protocol,
        };

        var exportClient = options.GetExportClient(OtlpSignalType.Traces);

        Assert.Equal(expectedExportClientType, exportClient.GetType());
    }

    [Fact]
    public void GetTraceExportClient_UnsupportedProtocol_Throws()
    {
        var options = new OtlpExporterOptions
        {
            Protocol = (OtlpExportProtocol)123,
        };

        Assert.Throws<NotSupportedException>(() => options.GetExportClient(OtlpSignalType.Traces));
    }

    [Theory]
    [InlineData("http://test:8888", "http://test:8888/v1/traces")]
    [InlineData("http://test:8888/", "http://test:8888/v1/traces")]
    [InlineData("http://test:8888/v1/traces", "http://test:8888/v1/traces")]
    [InlineData("http://test:8888/v1/traces/", "http://test:8888/v1/traces/")]
    public void AppendPathIfNotPresent_TracesPath_AppendsCorrectly(string input, string expected)
    {
        var uri = new Uri(input, UriKind.Absolute);

        var resultUri = uri.AppendPathIfNotPresent("v1/traces");

        Assert.Equal(expected, resultUri.AbsoluteUri);
    }

#if NET
    [Theory]
    [InlineData(null, "client.pem")]
    [InlineData("ca.pem", null)]
    [InlineData("ca.pem", "client.pem")]
    public void TryEnableIHttpClientFactoryIntegration_DoesNotReplaceDefaultFactory_WhenTlsIsEnabled(
        string? caCertificatePath,
        string? clientCertificatePath)
    {
        var services = new ServiceCollection();

        services.AddHttpClient("OtlpLogExporter");

        using var serviceProvider = services.BuildServiceProvider();

        var options = new OtlpExporterOptions
        {
            Protocol = OtlpExportProtocol.HttpProtobuf,
            MtlsOptions = new OtlpMtlsOptions
            {
                CaCertificatePath = caCertificatePath,
                ClientCertificatePath = clientCertificatePath,
            },
        };

        var originalFactory = options.HttpClientFactory;

        var actual = options.TryEnableIHttpClientFactoryIntegration(serviceProvider, "OtlpLogExporter");

        Assert.False(actual);
        Assert.Same(originalFactory, options.HttpClientFactory);
        Assert.Same(options.DefaultHttpClientFactory, options.HttpClientFactory);
    }
#endif

    [Fact]
    public void TryEnableIHttpClientFactoryIntegration_UsesIHttpClientFactory_WhenServiceProviderIsNotDisposed()
    {
        var services = new ServiceCollection();

        var invocations = 0;

        services.AddHttpClient("OtlpLogExporter", configureClient: (client) => invocations++);

        using var serviceProvider = services.BuildServiceProvider();

        var options = new OtlpExporterOptions { Protocol = OtlpExportProtocol.HttpProtobuf };

        Assert.True(options.TryEnableIHttpClientFactoryIntegration(serviceProvider, "OtlpLogExporter"));

        using var client = options.HttpClientFactory();

        Assert.NotNull(client);
        Assert.Equal(1, invocations);
    }

    [Fact]
    public void TryEnableIHttpClientFactoryIntegration_FallsBackToDefaultHttpClient_WhenServiceProviderIsDisposed()
    {
        using var listener = new TestEventListener(OpenTelemetryProtocolExporterEventSource.Log, EventLevel.Warning);

        var services = new ServiceCollection();

        var invocations = 0;

        services.AddHttpClient("OtlpLogExporter", configureClient: (client) => invocations++);

        var serviceProvider = services.BuildServiceProvider();

        var options = new OtlpExporterOptions
        {
            Protocol = OtlpExportProtocol.HttpProtobuf,
            TimeoutMilliseconds = 1234,
        };

        Assert.True(options.TryEnableIHttpClientFactoryIntegration(serviceProvider, "OtlpLogExporter"));

        // Simulate the service provider being disposed before the deferred
        // export client is created, e.g. when the first export of logs happens
        // from LoggerProvider.Dispose() while the container is being disposed.
        serviceProvider.Dispose();

        using var client = options.HttpClientFactory();

        Assert.NotNull(client);
        Assert.Equal(0, invocations);
        Assert.Equal(TimeSpan.FromMilliseconds(1234), client.Timeout);

        var message = Assert.Single(listener.CurrentMessages);
        Assert.Equal(42, message.EventId);
        Assert.Equal(EventLevel.Warning, message.Level);
        Assert.Equal("OtlpLogExporter", message.Payload?[0]);
    }

    [Theory]
    [InlineData(typeof(ObjectDisposedException))]
    [InlineData(typeof(InvalidOperationException))]
    public void TryEnableIHttpClientFactoryIntegration_DoesNotFallBack_WhenCreateClientThrows(Type exceptionType)
    {
        using var listener = new TestEventListener(OpenTelemetryProtocolExporterEventSource.Log, EventLevel.Warning);

        var services = new ServiceCollection();

        var exception = (Exception)Activator.CreateInstance(exceptionType, "Named client pipeline failed.")!;
        var httpClientFactory = new ThrowingHttpClientFactory(exception);

        services.AddSingleton<IHttpClientFactory>(httpClientFactory);

        using var serviceProvider = services.BuildServiceProvider();

        var options = new OtlpExporterOptions { Protocol = OtlpExportProtocol.HttpProtobuf };

        Assert.True(options.TryEnableIHttpClientFactoryIntegration(serviceProvider, "OtlpLogExporter"));

        // Only a failure to resolve IHttpClientFactory from a disposed container
        // falls back to the default HttpClient. Failures while creating the
        // named client itself (including an ObjectDisposedException thrown by a
        // user's handler) must surface so a configured pipeline, e.g. one that
        // adds authentication, is never silently bypassed while the container
        // is healthy.
        var actual = Assert.Throws<TargetInvocationException>(() => options.HttpClientFactory());

        Assert.Same(exception, actual.InnerException);
        Assert.Equal(["OtlpLogExporter"], httpClientFactory.RequestedNames);
        Assert.Empty(listener.CurrentMessages);
    }

    [Fact]
    public void DefaultHttpClientFactory_CreatesHttpClientThatDoesNotFollowRedirects()
    {
        var requestedPaths = new ConcurrentQueue<string>();

        using var server = RunRedirectingServer(requestedPaths, out var host, out var port);

        var options = new OtlpExporterOptions
        {
            Endpoint = new Uri($"http://{host}:{port}/v1/traces"),
            Protocol = OtlpExportProtocol.HttpProtobuf,
        };

        using var httpClient = options.DefaultHttpClientFactory();

        var exportClient = new OtlpHttpExportClient(options, httpClient, string.Empty);

        var response = exportClient.SendExportRequest(
            "hello world"u8.ToArray(),
            11,
            DateTime.UtcNow.AddMinutes(1),
            TestContext.Current.CancellationToken);

        Assert.False(response.Success);
        Assert.Equal(HttpStatusCode.TemporaryRedirect, Assert.IsType<ExportClientHttpResponse>(response).StatusCode);
        Assert.Equal(["/v1/traces"], requestedPaths);
    }

    [Theory]
#pragma warning disable CS0618 // Suppressing gRPC obsolete warning
    [InlineData(OtlpExportProtocol.Grpc, typeof(OtlpGrpcExportClient), false, 10000, null)]
    [InlineData(OtlpExportProtocol.Grpc, typeof(OtlpGrpcExportClient), false, 10000, "in_memory")]
#pragma warning restore CS0618 // Suppressing gRPC obsolete warning
    [InlineData(OtlpExportProtocol.HttpProtobuf, typeof(OtlpHttpExportClient), false, 10000, null)]
    [InlineData(OtlpExportProtocol.HttpProtobuf, typeof(OtlpHttpExportClient), true, 8000, null)]
    [InlineData(OtlpExportProtocol.HttpProtobuf, typeof(OtlpHttpExportClient), false, 10000, "in_memory")]
    [InlineData(OtlpExportProtocol.HttpProtobuf, typeof(OtlpHttpExportClient), true, 8000, "in_memory")]
    public void GetTransmissionHandler_InitializesCorrectHandlerExportClientAndTimeoutValue(OtlpExportProtocol protocol, Type exportClientType, bool customHttpClient, int expectedTimeoutMilliseconds, string? retryStrategy)
    {
        var exporterOptions = new OtlpExporterOptions() { Protocol = protocol };
        if (customHttpClient)
        {
            exporterOptions.HttpClientFactory = () =>
            {
                return new HttpClient { Timeout = TimeSpan.FromMilliseconds(expectedTimeoutMilliseconds) };
            };
        }

        var configuration = new ConfigurationBuilder()
         .AddInMemoryCollection(new Dictionary<string, string?> { [ExperimentalOptions.OtlpRetryEnvVar] = retryStrategy })
         .Build();

        using var transmissionHandler = exporterOptions.GetExportTransmissionHandler(new ExperimentalOptions(configuration), OtlpSignalType.Traces);
        AssertTransmissionHandler(transmissionHandler, exportClientType, expectedTimeoutMilliseconds, retryStrategy);
    }

    [Theory]
#pragma warning disable CS0618 // Suppressing gRPC obsolete warning
    [InlineData(OtlpExportProtocol.Grpc, typeof(OtlpGrpcExportClient), false, 10000)]
#pragma warning restore CS0618 // Suppressing gRPC obsolete warning
    [InlineData(OtlpExportProtocol.HttpProtobuf, typeof(OtlpHttpExportClient), false, 10000)]
    [InlineData(OtlpExportProtocol.HttpProtobuf, typeof(OtlpHttpExportClient), true, 8000)]
    public void GetTransmissionHandler_DiskRetryWithDirectory_InitializesCorrectHandlerExportClientAndTimeoutValue(OtlpExportProtocol protocol, Type exportClientType, bool customHttpClient, int expectedTimeoutMilliseconds)
    {
        var exporterOptions = new OtlpExporterOptions() { Protocol = protocol };
        if (customHttpClient)
        {
            exporterOptions.HttpClientFactory = () =>
            {
                return new HttpClient { Timeout = TimeSpan.FromMilliseconds(expectedTimeoutMilliseconds) };
            };
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                [ExperimentalOptions.OtlpRetryEnvVar] = "disk",
                [ExperimentalOptions.OtlpDiskRetryDirectoryPathEnvVar] = Path.GetTempPath(),
            })
            .Build();

        using var transmissionHandler = exporterOptions.GetExportTransmissionHandler(new ExperimentalOptions(configuration), OtlpSignalType.Traces);
        AssertTransmissionHandler(transmissionHandler, exportClientType, expectedTimeoutMilliseconds, "disk");
    }

    [Fact]
    public void GetTransmissionHandler_DiskRetryWithoutDirectory_Throws()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [ExperimentalOptions.OtlpRetryEnvVar] = "disk" })
            .Build();

        var exception = Assert.Throws<NotSupportedException>(() => new ExperimentalOptions(configuration));
        Assert.Contains(ExperimentalOptions.OtlpDiskRetryDirectoryPathEnvVar, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData("false", false)]
    [InlineData("False", false)]
    [InlineData("invalid", true)]
    public void ExperimentalOptions_DiskRetryCheckPermissions_IsReadFromConfiguration(string? value, bool expected)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                [ExperimentalOptions.OtlpRetryEnvVar] = "disk",
                [ExperimentalOptions.OtlpDiskRetryDirectoryPathEnvVar] = Path.GetTempPath(),
                [ExperimentalOptions.OtlpDiskRetryCheckPermissionsEnvVar] = value,
            })
            .Build();

        var options = new ExperimentalOptions(configuration);

        Assert.Equal(expected, options.DiskRetryCheckPermissions);
    }

#if NET
    [Theory]
    [InlineData(null)]
    [InlineData("true")]
    [InlineData("false")]
    public void GetTransmissionHandler_DiskRetryWithDirectoryWritableByOtherUsers_ChecksPermissionsUnlessDisabled(string? checkPermissions)
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Unix file modes do not apply on Windows.");
            return;
        }

        const UnixFileMode WritableByAll =
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;

        using var root = new TemporaryDirectory();

        // For example, a Kubernetes emptyDir volume
        var retryRootPath = Path.Combine(root.Path, "retry");
        Directory.CreateDirectory(retryRootPath);
        File.SetUnixFileMode(retryRootPath, WritableByAll);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                [ExperimentalOptions.OtlpRetryEnvVar] = "disk",
                [ExperimentalOptions.OtlpDiskRetryDirectoryPathEnvVar] = retryRootPath,
                [ExperimentalOptions.OtlpDiskRetryCheckPermissionsEnvVar] = checkPermissions,
            })
            .Build();

        var exporterOptions = new OtlpExporterOptions();
        var experimentalOptions = new ExperimentalOptions(configuration);

        if (checkPermissions == "false")
        {
            using var transmissionHandler = exporterOptions.GetExportTransmissionHandler(experimentalOptions, OtlpSignalType.Traces);
            Assert.StartsWith(retryRootPath, GetStorageDirectoryPath(transmissionHandler), StringComparison.Ordinal);
        }
        else
        {
            Assert.Throws<UnauthorizedAccessException>(() => exporterOptions.GetExportTransmissionHandler(experimentalOptions, OtlpSignalType.Traces));
        }
    }
#endif

    [Theory]
    [InlineData("Traces", "traces")]
    [InlineData("Logs", "logs")]
    [InlineData("Metrics", "metrics")]
    public void GetTransmissionHandler_DiskRetry_UsesSignalSpecificStorageDirectory(string signalTypeName, string expectedDirectoryName)
    {
        var retryRootPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        try
        {
            var exporterOptions = new OtlpExporterOptions();
            var signalType = signalTypeName switch
            {
                "Logs" => OtlpSignalType.Logs,
                "Metrics" => OtlpSignalType.Metrics,
                "Traces" => OtlpSignalType.Traces,
                _ => throw new ArgumentOutOfRangeException(nameof(signalTypeName)),
            };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    [ExperimentalOptions.OtlpRetryEnvVar] = "disk",
                    [ExperimentalOptions.OtlpDiskRetryDirectoryPathEnvVar] = retryRootPath,
                })
                .Build();

            using var transmissionHandler = exporterOptions.GetExportTransmissionHandler(new ExperimentalOptions(configuration), signalType);

            Assert.Equal(Path.Combine(retryRootPath, expectedDirectoryName), Path.GetDirectoryName(GetStorageDirectoryPath(transmissionHandler)));
        }
        finally
        {
            if (Directory.Exists(retryRootPath))
            {
                Directory.Delete(retryRootPath, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("http://collector-a:4318/v1/traces", "api-key=a", "http://collector-a:4318/v1/traces", "api-key=a", true)]
    [InlineData("http://collector-a:4318/v1/traces", "api-key=a", "http://collector-b:4318/v1/traces", "api-key=a", false)]
    [InlineData("http://collector-a:4318/v1/traces", "api-key=a", "http://collector-a:4318/v1/traces", "api-key=b", false)]
    [InlineData("http://collector-a:4318/v1/traces", "api-key=a", "http://collector-a:4318/v1/traces", null, false)]
    [InlineData("http://collector-a:4318/v1/traces", null, "https://collector-a:4318/v1/traces", null, false)]
    public void GetTransmissionHandler_DiskRetry_UsesDestinationSpecificStorageDirectory(string firstEndpoint, string? firstHeaders, string secondEndpoint, string? secondHeaders, bool expectSameDirectory)
        => AssertStorageDirectories(
            new() { Protocol = OtlpExportProtocol.HttpProtobuf, Endpoint = new Uri(firstEndpoint), Headers = firstHeaders },
            new() { Protocol = OtlpExportProtocol.HttpProtobuf, Endpoint = new Uri(secondEndpoint), Headers = secondHeaders },
            expectSameDirectory);

    [Fact]
    public void GetTransmissionHandler_DiskRetry_UsesProtocolSpecificStorageDirectory()
    {
        var endpoint = new Uri("http://collector:4317");

        AssertStorageDirectories(
#pragma warning disable CS0618 // Suppressing gRPC obsolete warning
            new() { Protocol = OtlpExportProtocol.Grpc, Endpoint = endpoint },
#pragma warning restore CS0618 // Suppressing gRPC obsolete warning
            new() { Protocol = OtlpExportProtocol.HttpProtobuf, Endpoint = endpoint },
            expectSameDirectory: false);
    }

    [Fact]
    public void GetTransmissionHandler_DiskRetry_DoesNotRetryRequestsStoredForAnotherDestination()
    {
        var retryRootPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        try
        {
            var experimentalOptions = CreateDiskRetryExperimentalOptions(retryRootPath);

            using var firstHttpHandler = new UnavailableHttpMessageHandler();
            using var secondHttpHandler = new TestHttpMessageHandler();

            var firstOptions = new OtlpExporterOptions
            {
                Protocol = OtlpExportProtocol.HttpProtobuf,
                Endpoint = new Uri("http://collector-a:4318/v1/traces"),
                HttpClientFactory = () => new HttpClient(firstHttpHandler, disposeHandler: false),
            };

            var secondOptions = new OtlpExporterOptions
            {
                Protocol = OtlpExportProtocol.HttpProtobuf,
                Endpoint = new Uri("http://collector-b:4318/v1/traces"),
                HttpClientFactory = () => new HttpClient(secondHttpHandler, disposeHandler: false),
            };

            using var firstHandler = Assert.IsType<OtlpExporterPersistentStorageTransmissionHandler>(firstOptions.GetExportTransmissionHandler(experimentalOptions, OtlpSignalType.Traces));
            using var secondHandler = Assert.IsType<OtlpExporterPersistentStorageTransmissionHandler>(secondOptions.GetExportTransmissionHandler(experimentalOptions, OtlpSignalType.Traces));

            Assert.True(firstHandler.TrySubmitRequest([1, 2, 3], 3));
            Assert.Equal(1, firstHttpHandler.RequestCount);

            Assert.True(secondHandler.InitiateAndWaitForRetryProcess(10_000));
            Assert.Null(secondHttpHandler.HttpRequestMessage);

            Assert.True(firstHandler.InitiateAndWaitForRetryProcess(10_000));
            Assert.Equal(2, firstHttpHandler.RequestCount);
        }
        finally
        {
            if (Directory.Exists(retryRootPath))
            {
                Directory.Delete(retryRootPath, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData(3)] // Profiles
    [InlineData(int.MaxValue)] // Invalid/Unknown signal type
    public void GetTransmissionHandler_DiskRetry_UnsupportedSignalType_ThrowsNotSupportedException(int signalTypeValue)
    {
        var retryRootPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        try
        {
            var exporterOptions = new OtlpExporterOptions();
            var signalType = (OtlpSignalType)signalTypeValue;

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    [ExperimentalOptions.OtlpRetryEnvVar] = "disk",
                    [ExperimentalOptions.OtlpDiskRetryDirectoryPathEnvVar] = retryRootPath,
                })
                .Build();

            Assert.Throws<NotSupportedException>(
                () => exporterOptions.GetExportTransmissionHandler(new ExperimentalOptions(configuration), signalType));
        }
        finally
        {
            if (Directory.Exists(retryRootPath))
            {
                Directory.Delete(retryRootPath, recursive: true);
            }
        }
    }

    private static ExperimentalOptions CreateDiskRetryExperimentalOptions(string retryRootPath)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                [ExperimentalOptions.OtlpRetryEnvVar] = "disk",
                [ExperimentalOptions.OtlpDiskRetryDirectoryPathEnvVar] = retryRootPath,
            })
            .Build();

        return new ExperimentalOptions(configuration);
    }

    private static string GetStorageDirectoryPath(OtlpExporterTransmissionHandler transmissionHandler)
    {
        var persistentStorageTransmissionHandler = Assert.IsType<OtlpExporterPersistentStorageTransmissionHandler>(transmissionHandler);

        var fileBlobProviderField = typeof(OtlpExporterPersistentStorageTransmissionHandler).GetField("persistentBlobProvider", BindingFlags.NonPublic | BindingFlags.Instance);
        var persistentBlobProvider = Assert.IsType<FileBlobProvider>(fileBlobProviderField?.GetValue(persistentStorageTransmissionHandler));

        return persistentBlobProvider.DirectoryPath;
    }

    private static void AssertStorageDirectories(OtlpExporterOptions firstOptions, OtlpExporterOptions secondOptions, bool expectSameDirectory)
    {
        var retryRootPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        try
        {
            var experimentalOptions = CreateDiskRetryExperimentalOptions(retryRootPath);

            using var firstHandler = firstOptions.GetExportTransmissionHandler(experimentalOptions, OtlpSignalType.Traces);
            using var secondHandler = secondOptions.GetExportTransmissionHandler(experimentalOptions, OtlpSignalType.Traces);

            if (expectSameDirectory)
            {
                Assert.Equal(GetStorageDirectoryPath(firstHandler), GetStorageDirectoryPath(secondHandler));
            }
            else
            {
                Assert.NotEqual(GetStorageDirectoryPath(firstHandler), GetStorageDirectoryPath(secondHandler));
            }
        }
        finally
        {
            if (Directory.Exists(retryRootPath))
            {
                Directory.Delete(retryRootPath, recursive: true);
            }
        }
    }

    private static void AssertTransmissionHandler(OtlpExporterTransmissionHandler transmissionHandler, Type exportClientType, int expectedTimeoutMilliseconds, string? retryStrategy)
    {
        if (retryStrategy == "in_memory")
        {
            Assert.IsType<OtlpExporterRetryTransmissionHandler>(transmissionHandler);
        }
        else if (retryStrategy == "disk")
        {
            Assert.IsType<OtlpExporterPersistentStorageTransmissionHandler>(transmissionHandler);
        }
        else
        {
            Assert.IsType<OtlpExporterTransmissionHandler>(transmissionHandler);
        }

        Assert.Equal(exportClientType, transmissionHandler.ExportClient.GetType());

        Assert.Equal(expectedTimeoutMilliseconds, transmissionHandler.TimeoutMilliseconds);
    }

    /// <summary>
    /// Validates whether the `Headers` property in `OtlpExporterOptions` is correctly processed and parsed.
    /// It also verifies that the extracted headers match the expected values and checks for expected exceptions.
    /// </summary>
    /// <param name="inputOptionHeaders">The raw header string assigned to `OtlpExporterOptions`.
    /// The format should be "key1=value1,key2=value2" (comma-separated key-value pairs).</param>
    /// <param name="expectedNormalizedOptional">A string representing expected additional headers.
    /// This will be parsed into a dictionary and compared with the actual extracted headers.</param>
    /// <param name="expectException">If `true`, the method expects `GetHeaders` to throw an `ArgumentException`
    /// when processing `inputOptionHeaders`.</param>
    private static void VerifyHeaders(string inputOptionHeaders, string expectedNormalizedOptional, bool expectException = false)
    {
        var options = new OtlpExporterOptions { Headers = inputOptionHeaders };

        if (expectException)
        {
            Assert.Throws<ArgumentException>(() =>
                options.GetHeaders<Dictionary<string, string>>((d, k, v) => d.Add(k, v)));
            return;
        }

        var headers = options.GetHeaders<Dictionary<string, string>>((d, k, v) => d.Add(k, v));
        var expectedOptional = new Dictionary<string, string>();

        if (!string.IsNullOrEmpty(expectedNormalizedOptional))
        {
            foreach (var segment in expectedNormalizedOptional.Split([','], StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = segment.Split(['='], 2);
                expectedOptional.Add(parts[0].Trim(), parts[1].Trim());
            }
        }

        Assert.Equal(options.StandardHeaders.Length + expectedOptional.Count, headers.Count);

        foreach (var kvp in expectedOptional)
        {
            Assert.Contains(headers, h => h.Key == kvp.Key && h.Value == kvp.Value);
        }

        foreach (var std in options.StandardHeaders)
        {
            Assert.Contains(headers, h => h.Key == std.Key && h.Value == std.Value);
        }
    }

    private static IDisposable RunRedirectingServer(ConcurrentQueue<string> requestedPaths, out string host, out int port)
        => TestHttpServer.RunServer(
            context =>
            {
                requestedPaths.Enqueue(context.Request.Url!.AbsolutePath);
                context.Request.InputStream.CopyTo(Stream.Null);

                if (context.Request.Url.AbsolutePath != "/redirected")
                {
                    context.Response.StatusCode = (int)HttpStatusCode.TemporaryRedirect;
                    context.Response.RedirectLocation = "/redirected";
                }

                context.Response.OutputStream.Close();
            },
            out host,
            out port);

    private sealed class UnavailableHttpMessageHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            this.RequestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        }

#if NET
        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            this.RequestCount++;
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        }
#endif
    }

    private sealed class ThrowingHttpClientFactory(Exception exception) : IHttpClientFactory
    {
        public List<string> RequestedNames { get; } = [];

        public HttpClient CreateClient(string name)
        {
            this.RequestedNames.Add(name);
            throw exception;
        }
    }
}

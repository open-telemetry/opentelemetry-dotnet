// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Net.Http;
#endif
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Transmission;
using OpenTelemetry.Logs;
using OpenTelemetry.PersistentStorage.FileSystem;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

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

        var transmissionHandler = exporterOptions.GetExportTransmissionHandler(new ExperimentalOptions(configuration), OtlpSignalType.Traces);
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

        var transmissionHandler = exporterOptions.GetExportTransmissionHandler(new ExperimentalOptions(configuration), OtlpSignalType.Traces);
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

            var transmissionHandler = exporterOptions.GetExportTransmissionHandler(new ExperimentalOptions(configuration), signalType);
            var persistentStorageTransmissionHandler = Assert.IsType<OtlpExporterPersistentStorageTransmissionHandler>(transmissionHandler);

            var fileBlobProviderField = typeof(OtlpExporterPersistentStorageTransmissionHandler).GetField("persistentBlobProvider", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var persistentBlobProvider = Assert.IsType<FileBlobProvider>(fileBlobProviderField?.GetValue(persistentStorageTransmissionHandler));

            Assert.EndsWith(expectedDirectoryName, persistentBlobProvider.DirectoryPath, StringComparison.Ordinal);
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

    [Fact]
    public void IsIHttpClientFactorySafeForLogExporter_ReturnsFalse_WhenHttpClientFactoryNotRegistered()
    {
        // An empty service provider has no IHttpClientFactory — must be treated as unsafe.
        using var serviceProvider = new ServiceCollection().BuildServiceProvider();

        var result = OtlpLogExporterHelperExtensions.IsIHttpClientFactorySafeForLogExporter(serviceProvider);

        Assert.False(result);
    }

    [Fact]
    public void IsIHttpClientFactorySafeForLogExporter_ReturnsFalse_WhenCustomHttpClientFactoryRegistered()
    {
        // A hand-rolled IHttpClientFactory has no "_logger" field at all — must be treated as unsafe
        // because we cannot verify the lazy-logger contract.
        var services = new ServiceCollection();
        services.AddSingleton<System.Net.Http.IHttpClientFactory>(new NoLoggerFieldHttpClientFactory());
        using var serviceProvider = services.BuildServiceProvider();

        var result = OtlpLogExporterHelperExtensions.IsIHttpClientFactorySafeForLogExporter(serviceProvider);

        Assert.False(result);
    }

    [Fact]
    public void IsIHttpClientFactorySafeForLogExporter_ReturnsFalse_WhenHttpClientFactoryHasEagerLoggerField()
    {
        // Simulate the old DefaultHttpClientFactory whose "_logger" field is plain ILogger (not Lazy<>).
        var services = new ServiceCollection();
        services.AddSingleton<System.Net.Http.IHttpClientFactory>(new EagerLoggerHttpClientFactory());
        using var serviceProvider = services.BuildServiceProvider();

        var result = OtlpLogExporterHelperExtensions.IsIHttpClientFactorySafeForLogExporter(serviceProvider);

        Assert.False(result);
    }

    [Fact]
    public void IsIHttpClientFactorySafeForLogExporter_ReturnsTrue_WhenHttpClientFactoryHasLazyLoggerField()
    {
        // Simulate the fixed DefaultHttpClientFactory whose "_logger" field is Lazy<ILogger>.
        var services = new ServiceCollection();
        services.AddSingleton<System.Net.Http.IHttpClientFactory>(new LazyLoggerHttpClientFactory());
        using var serviceProvider = services.BuildServiceProvider();

        var result = OtlpLogExporterHelperExtensions.IsIHttpClientFactorySafeForLogExporter(serviceProvider);

        Assert.True(result);
    }

    [Fact]
    public void IsIHttpClientFactorySafeForLogExporter_WithRealAddHttpClient_MatchesRuntimeBehavior()
    {
        var services = new ServiceCollection();
        services.AddHttpClient();
        using var serviceProvider = services.BuildServiceProvider();

        var isSafe = OtlpLogExporterHelperExtensions.IsIHttpClientFactorySafeForLogExporter(serviceProvider);

        // Independently verify the field type by inspecting the concrete factory instance.
        var httpClientFactoryType = Type.GetType("System.Net.Http.IHttpClientFactory, Microsoft.Extensions.Http", throwOnError: false);
        Assert.NotNull(httpClientFactoryType);

        var httpClientFactory = serviceProvider.GetService(httpClientFactoryType);
        Assert.NotNull(httpClientFactory);

        var concreteType = httpClientFactory.GetType();
        FieldInfo? loggerField = null;
        for (var t = concreteType; t != null; t = t.BaseType)
        {
            loggerField = t.GetField("_logger", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            if (loggerField != null)
            {
                break;
            }
        }

        if (loggerField == null)
        {
            // No "_logger" field — unknown implementation, expected to be false.
            Assert.False(isSafe);
        }
        else
        {
            var expectedSafe = loggerField.FieldType.IsGenericType
                && loggerField.FieldType.GetGenericTypeDefinition() == typeof(Lazy<>);
            Assert.Equal(expectedSafe, isSafe);
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

    /// <summary>
    /// An <see cref="System.Net.Http.IHttpClientFactory"/> implementation with no <c>_logger</c> field.
    /// Represents a completely custom factory — the safety probe must return <see langword="false"/>.
    /// </summary>
    private sealed class NoLoggerFieldHttpClientFactory : System.Net.Http.IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }

    /// <summary>
    /// Simulates the <em>old</em> <c>DefaultHttpClientFactory</c> whose <c>_logger</c> field is a
    /// plain (non-lazy) <c>ILogger</c> — the version affected by the circular-dependency bug.
    /// The safety probe must return <see langword="false"/>.
    /// </summary>
    private sealed class EagerLoggerHttpClientFactory : System.Net.Http.IHttpClientFactory
    {
        // The field name must be exactly "_logger" to mirror the real DefaultHttpClientFactory.
        // SA1309: suppressed intentionally — the underscore prefix is load-bearing for the reflection probe.
        // CS0169/CS0649/CA1823: suppressed intentionally — only the declared field type matters, not the value.
#pragma warning disable SA1309, CS0169, CS0649, CA1823
        private readonly Microsoft.Extensions.Logging.ILogger? _logger;
#pragma warning restore SA1309, CS0169, CS0649, CA1823

        public HttpClient CreateClient(string name) => new();
    }

    /// <summary>
    /// Simulates the <em>fixed</em> <c>DefaultHttpClientFactory</c> (shipped in <c>Microsoft.Extensions.Http</c> 8.0.0+)
    /// whose <c>_logger</c> field is <c>Lazy&lt;ILogger&gt;</c>.
    /// The safety probe must return <see langword="true"/>.
    /// </summary>
    private sealed class LazyLoggerHttpClientFactory : System.Net.Http.IHttpClientFactory
    {
        // The field name must be exactly "_logger" to mirror the real DefaultHttpClientFactory.
        // SA1309: suppressed intentionally — the underscore prefix is load-bearing for the reflection probe.
        // CS0169/CS0649/CA1823: suppressed intentionally — only the declared field type matters, not the value.
#pragma warning disable SA1309, CS0169, CS0649, CA1823
        private readonly Lazy<Microsoft.Extensions.Logging.ILogger>? _logger;
#pragma warning restore SA1309, CS0169, CS0649, CA1823

        public HttpClient CreateClient(string name) => new();
    }
}

// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net;
#if NETFRAMEWORK
using System.Net.Http;
#endif
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Exporter.Zipkin.Implementation;
using OpenTelemetry.Resources;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Exporter.Zipkin.Tests;

public sealed class ZipkinExporterTests : IDisposable
{
    private const string TraceId = "e8ea7e9ac72de94e91fabc613f9686b2";
    private static readonly ConcurrentDictionary<Guid, string> Responses = new();

    private readonly IDisposable testServer;
    private readonly string testServerHost;
    private readonly int testServerPort;

    static ZipkinExporterTests()
    {
        Activity.DefaultIdFormat = ActivityIdFormat.W3C;
        Activity.ForceDefaultIdFormat = true;
    }

    public ZipkinExporterTests()
    {
        this.testServer = TestHttpServer.RunServer(
            ctx => ProcessServerRequest(ctx),
            out this.testServerHost,
            out this.testServerPort);

        static void ProcessServerRequest(HttpListenerContext context)
        {
            context.Response.StatusCode = 200;

            using StreamReader readStream = new StreamReader(context.Request.InputStream);

            string requestContent = readStream.ReadToEnd();

            Responses.TryAdd(
                Guid.Parse(context.Request.QueryString["requestId"]!),
                requestContent);

            context.Response.OutputStream.Close();
        }
    }

    public void Dispose()
    {
        this.testServer.Dispose();
    }

    [Fact]
    public void AddAddZipkinExporterNamedOptionsSupported()
    {
        int defaultExporterOptionsConfigureOptionsInvocations = 0;
        int namedExporterOptionsConfigureOptionsInvocations = 0;

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .ConfigureServices(services =>
            {
                services.Configure<ZipkinExporterOptions>(o => defaultExporterOptionsConfigureOptionsInvocations++);

                services.Configure<ZipkinExporterOptions>("Exporter2", o => namedExporterOptionsConfigureOptionsInvocations++);
            })
            .AddZipkinExporter()
            .AddZipkinExporter("Exporter2", o => { })
            .Build();

        Assert.Equal(1, defaultExporterOptionsConfigureOptionsInvocations);
        Assert.Equal(1, namedExporterOptionsConfigureOptionsInvocations);
    }

    [Fact]
    public void BadArgs()
    {
        TracerProviderBuilder? builder = null;
        Assert.Throws<ArgumentNullException>(() => builder!.AddZipkinExporter());
    }

    [Fact]
    public void SuppressesInstrumentation()
    {
        const string ActivitySourceName = "zipkin.test";
        Guid requestId = Guid.NewGuid();
        TestActivityProcessor testActivityProcessor = new TestActivityProcessor();

        int endCalledCount = 0;

        testActivityProcessor.EndAction =
            (a) =>
            {
                endCalledCount++;
            };

        var exporterOptions = new ZipkinExporterOptions
        {
            Endpoint = new Uri($"http://{this.testServerHost}:{this.testServerPort}/api/v2/spans?requestId={requestId}"),
        };
        using var zipkinExporter = new ZipkinExporter(exporterOptions);
        using var exportActivityProcessor = new BatchActivityExportProcessor(zipkinExporter);

        var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(ActivitySourceName)
            .AddProcessor(testActivityProcessor)
            .AddProcessor(exportActivityProcessor)
            .AddHttpClientInstrumentation()
            .Build();

        using var source = new ActivitySource(ActivitySourceName);
        using var activity = source.StartActivity("Test Zipkin Activity");
        activity?.Stop();

        // We call ForceFlush on the exporter twice, so that in the event
        // of a regression, this should give any operations performed in
        // the Zipkin exporter itself enough time to be instrumented and
        // loop back through the exporter.
        exportActivityProcessor.ForceFlush();
        exportActivityProcessor.ForceFlush();

        Assert.Equal(1, endCalledCount);
    }

    [Fact]
    public void EndpointConfigurationUsingEnvironmentVariable()
    {
        try
        {
            Environment.SetEnvironmentVariable(ZipkinExporterOptions.ZipkinEndpointEnvVar, "http://urifromenvironmentvariable");

            var exporterOptions = new ZipkinExporterOptions();

            Assert.Equal(new Uri(Environment.GetEnvironmentVariable(ZipkinExporterOptions.ZipkinEndpointEnvVar)!).AbsoluteUri, exporterOptions.Endpoint.AbsoluteUri);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ZipkinExporterOptions.ZipkinEndpointEnvVar, null);
        }
    }

    [Fact]
    public void IncodeEndpointConfigTakesPrecedenceOverEnvironmentVariable()
    {
        try
        {
            Environment.SetEnvironmentVariable(ZipkinExporterOptions.ZipkinEndpointEnvVar, "http://urifromenvironmentvariable");

            var exporterOptions = new ZipkinExporterOptions
            {
                Endpoint = new Uri("http://urifromcode"),
            };

            Assert.Equal(new Uri("http://urifromcode").AbsoluteUri, exporterOptions.Endpoint.AbsoluteUri);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ZipkinExporterOptions.ZipkinEndpointEnvVar, null);
        }
    }

    [Fact]
    public void ErrorGettingUriFromEnvVarSetsDefaultEndpointValue()
    {
        try
        {
            Environment.SetEnvironmentVariable(ZipkinExporterOptions.ZipkinEndpointEnvVar, "InvalidUri");

            var options = new ZipkinExporterOptions();

            Assert.Equal(new Uri(ZipkinExporterOptions.DefaultZipkinEndpoint), options.Endpoint);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ZipkinExporterOptions.ZipkinEndpointEnvVar, null);
        }
    }

    [Fact]
    public void EndpointConfigurationUsingIConfiguration()
    {
        var values = new Dictionary<string, string>()
        {
            [ZipkinExporterOptions.ZipkinEndpointEnvVar] = "http://custom-endpoint:12345",
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values!)
            .Build();

        var options = new ZipkinExporterOptions(configuration, new());

        Assert.Equal(new Uri("http://custom-endpoint:12345"), options.Endpoint);
    }

    [Fact]
    public void UserHttpFactoryCalled()
    {
        ZipkinExporterOptions options = new ZipkinExporterOptions();

        var defaultFactory = options.HttpClientFactory;

        int invocations = 0;
        options.HttpClientFactory = () =>
        {
            invocations++;
            return defaultFactory();
        };

        using (var exporter = new ZipkinExporter(options))
        {
            Assert.Equal(1, invocations);
        }

        using (var provider = Sdk.CreateTracerProviderBuilder()
            .AddZipkinExporter(o => o.HttpClientFactory = options.HttpClientFactory)
            .Build())
        {
            Assert.Equal(2, invocations);
        }

        using var client = new HttpClient();

        using (var exporter = new ZipkinExporter(options, client))
        {
            // Factory not called when client is passed as a param.
            Assert.Equal(2, invocations);
        }

        options.HttpClientFactory = null!;
        Assert.Throws<InvalidOperationException>(() =>
        {
            using var exporter = new ZipkinExporter(options);
        });

        options.HttpClientFactory = () => null!;
        Assert.Throws<InvalidOperationException>(() =>
        {
            using var exporter = new ZipkinExporter(options);
        });
    }

    [Fact]
    public void ServiceProviderHttpClientFactoryInvoked()
    {
        IServiceCollection services = new ServiceCollection();

        services.AddHttpClient();

        int invocations = 0;

        services.AddHttpClient("ZipkinExporter", configureClient: (client) => invocations++);

        services.AddOpenTelemetry().WithTracing(builder => builder
            .AddZipkinExporter());

        using var serviceProvider = services.BuildServiceProvider();

        var tracerProvider = serviceProvider.GetRequiredService<TracerProvider>();

        Assert.Equal(1, invocations);
    }

    [Fact]
    public void UpdatesServiceNameFromDefaultResource()
    {
        var zipkinExporter = new ZipkinExporter(new ZipkinExporterOptions());

        zipkinExporter.SetLocalEndpointFromResource(Resource.Empty);

        Assert.StartsWith("unknown_service:", zipkinExporter.LocalEndpoint!.ServiceName);
    }

    [Fact]
    public void UpdatesServiceNameFromIConfiguration()
    {
        var tracerProviderBuilder = Sdk.CreateTracerProviderBuilder()
            .ConfigureServices(services =>
            {
                Dictionary<string, string> configuration = new()
                {
                    ["OTEL_SERVICE_NAME"] = "myservicename",
                };

                services.AddSingleton<IConfiguration>(
                    new ConfigurationBuilder().AddInMemoryCollection(configuration!).Build());
            });

        var zipkinExporter = new ZipkinExporter(new ZipkinExporterOptions());

        tracerProviderBuilder.AddProcessor(new BatchActivityExportProcessor(zipkinExporter));

        using var provider = tracerProviderBuilder.Build();

        zipkinExporter.SetLocalEndpointFromResource(Resource.Empty);

        Assert.Equal("myservicename", zipkinExporter.LocalEndpoint!.ServiceName);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    [InlineData(false, false, false, ActivityStatusCode.Ok)]
    [InlineData(false, false, false, ActivityStatusCode.Ok, null, true)]
    [InlineData(false, false, false, ActivityStatusCode.Error)]
    [InlineData(false, false, false, ActivityStatusCode.Error, "Error description")]
    public void IntegrationTest(
        bool useShortTraceIds,
        bool useTestResource,
        bool isRootSpan,
        ActivityStatusCode statusCode = ActivityStatusCode.Unset,
        string? statusDescription = null,
        bool addErrorTag = false)
    {
        Guid requestId = Guid.NewGuid();

        ZipkinExporter exporter = new ZipkinExporter(
            new ZipkinExporterOptions
            {
                Endpoint = new Uri($"http://{this.testServerHost}:{this.testServerPort}/api/v2/spans?requestId={requestId}"),
                UseShortTraceIds = useShortTraceIds,
            });

        var serviceName = (string)exporter.ParentProvider.GetDefaultResource().Attributes
            .Where(pair => pair.Key == ResourceSemanticConventions.AttributeServiceName).FirstOrDefault().Value;
        var resourceTags = string.Empty;
        var dateTime = DateTime.UtcNow;
        var activity = CreateTestActivity(isRootSpan: isRootSpan, statusCode: statusCode, statusDescription: statusDescription, dateTime: dateTime);
        if (useTestResource)
        {
            serviceName = "MyService";

            exporter.SetLocalEndpointFromResource(ResourceBuilder.CreateEmpty().AddAttributes(new Dictionary<string, object>
            {
                [ResourceSemanticConventions.AttributeServiceName] = serviceName,
                ["service.tag"] = "hello world",
            }).Build());
        }
        else
        {
            exporter.SetLocalEndpointFromResource(Resource.Empty);
        }

        if (addErrorTag)
        {
            activity.SetTag(ZipkinActivityConversionExtensions.ZipkinErrorFlagTagName, "This should be removed.");
        }

        var processor = new SimpleActivityExportProcessor(exporter);

        processor.OnEnd(activity);

        var context = activity.Context;

        var timestamp = activity.StartTimeUtc.ToEpochMicroseconds();
        var eventTimestamp = activity.Events.First().Timestamp.ToEpochMicroseconds();

        StringBuilder ipInformation = new StringBuilder();
        if (!string.IsNullOrEmpty(exporter.LocalEndpoint!.Ipv4))
        {
            ipInformation.Append($@",""ipv4"":""{exporter.LocalEndpoint.Ipv4}""");
        }

        if (!string.IsNullOrEmpty(exporter.LocalEndpoint.Ipv6))
        {
            ipInformation.Append($@",""ipv6"":""{exporter.LocalEndpoint.Ipv6}""");
        }

        var parentId = isRootSpan ? string.Empty : $@"""parentId"":""{ZipkinActivityConversionExtensions.EncodeSpanId(activity.ParentSpanId)}"",";

        var traceId = useShortTraceIds ? TraceId.Substring(TraceId.Length - 16, 16) : TraceId;

        string statusTag;
        string errorTag = string.Empty;
        switch (statusCode)
        {
            case ActivityStatusCode.Ok:
                statusTag = $@"""{SpanAttributeConstants.StatusCodeKey}"":""OK"",";
                break;
            case ActivityStatusCode.Unset:
                statusTag = string.Empty;
                break;
            case ActivityStatusCode.Error:
                statusTag = $@"""{SpanAttributeConstants.StatusCodeKey}"":""ERROR"",";
                errorTag = $@"""{ZipkinActivityConversionExtensions.ZipkinErrorFlagTagName}"":""{statusDescription}"",";
                break;
            default:
                throw new NotSupportedException();
        }

        Assert.Equal(
            $@"[{{""traceId"":""{traceId}"","
            + @"""name"":""Name"","
            + parentId
            + $@"""id"":""{ZipkinActivityConversionExtensions.EncodeSpanId(context.SpanId)}"","
            + @"""kind"":""CLIENT"","
            + $@"""timestamp"":{timestamp},"
            + @"""duration"":60000000,"
            + $@"""localEndpoint"":{{""serviceName"":""{serviceName}""{ipInformation}}},"
            + @"""remoteEndpoint"":{""serviceName"":""http://localhost:44312/""},"
            + $@"""annotations"":[{{""timestamp"":{eventTimestamp},""value"":""Event1""}},{{""timestamp"":{eventTimestamp},""value"":""Event2""}}],"
            + @"""tags"":{"
                + resourceTags
                + $@"""stringKey"":""value"","
                + @"""longKey"":""1"","
                + @"""longKey2"":""1"","
                + @"""doubleKey"":""1"","
                + @"""doubleKey2"":""1"","
                + @"""longArrayKey"":""[1,2]"","
                + @"""boolKey"":""true"","
                + @"""boolArrayKey"":""[true,false]"","
                + @"""http.host"":""http://localhost:44312/"","
                + $@"""dateTimeKey"":""{Convert.ToString(dateTime, CultureInfo.InvariantCulture)}"","
                + $@"""dateTimeArrayKey"":""[\u0022{Convert.ToString(dateTime, CultureInfo.InvariantCulture)}\u0022]"","
                + statusTag
                + errorTag
                + @"""otel.scope.name"":""CreateTestActivity"","
                + @"""otel.library.name"":""CreateTestActivity"""
            + "}}]",
            Responses[requestId]);
    }

    internal static Activity CreateTestActivity(
       bool isRootSpan = false,
       bool setAttributes = true,
       Dictionary<string, object>? additionalAttributes = null,
       bool addEvents = true,
       bool addLinks = true,
       Resource? resource = null,
       ActivityKind kind = ActivityKind.Client,
       ActivityStatusCode statusCode = ActivityStatusCode.Unset,
       string? statusDescription = null,
       DateTime? dateTime = null)
    {
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => options.Parent.TraceFlags.HasFlag(ActivityTraceFlags.Recorded)
                ? ActivitySamplingResult.AllDataAndRecorded
                : ActivitySamplingResult.AllData,
        };

        ActivitySource.AddActivityListener(activityListener);

        var startTimestamp = DateTime.UtcNow;
        var endTimestamp = startTimestamp.AddSeconds(60);
        var eventTimestamp = DateTime.UtcNow;
        var traceId = ActivityTraceId.CreateFromString("e8ea7e9ac72de94e91fabc613f9686b2".AsSpan());

        dateTime ??= DateTime.UtcNow;

        var parentSpanId = isRootSpan ? default : ActivitySpanId.CreateFromBytes(new byte[] { 12, 23, 34, 45, 56, 67, 78, 89 });

        var attributes = new Dictionary<string, object>
        {
            { "stringKey", "value" },
            { "longKey", 1L },
            { "longKey2", 1 },
            { "doubleKey", 1D },
            { "doubleKey2", 1F },
            { "longArrayKey", new long[] { 1, 2 } },
            { "boolKey", true },
            { "boolArrayKey", new bool[] { true, false } },
            { "http.host", "http://localhost:44312/" }, // simulating instrumentation tag adding http.host
            { "dateTimeKey", dateTime.Value },
            { "dateTimeArrayKey", new DateTime[] { dateTime.Value } },
        };
        if (additionalAttributes != null)
        {
            foreach (var attribute in additionalAttributes)
            {
                if (!attributes.ContainsKey(attribute.Key))
                {
                    attributes.Add(attribute.Key, attribute.Value);
                }
            }
        }

        var events = new List<ActivityEvent>
        {
            new ActivityEvent(
                "Event1",
                eventTimestamp,
                new ActivityTagsCollection(new Dictionary<string, object?>
                {
                    { "key", "value" },
                })),
            new ActivityEvent(
                "Event2",
                eventTimestamp,
                new ActivityTagsCollection(new Dictionary<string, object?>
                {
                    { "key", "value" },
                })),
        };

        var linkedSpanId = ActivitySpanId.CreateFromString("888915b6286b9c41".AsSpan());

        var activitySource = new ActivitySource(nameof(CreateTestActivity));

        var tags = setAttributes ?
                attributes.Select(kvp => new KeyValuePair<string, object?>(kvp.Key, kvp.Value))
                : null;
        var links = addLinks ?
                new[]
                {
                    new ActivityLink(new ActivityContext(
                        traceId,
                        linkedSpanId,
                        ActivityTraceFlags.Recorded)),
                }
                : null;

        var activity = activitySource.StartActivity(
            "Name",
            kind,
            parentContext: new ActivityContext(traceId, parentSpanId, ActivityTraceFlags.Recorded),
            tags,
            links,
            startTime: startTimestamp)!;

        Assert.NotNull(activity);

        if (addEvents)
        {
            foreach (var evnt in events)
            {
                activity.AddEvent(evnt);
            }
        }

        activity.SetStatus(statusCode, statusDescription);

        activity.SetEndTime(endTimestamp);
        activity.Stop();

        return activity;
    }
}

using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Trace;
using PerfTestAspNetCore;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var instrumentationOptions = new InstrumentationOptions();

builder.Configuration.GetSection("InstrumentationOptions").Bind(instrumentationOptions);

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

Console.WriteLine("DiagnosticSourceListener-WithoutActivityEnrichment-Enabled: " + instrumentationOptions.EnableDiagnosticSource);
Console.WriteLine("OTelInstrumentation-Enabled: " + instrumentationOptions.EnableOTel);
Console.WriteLine("Telemetry-Middleware-Enabled: " + instrumentationOptions.EnableMiddleware);

if (instrumentationOptions.EnableDiagnosticSource)
{
    ActivitySource.AddActivityListener(new ActivityListener
    {
        ShouldListenTo = source => source.Name == "Microsoft.AspNetCore",
        Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded,
        ActivityStarted = activity => { },
        ActivityStopped = activity => { },
    });

    DiagnosticListener.AllListeners.Subscribe(new DiagnosticSourceSubscriber());
}

if (instrumentationOptions.EnableOTel)
{
    Environment.SetEnvironmentVariable("OTEL_SEMCONV_STABILITY_OPT_IN", "http");
    // Set default propagator so that overhead is not caused by context extraction.
    // Set EnableGrpcAspNetCoreSupport = false, avoid additional work to find grpc tags.
    Sdk.SetDefaultTextMapPropagator(new TraceContextPropagator());
    Sdk.CreateTracerProviderBuilder()
        .SetSampler(new AlwaysOnSampler())
        .AddAspNetCoreInstrumentation(o => o.EnableGrpcAspNetCoreSupport = false)
        .Build();
}

if (instrumentationOptions.EnableMiddleware)
{
    builder.Services.AddSingleton<TelemetryMiddleware>();

    ActivitySource.AddActivityListener(new ActivityListener
    {
        ShouldListenTo = source => source.Name == "Microsoft.AspNetCore",
        Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded,
        ActivityStarted = activity => { },
        ActivityStopped = activity => { },
    });
}

builder.Logging.ClearProviders();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

if (instrumentationOptions.EnableMiddleware)
{
    app.UseMiddleware<TelemetryMiddleware>();
}

app.UseAuthorization();

app.MapControllers();

app.Run();

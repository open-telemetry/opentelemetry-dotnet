using System.Diagnostics;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using TestAspNetCoreInstrumentationPerf;

var builder = WebApplication.CreateBuilder(args);

var instrumentationOptions = new InstrumentationOptions();
builder.Configuration.GetSection("InstrumentationOptions").Bind(instrumentationOptions);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

Console.WriteLine("EnableMetricInstrumentation: " + instrumentationOptions.EnableMetricInstrumentation);
Console.WriteLine("EnableTraceInstrumentation: " + instrumentationOptions.EnableTraceInstrumentation);
Console.WriteLine("EnableDiagnosticSourceSubscription: " + instrumentationOptions.EnableDiagnosticSourceSubscription);

if (instrumentationOptions.EnableMetricInstrumentation)
{
    builder.Services.AddOpenTelemetry().WithMetrics(builder => builder.AddAspNetCoreInstrumentation().AddReader(new PeriodicExportingMetricReader(new MyExporter<Metric>("TestExporter"))));
}

if (instrumentationOptions.EnableTraceInstrumentation)
{
    builder.Services.AddOpenTelemetry().WithTracing(builder => builder.AddAspNetCoreInstrumentation());
}

if (instrumentationOptions.EnableDiagnosticSourceSubscription)
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

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

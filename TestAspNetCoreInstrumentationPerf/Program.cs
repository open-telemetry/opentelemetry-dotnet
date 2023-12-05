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

if (instrumentationOptions.EnableMetricInstrumentation)
{
    builder.Services.AddOpenTelemetry().WithMetrics(builder => builder.AddAspNetCoreInstrumentation().AddReader(new PeriodicExportingMetricReader(new MyExporter<Metric>("TestExporter"))));
}

if (instrumentationOptions.EnableTraceInstrumentation)
{
    builder.Services.AddOpenTelemetry().WithTracing(builder => builder.AddAspNetCoreInstrumentation());
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

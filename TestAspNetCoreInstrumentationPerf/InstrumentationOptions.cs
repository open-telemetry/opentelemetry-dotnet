namespace TestAspNetCoreInstrumentationPerf;

public class InstrumentationOptions
{
    public bool EnableTraceInstrumentation { get; set; }

    public bool EnableMetricInstrumentation { get; set; }

    public bool EnableDiagnosticSourceSubscription { get; set; }
}

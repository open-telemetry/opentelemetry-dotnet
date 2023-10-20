namespace PerfTestAspNetCore
{
    public class InstrumentationOptions
    {
        public bool EnableDiagnosticSource { get; set; }

        public bool EnableOTel { get; set; }

        public bool EnableMiddleware { get; set; }
    }
}

namespace LoggingTracer
{
    using OpenTelemetry.Context;
    using OpenTelemetry.Context.Propagation;
    using OpenTelemetry.Trace;

    public class LoggingTracerFactory : ITracerFactory
    {
        public ITracer Create(string name)
        {
            Logger.Log($"TracerFactory.Create({name})");
            // Here
            // - return new Tracer
            // - or return Tracer from a map for the the given name.
            // - or return (new) Tracer based on a condition (e..g config) otherwise NopTracer, ... 
            // - ...
            return new LoggingTracer();
        }
    }
}

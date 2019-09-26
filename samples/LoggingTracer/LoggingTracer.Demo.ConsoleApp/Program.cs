// <copyright file="Program.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LoggingTracer.Demo.ConsoleApp
{
    using System.Threading.Tasks;
    using OpenTelemetry.Trace;

    public class Program
    {
        public static async Task Main(string[] args)
        {
            var tracerFactory = new LoggingTracerFactory();
            var tracer = tracerFactory.GetTracer("ConsoleApp", "semver:1.0.0");
            
            var builder = tracer.SpanBuilder("Main (span1)");
            using (tracer.WithSpan(builder.StartSpan()))
            {
                await Task.Delay(100);
                await Foo(tracer);
            }
        }

        private static async Task Foo(ITracer tracer)
        {
            var builder = tracer.SpanBuilder("Foo (span2)");
            using (tracer.WithSpan(builder.StartSpan()))
            {
                tracer.CurrentSpan.SetAttribute("myattribute", "mvalue");
                await Task.Delay(100);
            }
        }
    }
}

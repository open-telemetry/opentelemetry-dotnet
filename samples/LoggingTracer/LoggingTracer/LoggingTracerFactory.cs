// <copyright file="LoggingTracerFactory.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LoggingTracer
{
    using OpenTelemetry.Context;
    using OpenTelemetry.Context.Propagation;
    using OpenTelemetry.Trace;

    public class LoggingTracerFactory : ITracerFactory
    {
        public ITracer GetTracer(string name, string version = null)
        {
            Logger.Log($"TracerFactory.GetTracer('{name}', '{version}')");
            return new LoggingTracer();
        }
    }
}

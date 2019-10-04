// <copyright file="LoggingTracerFactory.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LoggingTracer
{
    using System.Collections.Generic;
    using OpenTelemetry.Trace;

    public class LoggingTracerFactory : TracerFactoryBase
    {
        public override ITracer GetTracer(string name, string version = null)
        {
            Logger.Log($"ITracerFactory.GetTracer('{name}', '{version}')");

            // Create a Resource from "name" and "version" information.
            var labels = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(name))
            {
                labels.Add("name", name);
                if (!string.IsNullOrEmpty(version))
                {
                    labels.Add("version", version);
                }
            }

            return new LoggingTracer();
        }
    }
}

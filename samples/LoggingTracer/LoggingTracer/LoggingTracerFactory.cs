// <copyright file="LoggingTracerFactory.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System.Collections.Generic;
using OpenTelemetry.Resources;

namespace LoggingTracer
{
    using OpenTelemetry.Context;
    using OpenTelemetry.Context.Propagation;
    using OpenTelemetry.Trace;

    public class LoggingTracerFactory : ITracerFactory
    {
        public override ITracer GetTracer(string name, string version = null)
        {
            Logger.Log($"TracerFactory.GetTracer('{name}', '{version}')");
            
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
            var libraryResource = Resource.Create(labels);
           
            return new LoggingTracer(libraryResource);
        }
    }
}

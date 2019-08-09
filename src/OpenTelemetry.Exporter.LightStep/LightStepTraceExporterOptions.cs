// <auto-generated/>
using System;

namespace OpenTelemetry.Exporter.LightStep
{
    public sealed class LightStepTraceExporterOptions
    {
        public Uri Satellite { get; set; } = new Uri("https://collector.lightstep.com:443/api/v2/reports");
        public TimeSpan SatelliteTimeout { get; set; } = TimeSpan.FromSeconds(10);
        public string ServiceName { get; set; } = "OpenTelemetry Exporter";
        public string AccessToken { get; set; }
    }
}

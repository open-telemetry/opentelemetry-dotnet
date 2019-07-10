using System;
using OpenTelemetry.Stats;
using OpenTelemetry.Stats.Measures;

namespace hellocs.MetricsShim
{
    public class Measure
    {
        #region Fields for Json2Object conversion
        public string Name { get; set; }
        public string Description { get; set; }
        public string Unit { get; set; }
        public string Type { get; set; }
        #endregion
        private IMeasure _reqMeasure;

        public void Setup()
        {
            // Metric type: name, description, unit
            if (Type == nameof(MeasureLong))
                _reqMeasure = MeasureLong.Create(Name, Description, Unit);
            else if (Type == nameof(MeasureDouble))
                _reqMeasure = MeasureDouble.Create(Name, Description, Unit);
            else
                throw new ApplicationException(String.Format("Error Metrics#006: [Exporter/Views/Measure:Setup()] Unsupported Measure Type: {0}", Type));
        }

        public IMeasure getMeasure()
        {
            return _reqMeasure;
        }
    }
}

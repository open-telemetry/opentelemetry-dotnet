using System;
using Newtonsoft.Json.Linq;

namespace hellocs.MetricsShim
{
    public class MetricShim
    {
        public MetricExporter MetricsExp;
        private string _json;
        public MetricShim(string jsonString)
        {
            _json = jsonString;
        }

        public void Start()
        {
            JObject o = JObject.Parse(_json);
            JToken token = o.SelectToken("MetricShim.MetricsExp");
            try
            {
                MetricsExp = token.ToObject<MetricExporter>();
                MetricsExp.Setup();
                MetricsExp.Start();
            }
            catch (NullReferenceException)
            {
                throw new ApplicationException("Don't forget to add your settings for the Metrics exporter in file 'appsettings.json'");
            }
        }
    }
}

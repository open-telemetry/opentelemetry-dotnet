using System;
using System.Collections.Generic;
using OpenTelemetry.Exporter.Prometheus;
using OpenTelemetry.Stats;

namespace hellocs.MetricsShim
{
    public class MetricExporter
    {
        #region Fields for Json2Object conversion
        public String Hostname { get; set; }
        public String ListenOnPort { get; set; }
        public String Path { get; set; }
        public String AppName { get; set; }
        public Dictionary<String, View> Views { get; set; }
        #endregion
        private PrometheusExporter exporter;

        public void Setup()
        {
            IViewManager viewManager = Stats.ViewManager;
            foreach (KeyValuePair<string, View> view in Views)
            {
                view.Value.Setup();
                viewManager.RegisterView(view.Value.toRegister());
            }
            string s_uri = String.Format("http://{0}:{1}{2}{3}/", Hostname, ListenOnPort, Path, AppName);
            Console.WriteLine(String.Format("Metrics listening at: {0}", s_uri));
            exporter = new PrometheusExporter(
            new PrometheusExporterOptions()
            {
                Url = s_uri
            },
            viewManager);
        }

        public void Start()
        {
            // Starting of the prometheus provider (http server)
            exporter.Start();
        }
    }
}

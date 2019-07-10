using System;
using System.Collections.Generic;
using OpenTelemetry.Tags;
using OpenTelemetry.Stats;
using OpenTelemetry.Stats.Aggregations;
using OpenTelemetry.Stats.Measures;

namespace hellocs.MetricsShim
{
    public class View
    {
        #region Fields for Json2Object conversion
        public string Name { get; set; }
        public string Namespace { get; set; }
        public string Description { get; set; }
        public Measure Measure { get; set; }
        public string Aggregation { get; set; }
        public IList<double> Bucket { get; set; }
        public IList<string> TagKeys { get; set; }
        #endregion
        private IList<TagKey> _tags;
        private IMeasure _reqMeasure;
        private IStatsRecorder _statsRecorder;
        private IView _requestView;

        public void Setup()
        {
            _statsRecorder = Stats.StatsRecorder;
            IViewName ReqViewName = ViewName.Create(String.Format("{0}_{1}", Namespace, Name));
            Measure.Setup();
            _reqMeasure = Measure.getMeasure();
            // Counter type, default to 0 at start if wo/params
            IAggregation ReqViewType = this.aggSelect();
            // KeyTag creation
            _tags = new List<TagKey>();
            foreach (string str in TagKeys)
            {
                _tags.Add(TagKey.Create(str));
            }
            if(ReqViewType!=null)
            {
                // Build view: (IViewName name, string description, IMeasure measure, IAggregation aggregation, IReadOnlyList<ITagKey> columns)
                _requestView = OpenTelemetry.Stats.View.Create(
                    ReqViewName,
                    Description,
                    _reqMeasure,
                    ReqViewType,
                    new List<TagKey>(_tags));
            }
            else
                // Build view: (IViewName name, string description, IMeasure measure, IAggregation aggregation, IReadOnlyList<ITagKey> columns)
                _requestView = OpenTelemetry.Stats.View.Create(
                    ReqViewName,
                    Description,
                    _reqMeasure,
                    Count.Create(),
                    new List<TagKey>(_tags));
        }

        public void UpdateView(Dictionary<string, string> dic, int value)
        {
            string code;
            dic.TryGetValue("status_code", out code);
            if (code != null)
            {
                int res_code = Convert.ToInt32(code);
                if (res_code < 400)
                {
                    ITagContextBuilder tagMap = Tags.Tagger.EmptyBuilder;
                    foreach (TagKey tag in _tags)
                    {
                        tagMap.Put(tag, TagValue.Create(dic[tag.Name]));
                    }
                    ITagContext tagContext = tagMap.Build();
                    if (Measure.Type == nameof(MeasureLong))
                        _statsRecorder.NewMeasureMap().Put((MeasureLong)_reqMeasure, value).Record(tagContext);
                    else if (Measure.Type == nameof(MeasureDouble))
                        _statsRecorder.NewMeasureMap().Put((MeasureDouble)_reqMeasure, value).Record(tagContext);
                    else
                        Console.WriteLine(String.Format("Error Metrics#000: [Exporter/Views:UpdateView()] Unsupported Measure Type: {0}", Measure.Type));
                }
                else
                {
                    Console.WriteLine(String.Format("Info Metrics#001: [Exporter/Views:UpdateView()] Response code >= 400 (bad request), not added to metricsExporter: {0}", code));
                }
            }
            else
            {
                Console.WriteLine("Info Metrics#002: [Exporter/Views:UpdateView()] No response code in metrics dictionnary");
                ITagContextBuilder tagMap = Tags.Tagger.EmptyBuilder;
                foreach (TagKey tag in _tags)
                {
                    tagMap.Put(tag, TagValue.Create(dic[tag.Name]));
                }
                ITagContext tagContext = tagMap.Build();
                if (Measure.Type == nameof(MeasureLong))
                    _statsRecorder.NewMeasureMap().Put((MeasureLong)_reqMeasure, value).Record(tagContext);
                else if (Measure.Type == nameof(MeasureDouble))
                    _statsRecorder.NewMeasureMap().Put((MeasureDouble)_reqMeasure, value).Record(tagContext);
                else
                    Console.WriteLine(String.Format("Error Metrics#003: [Exporter/Views:UpdateView()] Unsupported Measure Type: {0}", Measure.Type));
            }
            
        }

        public IView toRegister()
        {
            return _requestView;
        }

        private IAggregation aggSelect()
        {
            switch (Aggregation)
            {
                case nameof(Count):
                    return Count.Create();
                case nameof(Distribution):
                    if (Bucket != null)
                        return Distribution.Create(BucketBoundaries.Create(new List<double>(Bucket)));
                    else
                        throw new ApplicationException("Error Metrics#004: [Exporter/Views:UpdateView()] Bucket parameter missing in appsettings.json");
                case nameof(LastValue):
                    return LastValue.Create();
                case nameof(Mean):
                    return Mean.Create();
                case nameof(Sum):
                    return Sum.Create();
                default:
                    throw new ApplicationException(String.Format("Error Metrics#005: [Exporter/Views:UpdateView()] Unsupported Aggregation Type: {0}", Aggregation));
            }
        }
    }
}

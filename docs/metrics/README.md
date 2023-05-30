# OpenTelemetry .NET Metrics

## Best Practices

- When emitting metrics with dimensions, ALWAYS provide the dimensions in an
  ascending sorted order (order by the dimension key) for best performance.

```csharp
// Sorted in an ascending order by dimension key: `color` comes before `name`
MyFruitCounter.Add(1, new("color", "red"), new("name", "apple")); // <--- DO THIS

MyFruitCounter.Add(1, new("name", "apple"), new("color", "red")); // <--- DON'T DO THIS
```

- When emitting metrics with more than three dimensions, use `TagList` for best
  performance.

```csharp
var tags = new TagList
{
    { "DimName1", "DimValue1" },
    { "DimName2", "DimValue2" },
    { "DimName3", "DimValue3" },
    { "DimName4", "DimValue4" },
};

// Uses a TagList as there are more than three dimensions
counter.Add(100, tags); // <--- DO THIS


// Avoid the below mentioned approaches when there are more than three dimensions
var tag1 = new KeyValuePair<string, object>("DimName1", "DimValue1");
var tag2 = new KeyValuePair<string, object>("DimName2", "DimValue2");
var tag3 = new KeyValuePair<string, object>("DimName3", "DimValue3");
var tag4 = new KeyValuePair<string, object>("DimName4", "DimValue4");

counter.Add(100, tag1, tag2, tag3, tag4); // <--- DON'T DO THIS

var readOnlySpanOfTags = new KeyValuePair<string, object>[4] { tag1, tag2, tag3, tag4};
counter.Add(100, readOnlySpanOfTags); // <--- DON'T DO THIS
```

## Common Issues that lead to missing metrics

- The `Meter` used to create the instruments is not added to the
  `MeterProvider`. Use `AddMeter` method to enable the processing for the
  required metrics.
- Instrument name is invalid. When naming instruments, ensure that the name you
  choose meets the criteria defined in the
  [spec](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/api.md#instrument-name-syntax).
- MetricPoint limit is reached. By default, the SDK limits the number of maximum
  MetricPoints (unique combination of keys and values for a given Metric stream)
  to 2000. This limit can be configured using
  `SetMaxMetricPointsPerMetricStream` method. Refer to this
  [doc](../../docs/metrics/customizing-the-sdk/README.md#changing-maximum-metricpoints-per-metricstream)
  for more information. The SDK would not process any newer unique key-value
  combination that it encounters, once this limit is reached.
- MeterProvider is disposed. You need to ensure that the `MeterProvider`
  instance is kept active for metrics to be collected. In a typical application,
  a single MeterProvider is built at application startup, and is disposed of at
  application shutdown.

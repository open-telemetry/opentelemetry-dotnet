# Building your own Sampler

* Samplers should inherit from `Sampler`, and implement `ShouldSample`
  method.
* `ShouldSample` should not block or take long time, since it will be called on
  critical code path.

```csharp
class MySampler : Sampler
{
    public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
    {
        var shouldSample = true;

        return new SamplingResult(shouldSample);
    }
}
```

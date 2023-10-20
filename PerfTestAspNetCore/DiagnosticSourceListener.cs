namespace PerfTestAspNetCore;

public class DiagnosticSourceListener : IObserver<KeyValuePair<string, object?>>
{
    public void OnCompleted()
    {
    }

    public void OnError(Exception error)
    {
    }

    public void OnNext(KeyValuePair<string, object?> value)
    {
        // do nothing
        // Measure how much perf is impacted by simply subscribing to diagnostic source.
    }
}
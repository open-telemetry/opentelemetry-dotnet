# EnrichingActivityProcessor

Sometimes the built-in instrumentation doesn't add enough data or you want to
augment the data it provides with contextual information your application or
library has available. For these cases OpenTelemetry .NET provides an
`EnrichingActivityProcessor` and `EnrichmentScope` API.

1. To enrich your `Activity` objects first add the `EnrichingActivityProcessor`
   to your `TracerProvider`:

    ```csharp
    using var tracerProvider = Sdk.CreateTracerProviderBuilder()
        .AddHttpClientInstrumentation()
        .AddProcessor(new EnrichingActivityProcessor())
        .AddConsoleExporter()
        .Build();
    ```

    **Note:** Order is important. Make sure your `EnrichingActivityProcessor` is
    registered before your `Exporter` and any other `ActivityProcessor`s you are
    using.

2. Use `EnrichmentScope.Begin` to wrap the call you want to instrument:

    ```csharp
    using (EnrichmentScope.Begin(a =>
    {
        a.AddTag("mycompany.user_id", 1234);
        a.AddTag("mycompany.customer_id", 5678);

        HttpRequestMessage request = (HttpRequestMessage)a.GetCustomProperty("HttpHandler.Request");
        if (request != null)
        {
            a.AddTag("http.user_agent", request.Headers.UserAgent.ToString());
        }

        HttpResponseMessage response = (HttpResponseMessage)a.GetCustomProperty("HttpHandler.Response");
        if (response != null)
        {
            a.AddTag("http.content_type", response.Content.Headers.ContentType.ToString());
        }
    }))
    {
        using var request = new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri("https://www.opentelemetry.io/"),
        };

        request.Headers.UserAgent.TryParseAdd("mycompany/mylibrary");

        using var response = await HttpClient.SendAsync(request).ConfigureAwait(false);
    }
    ```

    In that example the `Activity` created for the call to
    `www.opentelemetry.io` will be decorated with `mycompany.user_id` &
    `mycompany.customer_id` tags (simulating contextual data) and
    `http.user_agent` & `http.content_type` tags which are taken directly from
    the raw HTTP objects.

## Advanced Usage

### EnrichmentScopeTarget

`EnrichmentScope.Begin` supports a "target" argument which can be used to alter
the enrichment behavior via the `EnrichmentScopeTarget` enumeration:

```csharp
using EnrichmentScope.Begin(
    target: EnrichmentScopeTarget.FirstChild,
    enrichmentAction: a => a.AddTag("mycompany.user_id", 1234));

using EnrichmentScope.Begin(
    target: EnrichmentScopeTarget.AllChildren,
    enrichmentAction: a => a.AddTag("mycompany.user_id", 1234));
```

The default behavior is `EnrichmentScopeTarget.FirstChild`.

| Name | Description |
| ---- | ----------- |
| FirstChild  | The first child `Activity` created under the scope will be enriched and then the scope will automatically be closed. |
| AllChildren | All child `Activity` objects created under the scope will be enriched until the scope is closed. |

### Nesting

Enrichment scopes may be nested.

```csharp
using EnrichmentScope.Begin(
    target: EnrichmentScopeTarget.AllChildren,
    enrichmentAction: a => a.AddTag("mycompany.customer_id", 5678))
{
    using EnrichmentScope.Begin(
        target: EnrichmentScopeTarget.FirstChild,
        enrichmentAction: a => a.AddTag("mycompany.user_id", 1234))
    {
        using var response1 = await HttpClient.GetAsync("https://www.opentelemetry.io/").ConfigureAwait(false);
    }

    using EnrichmentScope.Begin(
        target: EnrichmentScopeTarget.FirstChild,
        enrichmentAction: a => a.AddTag("mycompany.user_id", 1818))
    {
        using var response2 = await HttpClient.GetAsync("https://www.cncf.io/").ConfigureAwait(false);
    }
}
```

In that example the request to `www.opentelemetry.io` will be enriched with the
`mycompany.user_id=1234` and `mycompany.customer_id=5678` tags (in that order)
and the request to `www.cncf.com` will be enriched with the
`mycompany.user_id=1818` and `mycompany.customer_id=5678` tags (in that order).

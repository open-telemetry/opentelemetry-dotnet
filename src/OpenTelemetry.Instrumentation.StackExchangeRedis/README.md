# Using StackExchange.Redis instrumentation

Outgoing calls to Redis made using `StackExchange.Redis` library can be automatically tracked.

1. Install package to your project:	
   [OpenTelemetry.Instrumentation.StackExchangeRedis](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.StackExchangeRedis)

2. Configure Redis instrumentation	

    ```csharp	
    // connect to the server	
    var connection = ConnectionMultiplexer.Connect("localhost:6379");	
    
    using (TracerFactory.Create(b => b	
                .SetSampler(new AlwaysSampleSampler())	
                .UseZipkin(options => {})	
                .SetResource(Resources.CreateServiceResource("my-service"))	
                .AddInstrumentation(t =>	
                {	
                    var instrumentation = new StackExchangeRedisCallsInstrumentation(t);	
                    connection.RegisterProfiler(instrumentation.GetProfilerSessionsFactory());	
                    return instrumentation;	
                })))	
    {	
    }	
    ```
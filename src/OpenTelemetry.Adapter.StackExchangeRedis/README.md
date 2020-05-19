# Using StackExchange.Redis adapter

Outgoing calls to Redis made using `StackExchange.Redis` library can be automatically tracked.

1. Install package to your project:	
   [OpenTelemetry.Adapter.StackExchangeRedis](https://www.nuget.org/packages/OpenTelemetry.Adapter.StackExchangeRedis)

2. Configure Redis adapter	

    ```csharp	
    // connect to the server	
    var connection = ConnectionMultiplexer.Connect("localhost:6379");	
    
    using (TracerFactory.Create(b => b	
                .SetSampler(new AlwaysSampleSampler())	
                .UseZipkin(options => {})	
                .SetResource(Resources.CreateServiceResource("my-service"))	
                .AddAdapter(t =>	
                {	
                    var adapter = new StackExchangeRedisCallsAdapter(t);	
                    connection.RegisterProfiler(adapter.GetProfilerSessionsFactory());	
                    return adapter;	
                })))	
    {	
    }	
    ```
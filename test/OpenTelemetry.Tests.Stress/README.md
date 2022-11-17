# OpenTelemetry Stress Tests

* [Why would you need stress test](#why-would-you-need-stress-test)
* [Running the demo](#running-the-demo)
* [Writing your own stress test](#writing-your-own-stress-test)
* [Understanding the results](#understanding-the-results)

## Why would you need stress test

* It helps you to understand performance.
* You can keep it running for days and nights to verify stability.
* You can use it to generate lots of load to your backend system.
* You can use it with other stress tools (e.g. a memory limiter) to verify how
  your code reacts to certain resource constraints.

## Running the demo

Open a console, run the following command from the current folder:

```sh
dotnet run --framework net6.0 --configuration Release
```

Once the application started, you will see the performance number updates from
the console window title.

Use the `SPACE` key to toggle the console output, which is off by default.

Use the `ENTER` key to print the latest performance statistics.

Use the `ESC` key to exit the stress test.

```text
Running (concurrency = 1), press <Esc> to stop...
2021-09-28T18:47:17.6807622Z Loops: 17,549,732,467, Loops/Second: 738,682,519, CPU Cycles/Loop: 3
2021-09-28T18:47:17.8846348Z Loops: 17,699,532,304, Loops/Second: 731,866,438, CPU Cycles/Loop: 3
2021-09-28T18:47:18.0914577Z Loops: 17,850,498,225, Loops/Second: 730,931,752, CPU Cycles/Loop: 3
2021-09-28T18:47:18.2992864Z Loops: 18,000,133,808, Loops/Second: 724,029,883, CPU Cycles/Loop: 3
2021-09-28T18:47:18.5052989Z Loops: 18,150,598,194, Loops/Second: 733,026,161, CPU Cycles/Loop: 3
2021-09-28T18:47:18.7116733Z Loops: 18,299,461,007, Loops/Second: 724,950,210, CPU Cycles/Loop: 3
```

The stress test metrics are exposed via
[Prometheus HttpListener](../../src/OpenTelemetry.Exporter.Prometheus.HttpListener/README.md),
which can be accessed via
[http://localhost:9464/metrics/](http://localhost:9464/metrics/).

Following shows a section of the metrics exposed in prometheus format:

```text
# HELP OpenTelemetry_Tests_Stress_Loops The total number of `Run()` invocations that are completed.
# TYPE OpenTelemetry_Tests_Stress_Loops counter
OpenTelemetry_Tests_Stress_Loops 1844902947 1658950184752

# HELP OpenTelemetry_Tests_Stress_LoopsPerSecond The rate of `Run()` invocations based on a small sliding window of few hundreds of milliseconds.
# TYPE OpenTelemetry_Tests_Stress_LoopsPerSecond gauge
OpenTelemetry_Tests_Stress_LoopsPerSecond 9007731.132075472 1658950184752

# HELP OpenTelemetry_Tests_Stress_CpuCyclesPerLoop The average CPU cycles for each `Run()` invocation, based on a small sliding window of few hundreds of milliseconds.
# TYPE OpenTelemetry_Tests_Stress_CpuCyclesPerLoop gauge
OpenTelemetry_Tests_Stress_CpuCyclesPerLoop 3008 1658950184752

# HELP process_runtime_dotnet_gc_collections_count Number of garbage collections that have occurred since process start.
# TYPE process_runtime_dotnet_gc_collections_count counter
process_runtime_dotnet_gc_collections_count{generation="gen2"} 0 1658950184752
process_runtime_dotnet_gc_collections_count{generation="gen1"} 0 1658950184752
process_runtime_dotnet_gc_collections_count{generation="gen0"} 0 1658950184752

# HELP process_runtime_dotnet_gc_allocations_size_bytes Count of bytes allocated on the managed GC heap since the process start. .NET objects are allocated from this heap. Object allocations from unmanaged languages such as C/C++ do not use this heap.
# TYPE process_runtime_dotnet_gc_allocations_size_bytes counter
process_runtime_dotnet_gc_allocations_size_bytes 5485192 1658950184752
```

## Writing your own stress test

Create a simple console application with the following code:

```csharp
using System.Runtime.CompilerServices;

public partial class Program
{
    public static void Main()
    {
        Stress(concurrency: 10, prometheusPort: 9464);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static void Run()
    {
        // add your logic here
    }
}
```

Add the [`Skeleton.cs`](./Skeleton.cs) file to your `*.csproj` file:

```xml
  <ItemGroup>
    <Compile Include="Skeleton.cs" />
  </ItemGroup>
```

Add the following packages to the project:

```shell
dotnet add package OpenTelemetry.Exporter.Prometheus --prerelease
dotnet add package OpenTelemetry.Instrumentation.Runtime --prerelease
```

Now you are ready to run your own stress test.

Some useful notes:

* You can specify the concurrency using `Stress(concurrency: {concurrency
  number})`, the default value is the number of CPU cores. Keep in mind that
  concurrency level does not equal to the number of threads.
* You can specify a local PrometheusExporter listening port using
  `Stress(prometheusPort: {port number})`, the default value is `0`, which will
  turn off the PrometheusExporter.
* You want to put `[MethodImpl(MethodImplOptions.AggressiveInlining)]` on
  `Run()`, this helps to reduce extra flushes on the CPU instruction cache.
* You might want to run the stress test under `Release` mode rather than `Debug`
  mode.

## Understanding the results

* `Loops` represent the total number of `Run()` invocations that are completed.
* `Loops/Second` represents the rate of `Run()` invocations based on a small
  sliding window of few hundreds of milliseconds.
* `CPU Cycles/Loop` represents the average CPU cycles for each `Run()`
  invocation, based on a small sliding window of few hundreds of milliseconds.
* `Runaway Time` represents the runaway time (seconds) since the test started.

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
[PrometheusExporter](../../src/OpenTelemetry.Exporter.Prometheus/README.md),
which can be accessed via
[http://localhost:9184/metrics/](http://localhost:9184/metrics/):

```text
# TYPE Process_NonpagedSystemMemorySize64 gauge
Process_NonpagedSystemMemorySize64 31651 1637385964580

# TYPE Process_PagedSystemMemorySize64 gauge
Process_PagedSystemMemorySize64 238672 1637385964580

# TYPE Process_PagedMemorySize64 gauge
Process_PagedMemorySize64 16187392 1637385964580

# TYPE Process_WorkingSet64 gauge
Process_WorkingSet64 29753344 1637385964580

# TYPE Process_VirtualMemorySize64 gauge
Process_VirtualMemorySize64 2204045848576 1637385964580
```

## Writing your own stress test

Create a simple console application with the following code:

```csharp
using System.Runtime.CompilerServices;

public partial class Program
{
    public static void Main()
    {
        Stress(concurrency: 10, prometheusPort: 9184);
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

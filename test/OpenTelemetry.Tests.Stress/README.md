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
dotnet run --framework net10.0 --configuration Release
```

To see command line options available, run the following command from the
current folder:

```sh
dotnet run --framework net10.0 --configuration Release -- --help
```

The help output includes settings and their explanations:

```text
  -c, --concurrency      The concurrency (maximum degree of parallelism) for the stress test. Default value: Environment.ProcessorCount.

  -p, --internal_port    The Prometheus http listener port where Prometheus will be exposed for retrieving internal metrics while the stress test is running. Set to '0' to
                         disable. Default value: 9464.

  -d, --duration         The duration for the stress test to run in seconds. If set to '0' or a negative value the stress test will run until canceled. Default value: 0.
```

Once the application started, you will see the performance number updates from
the console window title and the console window itself.

While a test is running...

* Use the `SPACE` key to toggle the console output, which is on by default.

* Use the `ENTER` key to print the latest performance statistics.

* Use the `ESC` key to exit the stress test.

Example output while a test is running:

```text
Options: {"Concurrency":20,"PrometheusInternalMetricsPort":9464,"DurationSeconds":0}
Run OpenTelemetry.Tests.Stress.exe --help to see available options.
Running (concurrency = 20, internalPrometheusEndpoint = http://localhost:9464/metrics/), press <Esc> to stop, press <Spacebar> to toggle statistics in the console...
Loops: 17,384,826,748, Loops/Second: 2,375,222,037, CPU Cycles/Loop: 24, RunningTime (Seconds): 7
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
using OpenTelemetry.Tests.Stress;

public static class Program
{
    public static int Main(string[] args)
    {
        return StressTestFactory.RunSynchronously<MyStressTest>(args);
    }

    private sealed class MyStressTest : StressTest<StressTestOptions>
    {
        public MyStressTest(StressTestOptions options)
            : base(options)
        {
        }

        protected override void RunWorkItemInParallel()
        {
        }
    }
}
```

Add the following project reference to the project:

```xml
<ProjectReference Include="$(RepoRoot)\test\OpenTelemetry.Tests.Stress\OpenTelemetry.Tests.Stress.csproj" />
```

Now you are ready to run your own stress test. Add test logic in the
`RunWorkItemInParallel` method to measure performance.

To define custom options create an options class which derives from
`StressTestOptions`:

```csharp
using CommandLine;
using OpenTelemetry.Tests.Stress;

public static class Program
{
    public static int Main(string[] args)
    {
        return StressTestFactory.RunSynchronously<MyStressTest, MyStressTestOptions>(args);
    }

    private sealed class MyStressTest : StressTest<MyStressTestOptions>
    {
        public MyStressTest(MyStressTestOptions options)
            : base(options)
        {
        }

        protected override void RunWorkItemInParallel()
        {
            // Use this.Options here to access options supplied
            // on the command line.
        }
    }

    private sealed class MyStressTestOptions : StressTestOptions
    {
        [Option('r', "rate", HelpText = "Add help text here for the rate option. Default value: 0.", Required = false)]
        public int Rate { get; set; } = 0;
    }
}
```

Some useful notes:

* It is generally best practice to run the stress test for code compiled in
  `Release` configuration rather than `Debug` configuration. `Debug` builds
  typically are not optimized and contain extra code which will change the
  performance characteristics of the logic under test. The stress test will
  write a warning message to the console when starting if compiled with `Debug`
  configuration.
* You can specify the concurrency using `-c` or `--concurrency` command line
  argument, the default value if not specified is the number of CPU cores. Keep
  in mind that concurrency level does not equal to the number of threads.
* You can use the duration `-d` or `--duration` command line argument to run the
  stress test for a specific time period. This is useful when comparing changes
  across multiple runs.

## Understanding the results

* `Loops` represent the total number of `Run()` invocations that are completed.
* `Loops/Second` represents the rate of `Run()` invocations based on a small
  sliding window of few hundreds of milliseconds.
* `CPU Cycles/Loop` represents the average CPU cycles for each `Run()`
  invocation, based on a small sliding window of few hundreds of milliseconds.
* `Total Running Time` represents the running time (seconds) since the test started.
* `GC Total Allocated Bytes` (not available on .NET Framework) shows the total
  amount of memory allocated while the test was running.

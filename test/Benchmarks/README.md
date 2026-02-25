# OpenTelemetry Benchmarks

Navigate to `./test/Benchmarks` directory and run the following command:

```sh
dotnet run -c Release -f net10.0 -- -m
```

[How to use console arguments](https://benchmarkdotnet.org/articles/guides/console-args.html)

- `-m` enables MemoryDiagnoser and prints memory statistics
- `-f` allows you to filter the benchmarks by their full name using glob patterns
  - Run all benchmarks in a class: `dotnet run -c Release -f net10.0 -- -f '*LogBenchmarks*'`
  - Run a single benchmark: `dotnet run -c Release -f net10.0 -- -f '*LogBenchmarks.OneProcessor'`

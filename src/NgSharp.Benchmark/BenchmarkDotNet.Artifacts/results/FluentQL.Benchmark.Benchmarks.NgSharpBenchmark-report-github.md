```

BenchmarkDotNet v0.15.2, macOS Sequoia 15.4.1 (24E263) [Darwin 24.4.0]
Apple M1 Max 2.40GHz, 1 CPU, 10 logical and 10 physical cores
.NET SDK 9.0.301
  [Host]     : .NET 9.0.6 (9.0.625.26613), X64 RyuJIT SSE4.2
  Job-XPUURG : .NET 9.0.6 (9.0.625.26613), X64 RyuJIT SSE4.2

IterationCount=10  WarmupCount=5  

```
| Method     | Mean      | Error      | StdDev    | Gen0   | Gen1   | Allocated |
|----------- |----------:|-----------:|----------:|-------:|-------:|----------:|
| NgSharp    |  4.372 μs |  0.4706 μs | 0.3113 μs | 0.1250 | 0.0469 |     840 B |
| RazorLight | 90.327 μs | 13.2068 μs | 8.7355 μs | 0.6000 | 0.2000 |    4490 B |

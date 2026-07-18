```

BenchmarkDotNet v0.15.2, macOS 26.2 (25C56) [Darwin 25.2.0]
Apple M1 Max, 1 CPU, 10 logical and 10 physical cores
.NET SDK 10.0.301
  [Host]     : .NET 9.0.6 (9.0.625.26613), Arm64 RyuJIT AdvSIMD
  Job-XPUURG : .NET 9.0.6 (9.0.625.26613), Arm64 RyuJIT AdvSIMD

IterationCount=10  WarmupCount=5  

```
| Method                | Categories | Mean          | Error       | StdDev      | Ratio    | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio |
|---------------------- |----------- |--------------:|------------:|------------:|---------:|--------:|---------:|--------:|----------:|------------:|
| NgSharp_Cold          | Cold       |     11.393 μs |   1.7639 μs |   1.1667 μs |     1.01 |    0.14 |   2.5024 |  0.0610 |   15877 B |        1.00 |
| RazorLight_Cold       | Cold       | 20,906.336 μs | 602.9349 μs | 358.7969 μs | 1,852.64 |  185.05 | 406.2500 | 62.5000 | 2647681 B |      166.76 |
| Handlebars_Cold       | Cold       |  1,848.109 μs |  71.4030 μs |  47.2286 μs |   163.77 |   16.62 |  23.4375 | 11.7188 |  151531 B |        9.54 |
| Scriban_Cold          | Cold       |     19.790 μs |   0.8316 μs |   0.5501 μs |     1.75 |    0.18 |   7.3242 |  0.6104 |   46325 B |        2.92 |
| Stubble_Cold          | Cold       |      2.499 μs |   0.0651 μs |   0.0430 μs |     0.22 |    0.02 |   0.9918 |  0.0114 |    6224 B |        0.39 |
| Fluid_Cold            | Cold       |      4.605 μs |   0.1100 μs |   0.0727 μs |     0.41 |    0.04 |   0.7782 |  0.0076 |    4912 B |        0.31 |
|                       |            |               |             |             |          |         |          |         |           |             |
| NgSharp_Warm          | Warm       |      5.386 μs |   0.0913 μs |   0.0604 μs |     1.00 |    0.02 |   0.6866 |       - |    4313 B |        1.00 |
| NgSharp_Warm_Prebuilt | Warm       |      2.536 μs |   0.0601 μs |   0.0398 μs |     0.47 |    0.01 |   0.2823 |       - |    1792 B |        0.42 |
| RazorLight_Warm       | Warm       |      1.525 μs |   0.0263 μs |   0.0174 μs |     0.28 |    0.00 |   0.4482 |  0.0019 |    2816 B |        0.65 |
| Handlebars_Warm       | Warm       |      1.009 μs |   0.0215 μs |   0.0142 μs |     0.19 |    0.00 |   0.0610 |       - |     392 B |        0.09 |
| Scriban_Warm          | Warm       |     12.034 μs |   0.4156 μs |   0.2749 μs |     2.23 |    0.05 |   5.6152 |  0.3967 |   35289 B |        8.18 |
| Fluid_Warm            | Warm       |      1.283 μs |   0.0214 μs |   0.0141 μs |     0.24 |    0.00 |   0.2480 |       - |    1560 B |        0.36 |

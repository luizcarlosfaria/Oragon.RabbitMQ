```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.7623)
AMD Ryzen 9 9950X3D, 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 9.0.12 (9.0.1225.60609), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  Job-IGSTJO : .NET 9.0.12 (9.0.1225.60609), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Server=True  

```
| Method                                       | MessageSize | Mean        | Error       | StdDev       | Median      | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------------------------------- |------------ |------------:|------------:|-------------:|------------:|------:|--------:|-------:|----------:|------------:|
| **&#39;Oragon (ToArray &gt; GetString &gt; Deserialize)&#39;** | **Large**       | **30,889.2 ns** | **4,143.79 ns** | **12,218.05 ns** | **24,454.5 ns** |  **1.31** |    **0.56** | **0.4883** |   **39764 B** |        **2.36** |
| &#39;Native (Deserialize from Span)&#39;             | Large       | 24,057.8 ns | 1,158.19 ns |  3,378.49 ns | 24,439.9 ns |  1.02 |    0.21 | 0.2136 |   16848 B |        1.00 |
| &#39;Native (Utf8JsonReader)&#39;                    | Large       | 31,132.1 ns |   707.17 ns |  1,994.60 ns | 30,764.4 ns |  1.32 |    0.21 | 0.2136 |   16848 B |        1.00 |
|                                              |             |             |             |              |             |       |         |        |           |             |
| **&#39;Oragon (ToArray &gt; GetString &gt; Deserialize)&#39;** | **Medium**      |  **1,646.2 ns** |   **124.70 ns** |    **355.79 ns** |  **1,766.9 ns** |  **1.50** |    **0.39** | **0.0725** |    **5696 B** |        **2.08** |
| &#39;Native (Deserialize from Span)&#39;             | Medium      |  1,118.2 ns |    50.05 ns |    146.01 ns |  1,094.8 ns |  1.02 |    0.20 | 0.0353 |    2736 B |        1.00 |
| &#39;Native (Utf8JsonReader)&#39;                    | Medium      |  1,437.6 ns |    70.03 ns |    205.38 ns |  1,420.0 ns |  1.31 |    0.27 | 0.0353 |    2736 B |        1.00 |
|                                              |             |             |             |              |             |       |         |        |           |             |
| **&#39;Oragon (ToArray &gt; GetString &gt; Deserialize)&#39;** | **Small**       |    **306.9 ns** |    **26.19 ns** |     **77.22 ns** |    **323.6 ns** |  **1.50** |    **0.47** | **0.0033** |     **264 B** |        **2.75** |
| &#39;Native (Deserialize from Span)&#39;             | Small       |    210.6 ns |    10.54 ns |     31.08 ns |    223.0 ns |  1.03 |    0.24 | 0.0012 |      96 B |        1.00 |
| &#39;Native (Utf8JsonReader)&#39;                    | Small       |    302.1 ns |    20.80 ns |     61.34 ns |    294.3 ns |  1.47 |    0.40 | 0.0010 |      96 B |        1.00 |

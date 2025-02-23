``` ini

BenchmarkDotNet=v0.13.1, OS=Windows 10.0.19043.1237 (21H1/May2021Update)
Intel Core i9-9980HK CPU 2.40GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK=6.0.100-rc.1.21458.32
  [Host]     : .NET 6.0.0 (6.0.21.45113), X64 RyuJIT
  DefaultJob : .NET 6.0.0 (6.0.21.45113), X64 RyuJIT


```
|            Method |     Mean |    Error |   StdDev | Ratio |
|------------------ |---------:|---------:|---------:|------:|
| TestTokenCreation | 908.2 μs | 26.35 μs | 76.45 μs |  1.00 |

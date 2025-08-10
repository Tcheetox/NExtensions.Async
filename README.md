# NExtensions.Async

[![NuGet Version](https://img.shields.io/nuget/v/NExtensions.Async)](https://www.nuget.org/packages/NExtensions.Async/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://licenses.nuget.org/MIT)
[![.NET](https://img.shields.io/badge/.NET-6.0%20%7C%207.0%20%7C%208.0%20%7C%209.0-blue)](https://dotnet.microsoft.com/)

High-performance async synchronization primitives for modern .NET applications. This library provides efficient, allocation-friendly implementations of essential async coordination
types: `AsyncReaderWriterLock`, `AsyncLock`, and `AsyncLazy`.
Inspired from the awesome [library](https://www.nuget.org/packages/Nito.AsyncEx) of Stephen Cleary.

## ✨ Features

- **🔒 AsyncLock**: Asynchronous mutual-exclusion lock
- **⚡ AsyncReaderWriterLock**: Multiple concurrent readers or single exclusive writer
- **⏳ AsyncLazy<T>**: Thread-safe asynchronous lazy initialization with multiple safety modes
- **Cancellation Support**: Thorough `CancellationToken` support across all primitives
- **Zero dependencies**: No external dependencies
- **Modern .NET**: Supports .NET 6.0, 7.0, and 8.0 with nullable reference types

## Installation

```bash
dotnet add package NExtensions.Async
```

## 🚀 Quick Start

### AsyncLock

An asynchronous mutual-exclusion lock that allows only one thread to enter the critical section at a time.

```csharp
var asyncLock = new AsyncLock();

// Basic usage
using (await asyncLock.EnterScopeAsync())
{
    // Only one thread at a time
    await ProcessSharedResourceAsync();
}

// With cancellation
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
try
{
    using (await asyncLock.EnterScopeAsync(cts.Token))
    {
        await LongRunningOperationAsync();
    }
}
catch (OperationCanceledException)
{
    // Handle timeout
}

// Allow synchronous continuations for better performance (use with care)
var fastLock = new AsyncLock(allowSynchronousContinuations: true);
```

### AsyncReaderWriterLock

A reader-writer lock allowing multiple concurrent readers or a single exclusive writer.

```csharp
var rwLock = new AsyncReaderWriterLock();

// Multiple readers can access simultaneously
using (await rwLock.EnterReaderScopeAsync())
{
    var data = await ReadOnlyOperationAsync();
    // Other readers can run concurrently
}

// Writers get exclusive access
using (await rwLock.EnterWriterScopeAsync())
{
    await ModifyDataAsync();
    // No other readers or writers allowed
}

// Configure synchronous continuations independently
var customRwLock = new AsyncReaderWriterLock(
    allowSynchronousReaderContinuations: true,
    allowSynchronousWriterContinuations: false
);
```

### AsyncLazy<T>

Thread-safe asynchronous lazy initialization with configurable safety modes.

```csharp
// Basic lazy initialization
var lazy = new AsyncLazy<DatabaseConnection>(
    async () => await ConnectToDatabaseAsync()
);

DatabaseConnection connection = await lazy;
DatabaseConnection sameConnection = await lazy; // Same instance, no re-initialization

// Consume with cancellation support
var lazyWithCancellation = new AsyncLazy<string>(
    async (ct) => await DownloadDataAsync(ct)
);
string data = await lazyWithCancellation.GetAsync(cancellationToken);

// Configure safety modes
var retryableLazy = new AsyncLazy<string>(
    async () => await UnreliableOperationAsync(),
    LazyAsyncSafetyMode.ExecutionAndPublicationWithRetry
);
```

#### AsyncLazy Safety Modes

```csharp
// No thread safety, no retry (fastest)
var noneMode = new AsyncLazy<string>(factory, LazyAsyncSafetyMode.None);

// No thread safety, but retry on failure
var noneWithRetry = new AsyncLazy<string>(factory, LazyAsyncSafetyMode.NoneWithRetry);

// Thread-safe publication, retry on failure 
var publicationOnly = new AsyncLazy<string>(factory, LazyAsyncSafetyMode.PublicationOnly);

// Thread-safe execution and publication, cache exceptions (default-like behavior)
var executionAndPublication = new AsyncLazy<string>(factory, LazyAsyncSafetyMode.ExecutionAndPublication);

// Thread-safe execution and publication with retry (safest)
var safestMode = new AsyncLazy<string>(factory, LazyAsyncSafetyMode.ExecutionAndPublicationWithRetry);
```

### 🔧 Synchronous Continuations

Both `AsyncReaderWriterLock`, `AsyncLock` support synchronous continuations for improved performance. This feature is disabled by default, as asynchronous continuations are
considered the safer default. Enable it only with caution and after prior benchmarking.

```csharp
// Can be faster, but be careful of reentrancy and stack diving
var fastLock = new AsyncLock(allowSynchronousContinuations: true);

// Fine-tune reader vs writer continuation behavior
var customRwLock = new AsyncReaderWriterLock(
    allowSynchronousReaderContinuations: true,  // Safe for read-only operations
    allowSynchronousWriterContinuations: false  // Writers might need async context
);
```

## 🎯 Notes

* **Always dispose releasers**: Use `using` statements to ensure locks are released
* **Handle cancellation**: Provide appropriate `CancellationToken` values for timeout scenarios
* **Choose safety modes wisely**: Balance performance vs. safety based on your requirements
* **Be cautious with sync continuations**: Only enable when you understand the implications
* **Debugging**: All types include comprehensive debugging support

## 📊 Benchmarks

```
BenchmarkDotNet v0.15.1, Windows 11 (10.0.26100.4770/24H2/2024Update/HudsonValley)
13th Gen Intel Core i9-13900KF 3.00GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 8.0.411
  [Host]     : .NET 8.0.18 (8.0.1825.31117), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.18 (8.0.1825.31117), X64 RyuJIT AVX2
```

### AsyncLock

| Method            | Hits       | Parallelism | Wait      |            Mean |         Error |        StdDev | Completed Work Items | Lock Contentions |           Gen0 |          Gen1 |      Allocated |
|-------------------|------------|-------------|-----------|----------------:|--------------:|--------------:|---------------------:|-----------------:|---------------:|--------------:|---------------:|
| **SemaphoreSlim** | **150000** | **1**       | **yield** |    **57.31 ms** |  **0.256 ms** |  **0.239 ms** |      **151662.2222** |            **-** |   **888.8889** |         **-** |   **16.02 MB** |
| AsyncExLock       | 150000     | 1           | yield     |        61.42 ms |      0.445 ms |      0.416 ms |          150544.0000 |                - |      3444.4444 |             - |        61.8 MB |
| AsyncLock         | 150000     | 1           | yield     |        57.15 ms |      0.163 ms |      0.152 ms |          151519.3333 |                - |       888.8889 |             - |       16.02 MB |
| **SemaphoreSlim** | **150000** | **20**      | **yield** | **3,502.90 ms** | **49.994 ms** | **41.747 ms** |     **6041664.0000** |      **48.0000** | **77000.0000** | **1000.0000** | **1373.29 MB** |
| AsyncExLock       | 150000     | 20          | yield     |     3,173.30 ms |     35.980 ms |     31.896 ms |         9101322.0000 |          81.0000 |    135000.0000 |     7000.0000 |     2426.15 MB |
| AsyncLock         | 150000     | 20          | yield     |     2,507.91 ms |     34.059 ms |     31.859 ms |         6032875.0000 |           6.0000 |     23000.0000 |             - |         412 MB |

### AsyncReaderWriterLock

| Method                      | RW        | Hits       | Wait      | Continuation       |         Mean |       Error |       StdDev |       Median |           Gen0 | Completed Work Items | Lock Contentions |     Allocated |
|-----------------------------|-----------|------------|-----------|--------------------|-------------:|------------:|-------------:|-------------:|---------------:|---------------------:|-----------------:|--------------:|
| **AsyncExReaderWriterLock** | **1/10**  | **150000** | **yield** | **AsyncReadWrite** | **245.6 ms** | **4.87 ms** |  **4.78 ms** | **244.5 ms** | **11000.0000** |      **604780.0000** |       **9.0000** | **208.29 MB** |
| AsyncReaderWriterLock       | 1/10      | 150000     | yield     | AsyncReadWrite     |     197.7 ms |     3.83 ms |      4.98 ms |     196.8 ms |      2000.0000 |          453012.0000 |           1.0000 |      36.63 MB |
| **AsyncExReaderWriterLock** | **10/1**  | **150000** | **yield** | **AsyncReadWrite** | **204.8 ms** | **4.03 ms** |  **7.67 ms** | **205.4 ms** | **10000.0000** |      **517546.0000** |    **3434.0000** | **191.23 MB** |
| AsyncReaderWriterLock       | 10/1      | 150000     | yield     | AsyncReadWrite     |     173.2 ms |     3.46 ms |      9.47 ms |     172.6 ms |      2000.0000 |          453465.6667 |         387.3333 |      37.95 MB |
| **AsyncExReaderWriterLock** | **10/10** | **150000** | **yield** | **AsyncReadWrite** | **250.8 ms** | **5.01 ms** | **11.11 ms** | **252.5 ms** | **11000.0000** |      **606837.0000** |     **698.0000** |  **208.3 MB** |
| AsyncReaderWriterLock       | 10/10     | 150000     | yield     | AsyncReadWrite     |     179.2 ms |     3.54 ms |      4.73 ms |     179.2 ms |      2000.0000 |          454672.0000 |         171.0000 |      36.63 MB |
| **AsyncExReaderWriterLock** | **10/5**  | **150000** | **yield** | **AsyncReadWrite** | **250.5 ms** | **4.99 ms** |  **8.47 ms** | **246.6 ms** | **11000.0000** |      **605912.0000** |     **171.0000** | **208.29 MB** |
| AsyncReaderWriterLock       | 10/5      | 150000     | yield     | AsyncReadWrite     |     179.9 ms |     3.49 ms |      3.88 ms |     180.3 ms |      2000.0000 |          454856.3333 |         187.6667 |      36.63 MB |
| **AsyncExReaderWriterLock** | **5/10**  | **150000** | **yield** | **AsyncReadWrite** | **238.6 ms** | **4.30 ms** |  **4.22 ms** | **237.7 ms** | **11000.0000** |      **607103.0000** |      **77.0000** | **208.29 MB** |
| AsyncReaderWriterLock       | 5/10      | 150000     | yield     | AsyncReadWrite     |     168.2 ms |     3.26 ms |      3.62 ms |     168.5 ms |      2000.0000 |          455618.6667 |          12.6667 |      36.63 MB |

### AsyncLazy

| Method                              | Parallelism |          Mean |         Error |        StdDev |    Ratio |  RatioSD |       Gen0 | Completed Work Items | Lock Contentions |  Allocated | Alloc Ratio |
|-------------------------------------|-------------|--------------:|--------------:|--------------:|---------:|---------:|-----------:|---------------------:|-----------------:|-----------:|------------:|
| **Lazy_ExecutionAndPublication**    | **1**       | **0.0007 ms** | **0.0000 ms** | **0.0000 ms** | **1.00** | **0.00** | **0.0200** |           **1.0189** |       **0.0000** |  **386 B** |    **1.00** |
| AsyncExLazy_ExecutionAndPublication | 1           |     0.0011 ms |     0.0000 ms |     0.0000 ms |     1.61 |     0.01 |     0.0362 |               2.0009 |           0.0000 |      696 B |        1.80 |
| AsyncLazy_ExecutionAndPublication   | 1           |     0.0008 ms |     0.0000 ms |     0.0000 ms |     1.14 |     0.01 |     0.0372 |               1.0274 |           0.0000 |      708 B |        1.83 |
|                                     |             |               |               |               |          |          |            |                      |                  |            |             |
| **Lazy_ExecutionAndPublication**    | **10**      | **0.0025 ms** | **0.0000 ms** | **0.0001 ms** | **1.00** | **0.03** | **0.0648** |           **6.7474** |       **0.0001** | **1254 B** |    **1.00** |
| AsyncExLazy_ExecutionAndPublication | 10          |     0.0035 ms |     0.0000 ms |     0.0000 ms |     1.40 |     0.03 |     0.1030 |              11.1279 |           0.0002 |     1968 B |        1.57 |
| AsyncLazy_ExecutionAndPublication   | 10          |     0.0025 ms |     0.0000 ms |     0.0001 ms |     1.01 |     0.03 |     0.0839 |               6.5738 |           0.0000 |     1594 B |        1.27 |
|                                     |             |               |               |               |          |          |            |                      |                  |            |             |
| **Lazy_ExecutionAndPublication**    | **200**     | **0.0160 ms** | **0.0001 ms** | **0.0001 ms** | **1.00** | **0.01** | **0.0610** |          **12.3357** |       **0.0050** | **1341 B** |    **1.00** |
| AsyncExLazy_ExecutionAndPublication | 200         |     0.0431 ms |     0.0009 ms |     0.0015 ms |     2.68 |     0.09 |     0.0610 |              18.9797 |           1.7889 |     2231 B |        1.66 |
| AsyncLazy_ExecutionAndPublication   | 200         |     0.0160 ms |     0.0001 ms |     0.0001 ms |     0.99 |     0.01 |     0.0916 |              12.8473 |           0.0040 |     1784 B |        1.33 |

## 📄 License

This project is licensed under the [MIT](https://licenses.nuget.org/MIT) License.
# NExtensions.Async

[![NuGet Version](https://img.shields.io/nuget/v/NExtensions.Async)](https://www.nuget.org/packages/NExtensions.Async/)
[![Tests](https://github.com/Tcheetox/NExtensions.Async/actions/workflows/test.yml/badge.svg)](https://github.com/Tcheetox/NExtensions.Async/actions/workflows/test.yml)
[![codecov](https://codecov.io/gh/Tcheetox/NExtensions.Async/graph/badge.svg?token=HTJAJIKELY)](https://codecov.io/gh/Tcheetox/NExtensions.Async)
[![.NET](https://img.shields.io/badge/.NET-6.0%20%7C%207.0%20%7C%208.0%20%7C%209.0%20%7C%2010.0-blue)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/Tcheetox/NExtensions.Async?tab=MIT-1-ov-file#readme)

High-performance async synchronization primitives for modern .NET applications. This library provides efficient, allocation-friendly implementations of essential async coordination
types: `AsyncReaderWriterLock`, `AsyncLock`, `AsyncLazy`, `AsyncAutoResetEvent`, and `AsyncManualResetEvent`.
Inspired from the awesome [library](https://www.nuget.org/packages/Nito.AsyncEx) of Stephen Cleary.

## ✨ Features

- **🔒 AsyncLock**: Asynchronous mutual-exclusion lock
- **⚡ AsyncReaderWriterLock**: Multiple concurrent readers or single exclusive writer
- **⏳ AsyncLazy<T>**: Thread-safe asynchronous lazy initialization with multiple safety modes
- **🚥 AsyncAutoResetEvent**: Signals a single waiting task and automatically resets
- **🚩 AsyncManualResetEvent**: Signals all waiting tasks and remains signaled until manually reset
- **Cancellation Support**: Thorough `CancellationToken` support across all primitives
- **Zero dependencies**: No external dependencies
- **Modern .NET**: Supports from .NET 6.0 to the latest .NET 10.0 with nullable reference types

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

### AsyncAutoResetEvent

An asynchronous event that, when signaled, releases a single waiting task and then automatically resets to a non-signaled state.

```csharp
// Initialize non-signaled
var autoEvent = new AsyncAutoResetEvent(initialState: false);

// Task 1: Wait for signal
await autoEvent.WaitAsync();

// Task 2: Signal the event
autoEvent.Set(); // Task 1 resumes, event is automatically reset
```

### AsyncManualResetEvent

An asynchronous event that, when signaled, remains signaled until manually reset, allowing all current and future waiting tasks to proceed.

```csharp
// Initialize non-signaled
var manualEvent = new AsyncManualResetEvent(initialState: false);

// Multiple tasks can wait
var task1 = manualEvent.WaitAsync();
var task2 = manualEvent.WaitAsync();

// Signal the event
manualEvent.Set(); // Both task1 and task2 resume

// Event remains signaled
await manualEvent.WaitAsync(); // Completes immediately

// Reset the event
manualEvent.Reset(); // Future WaitAsync() will block
```

### 🔧 Synchronous Continuations

`AsyncReaderWriterLock`, `AsyncLock`, `AsyncAutoResetEvent`, and `AsyncManualResetEvent` support synchronous continuations for improved performance. This feature is disabled by
default, as asynchronous continuations are
considered the safer default. Enable it only with caution and after prior benchmarking.

```csharp
// Can be faster, but be careful of reentrancy and stack diving
var fastLock = new AsyncLock(allowSynchronousContinuations: true);
var fastEvent = new AsyncAutoResetEvent(initialState: false, allowSynchronousContinuations: true);

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

### AsyncAutoResetEvent

| Method                | SW        | Hits       | Wait      |          Mean |        Error |       StdDev |        Median | Completed Work Items | Lock Contentions |      Gen0 |    Allocated |
|-----------------------|-----------|------------|-----------|--------------:|-------------:|-------------:|--------------:|---------------------:|-----------------:|----------:|-------------:|
| **AutoResetEvent**    | **1/1**   | **100000** | **yield** |  **62.17 ms** | **1.185 ms** | **1.317 ms** |  **61.86 ms** |      **100004.0000** |            **-** |     **-** | **10.68 MB** |
| AsyncExAutoResetEvent | 1/1       | 100000     | yield     |      65.63 ms |     1.302 ms |     3.315 ms |      65.66 ms |          100188.0000 |          41.0000 |         - |     10.69 MB |
| AsyncAutoResetEvent   | 1/1       | 100000     | yield     |      38.35 ms |     0.747 ms |     0.971 ms |      38.25 ms |          100379.0000 |                - |         - |     10.68 MB |
| **AutoResetEvent**    | **1/10**  | **100000** | **yield** | **117.69 ms** | **0.618 ms** | **0.579 ms** | **117.60 ms** |      **102225.0000** |            **-** |     **-** | **10.69 MB** |
| AsyncExAutoResetEvent | 1/10      | 100000     | yield     |      57.64 ms |     0.807 ms |     0.674 ms |      57.47 ms |          186625.0000 |        2364.0000 | 1000.0000 |     23.76 MB |
| AsyncAutoResetEvent   | 1/10      | 100000     | yield     |      36.43 ms |     1.139 ms |     3.360 ms |      37.93 ms |          141620.0000 |                - |         - |     12.39 MB |
| **AutoResetEvent**    | **3/100** | **100000** | **yield** |  **69.10 ms** | **0.769 ms** | **0.719 ms** |  **69.27 ms** |      **100102.0000** |            **-** |     **-** | **10.71 MB** |
| AsyncExAutoResetEvent | 3/100     | 100000     | yield     |      50.74 ms |     1.000 ms |     1.671 ms |      50.51 ms |          174227.0000 |         664.0000 | 1000.0000 |     21.99 MB |
| AsyncAutoResetEvent   | 3/100     | 100000     | yield     |      27.64 ms |     0.614 ms |     1.792 ms |      27.38 ms |          109116.0000 |                - |         - |     11.57 MB |

### AsyncManualResetEvent

| Method                  | Waiters | Hits       |           Mean |         Error |        StdDev | Completed Work Items | Lock Contentions |  Allocated |
|-------------------------|---------|------------|---------------:|--------------:|--------------:|---------------------:|-----------------:|-----------:|
| **ManualResetEvent**    | **1**   | **100000** | **17.8352 ms** | **0.2861 ms** | **0.2537 ms** |           **2.0000** |            **-** |  **728 B** |
| AsyncExManualResetEvent | 1       | 100000     |      1.6770 ms |     0.0403 ms |     0.1162 ms |               1.0000 |                - |      496 B |
| AsyncManualResetEvent   | 1       | 100000     |      0.8266 ms |     0.0105 ms |     0.0088 ms |               1.0000 |                - |      496 B |
| **ManualResetEvent**    | **10**  | **100000** | **12.5441 ms** | **0.5298 ms** | **1.5621 ms** |          **20.0000** |            **-** | **2816 B** |
| AsyncExManualResetEvent | 10      | 100000     |     10.1570 ms |     0.2022 ms |     0.2483 ms |              20.0000 |          97.0000 |     3144 B |
| AsyncManualResetEvent   | 10      | 100000     |      2.0304 ms |     0.0525 ms |     0.1514 ms |              20.0000 |                - |     4096 B |

## 📜 License

This project is licensed under the [MIT](https://github.com/Tcheetox/NExtensions.Async?tab=MIT-1-ov-file#readme) License.
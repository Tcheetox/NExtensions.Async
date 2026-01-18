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
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.7623/25H2/2025Update/HudsonValley2)
13th Gen Intel Core i9-13900KF 3.00GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
```

### AsyncLock

| Method        | Hits   | Parallelism | Wait  | Mean        | Error     | StdDev    | Gen0        | Completed Work Items | Lock Contentions | Gen1      | Allocated  |
|-------------- |------- |------------ |------ |------------:|----------:|----------:|------------:|---------------------:|-----------------:|----------:|-----------:|
| **SemaphoreSlim** | **150000** | **1**           | **yield** |    **55.91 ms** |  **0.231 ms** |  **0.216 ms** |    **888.8889** |          **151062.0000** |                **-** |         **-** |   **16.02 MB** |
| AsyncExLock   | 150000 | 1           | yield |    58.36 ms |  0.675 ms |  0.632 ms |   3444.4444 |          150555.2222 |                - |         - |    61.8 MB |
| AsyncLock     | 150000 | 1           | yield |    56.29 ms |  0.205 ms |  0.192 ms |    888.8889 |          151753.0000 |                - |         - |   16.02 MB |
| **SemaphoreSlim** | **150000** | **20**          | **yield** | **3,160.96 ms** | **46.225 ms** | **43.239 ms** |  **78000.0000** |         **6051243.0000** |          **29.0000** | **1000.0000** | **1396.19 MB** |
| AsyncExLock   | 150000 | 20          | yield | 2,985.29 ms | 41.962 ms | 37.198 ms | 135000.0000 |         9061226.0000 |          50.0000 | 7000.0000 | 2426.15 MB |
| AsyncLock     | 150000 | 20          | yield | 2,502.91 ms | 46.299 ms | 45.472 ms |  23000.0000 |         6040592.0000 |           3.0000 |         - |  411.99 MB |

### AsyncReaderWriterLock

| Method                  | RW    | Hits   | Wait  | Mean     | Error   | StdDev   | Completed Work Items | Lock Contentions | Gen0       | Allocated |
|------------------------ |------ |------- |------ |---------:|--------:|---------:|---------------------:|-----------------:|-----------:|----------:|
| **AsyncExReaderWriterLock** | **1/10**  | **150000** | **yield** | **234.1 ms** | **2.89 ms** |  **2.26 ms** |          **603723.0000** |           **5.0000** | **11000.0000** | **208.29 MB** |
| AsyncReaderWriterLock   | 1/10  | 150000 | yield | 177.9 ms | 3.21 ms |  2.68 ms |          453660.0000 |           1.5000 |  2000.0000 |  36.63 MB |
| **AsyncExReaderWriterLock** | **10/1**  | **150000** | **yield** | **194.3 ms** | **3.87 ms** | **10.20 ms** |          **480369.0000** |        **2233.0000** | **10000.0000** | **183.87 MB** |
| AsyncReaderWriterLock   | 10/1  | 150000 | yield | 173.0 ms | 5.08 ms | 14.99 ms |          452356.5000 |         283.5000 |  2000.0000 |  37.88 MB |
| **AsyncExReaderWriterLock** | **10/10** | **150000** | **yield** | **225.8 ms** | **1.80 ms** |  **1.59 ms** |          **607389.0000** |         **437.0000** | **11000.0000** | **208.29 MB** |
| AsyncReaderWriterLock   | 10/10 | 150000 | yield | 181.9 ms | 3.61 ms |  4.82 ms |          454691.5000 |         367.0000 |  2000.0000 |  36.63 MB |
| **AsyncExReaderWriterLock** | **10/5**  | **150000** | **yield** | **229.7 ms** | **2.64 ms** |  **2.20 ms** |          **605912.0000** |         **142.0000** | **11000.0000** | **208.29 MB** |
| AsyncReaderWriterLock   | 10/5  | 150000 | yield | 175.1 ms | 3.33 ms |  3.56 ms |          454742.0000 |         881.5000 |  2000.0000 |  36.63 MB |
| **AsyncExReaderWriterLock** | **5/10**  | **150000** | **yield** | **232.0 ms** | **4.48 ms** |  **4.80 ms** |          **606453.0000** |          **10.0000** | **11000.0000** | **208.29 MB** |
| AsyncReaderWriterLock   | 5/10  | 150000 | yield | 164.0 ms | 3.20 ms |  4.38 ms |          453987.5000 |          18.0000 |  2000.0000 |  36.63 MB |

### AsyncLazy

| Method                              | Parallelism | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0   | Completed Work Items | Lock Contentions | Allocated | Alloc Ratio |
|------------------------------------ |------------ |----------:|----------:|----------:|------:|--------:|-------:|---------------------:|-----------------:|----------:|------------:|
| **Lazy_ExecutionAndPublication**        | **1**           | **0.0007 ms** | **0.0000 ms** | **0.0000 ms** |  **1.00** |    **0.00** | **0.0200** |               **1.0234** |           **0.0000** |     **379 B** |        **1.00** |
| AsyncExLazy_ExecutionAndPublication | 1           | 0.0011 ms | 0.0000 ms | 0.0000 ms |  1.62 |    0.01 | 0.0362 |               2.0016 |           0.0000 |     696 B |        1.84 |
| AsyncLazy_ExecutionAndPublication   | 1           | 0.0007 ms | 0.0000 ms | 0.0000 ms |  1.08 |    0.01 | 0.0381 |               1.0315 |                - |     724 B |        1.91 |
|                                     |             |           |           |           |       |         |        |                      |                  |           |             |
| **Lazy_ExecutionAndPublication**        | **10**          | **0.0024 ms** | **0.0000 ms** | **0.0000 ms** |  **1.00** |    **0.01** | **0.0610** |               **6.4342** |           **0.0001** |    **1172 B** |        **1.00** |
| AsyncExLazy_ExecutionAndPublication | 10          | 0.0034 ms | 0.0000 ms | 0.0000 ms |  1.40 |    0.01 | 0.0992 |              10.6488 |           0.0001 |    1866 B |        1.59 |
| AsyncLazy_ExecutionAndPublication   | 10          | 0.0026 ms | 0.0000 ms | 0.0000 ms |  1.06 |    0.01 | 0.0839 |               6.6587 |           0.0000 |    1579 B |        1.35 |
|                                     |             |           |           |           |       |         |        |                      |                  |           |             |
| **Lazy_ExecutionAndPublication**        | **200**         | **0.0170 ms** | **0.0002 ms** | **0.0002 ms** |  **1.00** |    **0.02** | **0.0610** |              **12.4302** |           **0.0108** |    **1322 B** |        **1.00** |
| AsyncExLazy_ExecutionAndPublication | 200         | 0.0409 ms | 0.0005 ms | 0.0004 ms |  2.41 |    0.04 | 0.0610 |              16.4691 |           1.4722 |    2126 B |        1.61 |
| AsyncLazy_ExecutionAndPublication   | 200         | 0.0165 ms | 0.0001 ms | 0.0001 ms |  0.97 |    0.01 | 0.0916 |              13.1285 |           0.0106 |    1738 B |        1.31 |

### AsyncAutoResetEvent

| Method                | SW    | Hits   | Wait  | Mean      | Error    | StdDev   | Median    | Gen0      | Completed Work Items | Lock Contentions | Allocated |
|---------------------- |------ |------- |------ |----------:|---------:|---------:|----------:|----------:|---------------------:|-----------------:|----------:|
| **AutoResetEvent**        | **1/1**   | **100000** | **yield** |  **62.81 ms** | **1.047 ms** | **1.361 ms** |  **62.78 ms** |         **-** |          **100003.0000** |                **-** |  **10.68 MB** |
| AsyncExAutoResetEvent | 1/1   | 100000 | yield |  62.99 ms | 1.246 ms | 3.148 ms |  63.83 ms |         - |          100149.0000 |           4.0000 |  10.69 MB |
| AsyncAutoResetEvent   | 1/1   | 100000 | yield |  37.14 ms | 0.348 ms | 0.671 ms |  36.95 ms |         - |          100782.0000 |                - |  10.68 MB |
| **AutoResetEvent**        | **1/10**  | **100000** | **yield** | **115.43 ms** | **1.394 ms** | **1.304 ms** | **115.49 ms** |         **-** |          **102242.0000** |                **-** |  **10.69 MB** |
| AsyncExAutoResetEvent | 1/10  | 100000 | yield |  56.10 ms | 0.309 ms | 0.412 ms |  55.96 ms | 1000.0000 |          180499.0000 |        2574.0000 |  22.83 MB |
| AsyncAutoResetEvent   | 1/10  | 100000 | yield |  35.66 ms | 1.582 ms | 4.663 ms |  37.46 ms |         - |          153899.0000 |                - |  12.64 MB |
| **AutoResetEvent**        | **3/100** | **100000** | **yield** |  **67.24 ms** | **1.080 ms** | **0.958 ms** |  **67.33 ms** |         **-** |          **100102.0000** |                **-** |  **10.71 MB** |
| AsyncExAutoResetEvent | 3/100 | 100000 | yield |  53.66 ms | 1.065 ms | 2.692 ms |  53.13 ms | 1000.0000 |          170633.0000 |         901.0000 |  21.44 MB |
| AsyncAutoResetEvent   | 3/100 | 100000 | yield |  27.87 ms | 0.554 ms | 1.562 ms |  27.83 ms |         - |          105568.0000 |                - |  11.36 MB |

### AsyncManualResetEvent

| Method                  | Waiters | Hits   | Mean       | Error     | StdDev    | Median     | Completed Work Items | Lock Contentions | Allocated |
|------------------------ |-------- |------- |-----------:|----------:|----------:|-----------:|---------------------:|-----------------:|----------:|
| **ManualResetEvent**        | **1**       | **100000** | **17.1924 ms** | **0.1795 ms** | **0.1679 ms** | **17.2139 ms** |               **2.0000** |                **-** |     **728 B** |
| AsyncExManualResetEvent | 1       | 100000 |  1.5077 ms | 0.0274 ms | 0.0548 ms |  1.4874 ms |               2.0000 |                - |     728 B |
| AsyncManualResetEvent   | 1       | 100000 |  0.5551 ms | 0.0184 ms | 0.0535 ms |  0.5702 ms |               1.0000 |                - |     616 B |
| **ManualResetEvent**        | **10**      | **100000** | **13.9264 ms** | **0.6168 ms** | **1.8185 ms** | **14.1759 ms** |              **20.0000** |                **-** |    **2816 B** |
| AsyncExManualResetEvent | 10      | 100000 |  8.4997 ms | 0.2556 ms | 0.7536 ms |  8.4867 ms |              20.0000 |          19.0000 |    3144 B |
| AsyncManualResetEvent   | 10      | 100000 |  2.1814 ms | 0.0415 ms | 0.0494 ms |  2.1729 ms |              20.0000 |                - |    4096 B |

## 📜 License

This project is licensed under the [MIT](https://github.com/Tcheetox/NExtensions.Async?tab=MIT-1-ov-file#readme) License.
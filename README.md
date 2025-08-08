# NExtensions.Async

[![NuGet Version](https://img.shields.io/nuget/v/NExtensions.Async)](https://www.nuget.org/packages/NExtensions.Async/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://licenses.nuget.org/MIT)
[![.NET](https://img.shields.io/badge/.NET-6.0%20%7C%207.0%20%7C%208.0-blue)](https://dotnet.microsoft.com/)

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

| Method                              | Parallelism |          Mean |     Error | Compl. Work Items | Lock Contentions |  Allocated | Alloc Ratio |
|-------------------------------------|-------------|--------------:|----------:|------------------:|-----------------:|-----------:|------------:|
| **Lazy_ExecutionAndPublication**    | **1**       | **0.0007 ms** |  **0.00** |        **0.0200** |       **1.0189** | **0.0000** |   **386 B** |        **1.00** |
| AsyncExLazy_ExecutionAndPublication | 1           |     0.0011 ms | 0.0000 ms |            2.0009 |           0.0000 |      696 B |        1.80 |
| AsyncLazy_ExecutionAndPublication   | 1           |     0.0008 ms | 0.0000 ms |            1.0274 |           0.0000 |      708 B |        1.83 |
|                                     |             |               |           |                   |                  |            |             |
| **Lazy_ExecutionAndPublication**    | **10**      | **0.0025 ms** |  **0.03** |        **0.0648** |       **6.7474** | **0.0001** |  **1254 B** |        **1.00** |
| AsyncExLazy_ExecutionAndPublication | 10          |     0.0035 ms | 0.0000 ms |           11.1279 |           0.0002 |     1968 B |        1.57 |
| AsyncLazy_ExecutionAndPublication   | 10          |     0.0025 ms | 0.0000 ms |            6.5738 |           0.0000 |     1594 B |        1.27 |
|                                     |             |               |           |                   |                  |            |             |
| **Lazy_ExecutionAndPublication**    | **200**     | **0.0160 ms** |  **0.01** |        **0.0610** |      **12.3357** | **0.0050** |  **1341 B** |        **1.00** |
| AsyncExLazy_ExecutionAndPublication | 200         |     0.0431 ms | 0.0009 ms |           18.9797 |           1.7889 |     2231 B |        1.66 |
| AsyncLazy_ExecutionAndPublication   | 200         |     0.0160 ms | 0.0001 ms |           12.8473 |           0.0040 |     1784 B |        1.33 |

### AsyncReaderWriterLock

| Method            | Hits       | Parallelism | Wait      |            Mean |         Error | Compl. Work Items | Lock Contentions |           Gen0 |      Allocated |
|-------------------|------------|-------------|-----------|----------------:|--------------:|------------------:|-----------------:|---------------:|---------------:|
| **SemaphoreSlim** | **150000** | **1**       | **yield** |    **57.31 ms** |  **0.256 ms** |   **151662.2222** |            **-** |   **888.8889** |   **16.02 MB** |
| AsyncExLock       | 150000     | 1           | yield     |        61.42 ms |      0.445 ms |       150544.0000 |                - |      3444.4444 |        61.8 MB |
| AsyncLock         | 150000     | 1           | yield     |        57.15 ms |      0.163 ms |       151519.3333 |                - |       888.8889 |       16.02 MB |
| **SemaphoreSlim** | **150000** | **20**      | **yield** | **3,502.90 ms** | **49.994 ms** |  **6041664.0000** |      **48.0000** | **77000.0000** | **1373.29 MB** |
| AsyncExLock       | 150000     | 20          | yield     |     3,173.30 ms |     35.980 ms |      9101322.0000 |          81.0000 |    135000.0000 |     2426.15 MB |
| AsyncLock         | 150000     | 20          | yield     |     2,507.91 ms |     34.059 ms |      6032875.0000 |           6.0000 |     23000.0000 |         412 MB |

## 📄 License

This project is licensed under the [MIT](https://licenses.nuget.org/MIT) License.
# NExtensions.Async 🚀

> High-performance async synchronization primitives for modern .NET applications

[![NuGet](https://img.shields.io/nuget/v/NExtensions.Async.svg)](https://www.nuget.org/packages/NExtensions.Async)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

## ✨ What's Inside

**NExtensions.Async** provides a collection of essential async synchronization primitives designed to make concurrent programming safer and more intuitive:

-   **🔒 AsyncLock** - Mutual exclusion for async contexts
-   **📚 AsyncReaderWriterLock** - Concurrent reads, exclusive writes
-   **⚡ AsyncLazy** - Thread-safe lazy initialization for async operations

## 🎯 Why Choose NExtensions.Async?

-   **Performance First** - Optimized for high-throughput applications
-   **Modern API** - Built for async/await patterns
-   **Multi-Target** - Supports .NET 6, 7, and 8
-   **Zero Dependencies** - Lightweight and focused
-   **Production Ready** - Thoroughly tested and benchmarked

## 🚀 Quick Start

```bash
dotnet add package NExtensions.Async
```

```csharp
using NExtensions.Async;

// AsyncLock - Simple mutual exclusion
private readonly AsyncLock _lock = new();

public async Task SafeOperationAsync()
{
    using (await _lock.LockAsync())
    {
        // Critical section - only one thread at a time
        await DoSomethingAsync();
    }
}

// AsyncReaderWriterLock - Concurrent reads, exclusive writes
private readonly AsyncReaderWriterLock _rwLock = new();

public async Task<string> ReadDataAsync()
{
    using (await _rwLock.ReaderLockAsync())
    {
        // Multiple readers can access simultaneously
        return await GetDataAsync();
    }
}

// AsyncLazy - One-time initialization
private readonly AsyncLazy<ExpensiveResource> _resource =
    new(() => ExpensiveResource.CreateAsync());

public async Task<string> UseResourceAsync()
{
    var resource = await _resource.Value;
    return resource.DoSomething();
}
```

## 📖 Documentation

Full documentation and examples coming soon! In the meantime, explore the well-documented source code and comprehensive unit tests.

## 🤝 Contributing

This project is in active development. Stay tuned for contribution guidelines!

## 📄 License

MIT License - see [LICENSE](LICENSE) for details.

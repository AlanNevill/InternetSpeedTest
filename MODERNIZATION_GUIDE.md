# .NET 10 & C# 14 Modernization Guide

## Applied Refactorings ?

### 1. **Collection Expressions** (C# 12+)
**Before:**
```csharp
var latencies = new List<double>();
var differences = new List<double>();
```

**After:**
```csharp
List<double> latencies = [];
List<double> differences = [];
```

**Benefits:**
- More concise syntax
- Consistent with array initialization
- Clearer intent

**Files Modified:** `Services/CloudflareSpeedTestService.cs`

---

### 2. **Primary Constructors** (C# 12+)
**Before:**
```csharp
public class CloudflareSpeedTestService
{
    private readonly ILogger<CloudflareSpeedTestService> _logger;
    private readonly IConfiguration _configuration;
    
    public CloudflareSpeedTestService(
        ILogger<CloudflareSpeedTestService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        // initialization code...
    }
}
```

**After:**
```csharp
public class CloudflareSpeedTestService(
    ILogger<CloudflareSpeedTestService> logger,
    IConfiguration configuration) : IDisposable
{
    private readonly HttpClient _httpClient = CreateHttpClient(configuration);
    private readonly int _parallelConnections = configuration.GetValue(...);
    // Direct field initialization from constructor parameters
}
```

**Benefits:**
- Eliminates boilerplate field assignments
- Clearer constructor parameters
- Direct field initialization from constructor params

**Files Modified:** `Services/CloudflareSpeedTestService.cs`

---

### 3. **ArgumentNullException.ThrowIfNullOrWhiteSpace** (C# 11+)
**Before:**
```csharp
var connectionString = config.GetConnectionString("connLocal")
    ?? throw new InvalidOperationException("Connection string 'connLocal' is not configured.");
```

**After:**
```csharp
var connectionString = config.GetConnectionString("connLocal");
ArgumentNullException.ThrowIfNullOrWhiteSpace(connectionString, nameof(connectionString));
```

**Benefits:**
- More semantic - clearly states validation intent
- Built-in framework method
- Consistent error messages

**Files Modified:** `Program.cs`

---

### 4. **TimeProvider Abstraction** (.NET 8+)
**Before:**
```csharp
var today = DateTime.UtcNow.Date;
state.LastDailyRunUtc = DateTime.UtcNow;
```

**After:**
```csharp
// Constructor injection
internal sealed class InternetSpeedTestService(
    // ... other parameters
    TimeProvider timeProvider)

// Usage
var today = timeProvider.GetUtcNow().Date;
state.LastDailyRunUtc = timeProvider.GetUtcNow().DateTime;
```

**Program.cs registration:**
```csharp
services.AddSingleton(TimeProvider.System);
```

**Benefits:**
- **Testability**: Mock time in unit tests
- **Consistency**: Single source of truth for time
- **Best Practice**: Modern .NET pattern

**Files Modified:** `Services/InternetSpeedTestService.cs`, `Program.cs`

---

### 5. **File-Scoped Types** (C# 11+)
**Before:**
```csharp
private sealed class CompositeDisposable : IDisposable
{
    private readonly IDisposable _first;
    private readonly IDisposable _second;
    
    public CompositeDisposable(IDisposable first, IDisposable second)
    {
        _first = first;
        _second = second;
    }
}
```

**After:**
```csharp
file sealed class CompositeDisposable(IDisposable first, IDisposable second) : IDisposable
{
    private bool _disposed;
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { second.Dispose(); }
        finally { first.Dispose(); }
    }
}
```

**Benefits:**
- Prevents accidental external usage
- Clearer encapsulation
- Combined with primary constructor for brevity

**Files Modified:** `HelperLib.cs`

---

## Additional Recommendations ??

### 6. **Interceptors** (C# 12+ Preview Feature)
Use interceptors for aspect-oriented programming (logging, metrics, etc.)

**Potential Use Case:**
```csharp
// Intercept method calls to add automatic logging/telemetry
[InterceptsLocation("Program.cs", line: 50, character: 20)]
public static void InterceptRunAsync(this IInternetSpeedTestService service)
{
    using var activity = new Activity("RunSpeedTest");
    activity.Start();
    service.RunAsync();
}
```

**When to Use:**
- Cross-cutting concerns (logging, metrics, tracing)
- Performance monitoring
- Method call interception without inheritance

---

### 7. **Inline Arrays** (C# 12+)
For fixed-size buffers with better performance:

**Example:**
```csharp
[System.Runtime.CompilerServices.InlineArray(5)]
public struct PingMeasurements
{
    private double _element0;
}

// Usage
var measurements = new PingMeasurements();
measurements[0] = 12.5;
```

**When to Use:**
- Fixed-size collections
- High-performance scenarios
- Stack-allocated arrays

---

### 8. **UTF-8 String Literals** (C# 11+)
For better performance with UTF-8 APIs:

**Example:**
```csharp
ReadOnlySpan<byte> utf8Text = "CloudflareSpeedTest/1.0"u8;
```

**When to Use:**
- HTTP headers
- Network protocols
- Binary format handling

---

### 9. **List Patterns** (C# 11+)
For pattern matching on collections:

**Example:**
```csharp
if (latencies is [var first, .., var last])
{
    var range = last - first;
}
```

**Current Application:**
```csharp
// In CalculateJitter method
private static double CalculateJitter(IList<double> latencies) => latencies switch
{
    [] or [_] => 0,
    [var first, .. var rest, var last] => 
        rest.Zip(rest.Skip(1), (a, b) => Math.Abs(b - a)).Average(),
    _ => 0
};
```

---

### 10. **Required Members** (C# 11+)
Ensure properties are initialized:

**Example for Configuration POCOs:**
```csharp
public class SpeedTestConfig
{
    public required string Executable { get; init; }
    public required string Arguments { get; init; }
    public int TestDurationSeconds { get; init; } = 10;
}
```

**Benefits:**
- Compile-time validation
- Clear required vs optional properties
- Better than constructor parameters for many properties

---

### 11. **Generic Math** (C# 11+, .NET 7+)
For reusable numeric algorithms:

**Example:**
```csharp
public static T Average<T>(this IEnumerable<T> values)
    where T : INumber<T>, IDivisionOperators<T, int, T>
{
    var sum = T.Zero;
    var count = 0;
    foreach (var value in values)
    {
        sum += value;
        count++;
    }
    return sum / count;
}
```

**When to Use:**
- Generic numeric calculations
- Type-safe mathematical operations
- Performance-critical code

---

### 12. **Improved Lambda Expressions**
Use natural type for lambda expressions:

**Example:**
```csharp
// Natural type inference
var lambda = (int x, int y) => x + y;

// Lambda attributes
var loggedAction = [Logging] async () => await PerformOperation();
```

---

### 13. **nameof Scope Extension** (C# 11+)
Access parameter names in attributes:

**Example:**
```csharp
public void ProcessData([CallerArgumentExpression(nameof(data))] string? expr = null, object? data = null)
{
    logger.LogInformation("Processing: {Expression}", expr);
}
```

---

### 14. **Raw String Literals** (C# 11+)
For SQL queries, JSON templates, or multiline strings:

**Example:**
```csharp
string json = """
    {
        "type": "result",
        "timestamp": "2025-01-01T00:00:00.000Z",
        "ping": {
            "latency": 15.5
        }
    }
    """;
```

**Potential Use:** Email templates in `HelperLib.cs`

---

### 15. **Async Streams Improvements** (.NET 8+)
Better async enumerable support:

**Example:**
```csharp
await foreach (var result in GetSpeedTestResultsAsync())
{
    await ProcessResult(result);
}
```

---

## Performance Recommendations ??

### 1. **Use `CompositeFormat` for Hot Paths** (.NET 8+)
**Before:**
```csharp
var message = string.Format("Download: {0:F2} Mbps", speed);
```

**After:**
```csharp
private static readonly CompositeFormat DownloadFormat = 
    CompositeFormat.Parse("Download: {0:F2} Mbps");

var message = string.Format(null, DownloadFormat, speed);
```

### 2. **Use `SearchValues<T>` for String Searching** (.NET 8+)
```csharp
private static readonly SearchValues<char> LineBreaks = SearchValues.Create("\r\n");
var index = text.AsSpan().IndexOfAny(LineBreaks);
```

### 3. **Use `FrozenDictionary` and `FrozenSet`** (.NET 8+)
For immutable, read-optimized collections:

```csharp
private static readonly FrozenDictionary<string, string> ConfigKeys = 
    new Dictionary<string, string>
    {
        ["colo"] = "Colo",
        ["loc"] = "Location",
        ["ip"] = "ClientIp"
    }.ToFrozenDictionary();
```

---

## Testing Recommendations ??

With `TimeProvider`, you can now write better unit tests:

```csharp
[Test]
public async Task RunDailyIfNeededAsync_AlreadyRanToday_ReturnsFalse()
{
    // Arrange
    var fakeTime = new FakeTimeProvider(new DateTimeOffset(2025, 1, 15, 10, 0, 0, TimeSpan.Zero));
    var service = new InternetSpeedTestService(
        /* other dependencies */,
        fakeTime
    );
    
    // Act
    var result = await service.RunDailyIfNeededAsync();
    
    // Assert
    Assert.False(result);
}
```

---

## Migration Checklist ?

- [x] Collection expressions
- [x] Primary constructors
- [x] ArgumentNullException.ThrowIfNullOrWhiteSpace
- [x] TimeProvider abstraction
- [x] File-scoped types (where appropriate)
- [ ] Raw string literals (for templates)
- [ ] List patterns (optional)
- [ ] Required members (for DTOs)
- [ ] Generic math (if needed)
- [ ] Performance: CompositeFormat
- [ ] Performance: SearchValues
- [ ] Performance: FrozenCollections

---

## Summary

Your codebase now leverages several modern C# and .NET features that improve:

1. **Readability**: Collection expressions, primary constructors
2. **Testability**: TimeProvider abstraction
3. **Maintainability**: Cleaner code with less boilerplate
4. **Type Safety**: Argument validation helpers

The additional recommendations can be applied incrementally based on your specific needs and performance requirements.

---

## Resources

- [What's New in C# 14](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14)
- [What's New in .NET 10](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10)
- [TimeProvider Documentation](https://learn.microsoft.com/en-us/dotnet/api/system.timeprovider)
- [Collection Expressions](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/collection-expressions)

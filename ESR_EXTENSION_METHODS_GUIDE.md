# ESR Services Extension Methods - Quick Reference

## Installation

The extension methods are included in the `SUS.EOS.EosioSigningRequest` library.

```bash
# If publishing as NuGet package
dotnet add package SUS.EOS.EosioSigningRequest

# Or as project reference
<ProjectReference Include="path/to/SUS.EOS.EosioSigningRequest.csproj" />
```

## Import

```csharp
using SUS.EOS.EosioSigningRequest;
```

## Extension Methods

### 1. AddEsrServices()
Simple in-memory storage (non-persistent).

```csharp
builder.Services.AddEsrServices();
```

**Registers:**
- `IEsrService` → `EsrService`
- `IEsrStateStore` → `MemoryEsrStateStore`
- `IEsrSessionManager` → `EsrSessionManager`
- `HttpClient` via `AddHttpClient()`

**Use Cases:**
- Console applications
- Testing/development
- Temporary sessions
- When persistence not needed

---

### 2. AddEsrServices(bool useMemoryStore)
Control whether to use memory store.

```csharp
// Use memory store (default)
builder.Services.AddEsrServices(useMemoryStore: true);

// Don't register state store (you must register it yourself)
builder.Services.AddSingleton<IEsrStateStore, MyCustomStore>();
builder.Services.AddEsrServices(useMemoryStore: false);
```

---

### 3. AddEsrServices<TStateStore>()
Register with custom state store type.

```csharp
builder.Services.AddEsrServices<MyCustomStateStore>();
```

**Requirements:**
- `TStateStore` must implement `IEsrStateStore`
- Must be a `class`

**Example:**
```csharp
public class SqliteEsrStateStore : IEsrStateStore
{
    // Implementation...
}

builder.Services.AddEsrServices<SqliteEsrStateStore>();
```

---

### 4. AddEsrServices(Func<IServiceProvider, IEsrStateStore>)
Register with factory function for complex initialization.

```csharp
builder.Services.AddEsrServices(sp => 
{
    var config = sp.GetRequiredService<IConfiguration>();
    var connectionString = config["ConnectionString"];
    return new SqliteEsrStateStore(connectionString);
});
```

**Use Cases:**
- Need to inject other services into state store
- Complex initialization logic
- Configuration-based store selection

## Platform-Specific Examples

### MAUI (Persistent)
```csharp
// Register MAUI Preferences
builder.Services.AddSingleton(Preferences.Default);

// Register ESR services with MAUI state store
builder.Services.AddEsrServices(sp => 
    new MauiEsrStateStore(sp.GetRequiredService<IPreferences>())
);
```

### Console App (In-Memory)
```csharp
var services = new ServiceCollection();
services.AddEsrServices();
var provider = services.BuildServiceProvider();
```

### ASP.NET Core (Database)
```csharp
builder.Services.AddDbContext<AppDbContext>();
builder.Services.AddEsrServices<DatabaseEsrStateStore>();
```

### Blazor Server (Session Storage)
```csharp
builder.Services.AddScoped<IEsrStateStore, BlazorSessionStateStore>();
builder.Services.AddEsrServices(useMemoryStore: false);
```

### WPF/WinForms (File-Based)
```csharp
services.AddEsrServices(sp => 
{
    var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    var stateFile = Path.Combine(appData, "MyApp", "esr-state.json");
    return new FileEsrStateStore(stateFile);
});
```

## Custom State Store Implementation

```csharp
using SUS.EOS.EosioSigningRequest.Services;

public class MyCustomStateStore : IEsrStateStore
{
    private readonly Dictionary<string, string> _storage = new();

    public string Get(string key, string defaultValue)
    {
        return _storage.TryGetValue(key, out var value) ? value : defaultValue;
    }

    public void Set(string key, string value)
    {
        _storage[key] = value;
        // Save to disk, database, etc.
    }

    public void Remove(string key)
    {
        _storage.Remove(key);
    }

    public void Clear()
    {
        _storage.Clear();
    }
}

// Register it
builder.Services.AddEsrServices<MyCustomStateStore>();
```

## Dependency Injection Resolution

After calling `AddEsrServices()`, you can inject:

```csharp
public class MyService
{
    public MyService(
        IEsrService esrService,
        IEsrSessionManager sessionManager,
        IEsrStateStore stateStore  // Optional
    )
    {
        // Use the services...
    }
}
```

## What Gets Registered

| Service | Lifetime | Implementation |
|---------|----------|----------------|
| `IEsrService` | Singleton | `EsrService` |
| `IEsrSessionManager` | Singleton | `EsrSessionManager` |
| `IEsrStateStore` | Singleton | Varies (Memory/Custom) |
| `HttpClient` | Transient | Framework provided |

## Troubleshooting

### "AddHttpClient not found"
Make sure you have the package:
```xml
<PackageReference Include="Microsoft.Extensions.Http" Version="9.0.0" />
```

### "IEsrStateStore not registered"
If you use `useMemoryStore: false`, you must register it manually:
```csharp
builder.Services.AddSingleton<IEsrStateStore, MyStore>();
builder.Services.AddEsrServices(useMemoryStore: false);
```

### Multiple registrations
Only call `AddEsrServices()` once. If called multiple times, the last one wins.

## Testing

Mock the state store for unit tests:

```csharp
[Fact]
public async Task TestEsrManager()
{
    var services = new ServiceCollection();
    services.AddEsrServices(); // Uses memory store
    
    var provider = services.BuildServiceProvider();
    var manager = provider.GetRequiredService<IEsrSessionManager>();
    
    Assert.NotNull(manager);
    Assert.NotEmpty(manager.LinkId);
}
```

---

**Quick Start**: For most applications, just use `builder.Services.AddEsrServices()` and you're done!

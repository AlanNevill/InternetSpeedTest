# Copilot Instructions — InternetSpeedTest

## Project Overview

.NET 10 Windows console app that runs internet speed tests hourly via Windows Task Scheduler. Supports two providers: **Ookla** (spawns `speedtest.exe`) and **Cloudflare** (HTTP-based, no external executable). Results are stored in SQL Server, and a daily email report is enqueued via the `EmailerUtility` package.

## Build & Run

```powershell
# Build
dotnet build

# Build Release
dotnet build -c Release

# Publish self-contained to deployment folder
dotnet publish InternetSpeedTest.csproj -c Release -o C:\ScheduledTasks\InternetSpeedTest

# Run locally
dotnet run
```

There is no test project. `TimeProvider` is injected for testability if tests are added.

## Architecture

### Entry point flow (`Program.cs`)
1. Bootstraps Serilog early (console-only), then reconfigures it via `appsettings.json` using `UseSerilog()`.
2. Builds a generic `IHost` with DI: `PopsContext` (speed data DB), `CloudflareSpeedTestService`, `IInternetSpeedTestService`, and EmailerUtility. There is no local `Emailer` DbContext — mail is enqueued via `EmailerClient`.
3. Resolves `IInternetSpeedTestService` and calls `RunAsync()` then `RunDailyIfNeededAsync()`.

### Service layer (`Services/`)
- **`IInternetSpeedTestService`** — interface with `RunAsync()` and `RunDailyIfNeededAsync()`.
- **`InternetSpeedTestService`** — primary service. Delegates to `CloudflareSpeedTestService` or spawns `speedtest.exe` depending on `SpeedTest:UseCloudflare` config. Persists result to SQL Server. Tracks daily run via `daily-state.json`.
- **`CloudflareSpeedTestService`** — HTTP-based test using `speed.cloudflare.com`. Parallel connections, warmup phase, time-based measurement. Returns an Ookla-compatible JSON DTO so both paths share the same `PersistAsync()` code. Implements `IDisposable` (owns `HttpClient`).

### Data models (`DataModels/`)
- **`PopsContext`** — EF Core `DbContext` for the `pops` database. Contains `InternetSpeed` entity and `VGigaClearByDay` view.
- **`InternetSpeed`** — maps to the `InternetSpeed` table.
- **`VGigaClearByDay`** — read-only view used for daily summary emails.
- **`InternetSpeedJSON`** — deserialization POCOs matching Ookla JSON schema; `CloudflareSpeedTestService` produces the same shape via `ToOoklaCompatibleJson()`.

### `HelperLib` (static utility class)
- `BeginMethodScope()` / `BeginMethodScopeLocal()` — push `MethodName` (and optionally `SourceContext`) into Serilog's `LogContext`. **Use these at the top of every significant method.**
- `FormatEmailForAcs()` — builds the daily HTML speed summary email.
- `HtmlToText()` — strips HTML to plain text for email fallback.

### `EmailerUtility` (NuGet package)
Registered via `services.AddEmailerUtility(config)`. `EmailerClient.EnqueueAsync()` inserts an email record into the `Emailer` SQL Server database for async delivery.

## Key Conventions

### Logging pattern
Every method of consequence opens a scope at the top:
```csharp
using var _ = HelperLib.BeginMethodScope(); // injects CallerMemberName as MethodName
```
The Serilog output template renders `[{SourceContext:l}].[{MethodName}]` so logs are fully traceable.

### C# language style
The codebase targets **C# latest (`preview`) on .NET 10** and actively uses modern features:
- **Primary constructors** for all service classes (not traditional `_field` assignments).
- **Collection expressions**: `List<double> items = [];` not `new List<double>()`.
- **`ArgumentNullException.ThrowIfNullOrWhiteSpace()`** for guard clauses.
- **`TimeProvider`** injected and used everywhere instead of `DateTime.UtcNow` — keeps code testable.
- **`file`-scoped types** for implementation details private to a file.
- **`sealed`** on internal/private classes by default.

### Configuration
All settings are in `appsettings.json`. Key sections:
- `ConnectionStrings:connLocal` — `pops` database.
- `ConnectionStrings:Emailer` — emailer database.
- `SpeedTest:UseCloudflare` — `true` = Cloudflare, `false` = Ookla.
- `DailyRun:StatePath` — path to `daily-state.json` (prevents duplicate daily runs).

Sensitive overrides go in **User Secrets** (UserSecretsId configured in `.csproj`).

### NuGet sources
`NuGet.Config` includes a local feed at `C:\LocalPackages` and a local `EmailerUtility` nupkg path. The `EmailerUtility` package is consumed as a NuGet package (not a project reference).

### Deployment target
`C:\ScheduledTasks\InternetSpeedTest` — run hourly by Windows Task Scheduler. Log files go to `E:\Logs\BEELINK-1\`.

### `InternetSpeedTestLib.cs`
Legacy static class kept for reference only. Its `SpeedTest()` and `ProcessResult()` methods throw `NotSupportedException` — do not call them. Use `IInternetSpeedTestService` via DI instead.

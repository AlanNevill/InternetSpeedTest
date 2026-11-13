# Copilot Instructions — InternetSpeedTest

## Project Overview

.NET 11 Windows console app that runs internet speed tests hourly via Windows Task Scheduler. Supports two providers: **Ookla** (spawns `speedtest.exe`) and **Cloudflare** (HTTP-based, no external executable). Results are stored in SQL Server, and a daily email report is enqueued via the `EmailerUtility` package.

## Build & Run

```powershell
# Build
dotnet build

# Build Release
dotnet build -c Release

# Publish (preferred: self-contained single file) to the deployment folder
dotnet publish InternetSpeedTest.csproj --configuration Release --runtime win-x64 `
  --self-contained true -p:PublishSingleFile=true --output C:\ScheduledTasks\InternetSpeedTest

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
  - **Download and upload are independent phases** (`RunPhaseAsync`): a failure in one must not discard the other. Ping/server-info failure is still fatal for the run.
  - **Upload is resilient to Cloudflare `/__up` stalls/502s**: 10s per-request timeout, retry instead of `break`, a connection gives up after 5 consecutive errors, and the phase is hard-stopped 3s after `TestDurationSeconds`. Do not revert to "first failure ends the connection" or to timing without a hard stop — a hung request then inflates elapsed time and stores a falsely low speed.
  - Each phase returns `CloudflareSpeedResult { Bandwidth, Ok, Errors }`; the JSON carries a `status` object (`downloadOk`, `uploadOk`, `uploadErrors`) that `PersistAsync()` stores.
- **`DailyReportBuilder`** — static class building the daily email: `FormatHtml(summary, reportDate)` and `ToPlainText(html)` (uses HtmlAgilityPack). The report date is passed in by the caller; do not recompute it from `DateTime.Today`.

### Data models (`DataModels/`)
- **`PopsContext`** — EF Core `DbContext` for the `InternetSpeedTest` database (the class name is historical; the data moved out of `pops` on 2026-11-07). Contains `InternetSpeed` entity and `VGigaClearByDay` view.
- **`InternetSpeed`** — maps to the `InternetSpeed` table, including `DownloadOk`, `UploadOk`, `UploadErrors`. `NULL` in the `*Ok` columns means a legacy row (treated as good by the views); a failed phase stores its bandwidth as `NULL`. `DayNum`, `HourOfDay` and `SmallDate` are computed columns and are not mapped.
- **`VGigaClearByDay`** — read-only view used for daily summary emails. Averages each direction over its own good samples and exposes `NumDownFailed` / `NumUpFailed`. Schema changes (columns, view definitions) are made in SQL, not by EF migrations; there are none.
- **`InternetSpeedJSON`** — deserialization POCOs matching Ookla JSON schema; `CloudflareSpeedTestService` produces the same shape via `ToOoklaCompatibleJson()`.

### `HelperLib` (static utility class)
- `BeginMethodScope()` / `BeginMethodScopeLocal()` — push `MethodName` (and optionally `SourceContext`) into Serilog's `LogContext`. **Use these at the top of every significant method.** This is all it contains; email formatting lives in `DailyReportBuilder`.

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
The codebase targets **C# 15 on .NET 11** and actively uses modern features:
- **Primary constructors** for all service classes (not traditional `_field` assignments).
- **Collection expressions**: `List<double> items = [];` not `new List<double>()`.
- **`ArgumentNullException.ThrowIfNullOrWhiteSpace()`** for guard clauses.
- **`TimeProvider`** injected in the services for the daily gate and report dates instead of `DateTime.UtcNow` — keeps code testable.
- **`file`-scoped types** for implementation details private to a file.
- **`sealed`** on internal/private classes by default.

### Configuration
All settings are in `appsettings.json`. Key sections:
- `ConnectionStrings:connLocal` — `InternetSpeedTest` database.
- `ConnectionStrings:Emailer` — emailer database.
- `SpeedTest:UseCloudflare` — `true` = Cloudflare, `false` = Ookla.
- `DailyRun:StatePath` — path to `daily-state.json` (prevents duplicate daily runs).

Sensitive overrides go in **User Secrets** (UserSecretsId configured in `.csproj`).

### NuGet sources
`NuGet.Config` uses nuget.org plus a local feed at `C:\LocalPackages`. The `EmailerUtility` package is consumed as a NuGet package (not a project reference), so a change to it only reaches this app after the package is rebuilt, its version bumped, and the reference here updated.

`System.Security.Cryptography.Pkcs` is referenced directly only to override the vulnerable version pulled in transitively by `Microsoft.Data.SqlClient`; leave it unless SqlClient is upgraded.

### Deployment target
`C:\ScheduledTasks\InternetSpeedTest` — run hourly by Windows Task Scheduler. Log files go to `E:\Logs\BEELINK-1\`.

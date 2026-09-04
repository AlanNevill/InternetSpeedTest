# CLAUDE.md

Guidance for Claude Code (claude.ai/code) when working in this repository.

## What this is

**InternetSpeedTest** is a .NET 10 **console application** — not a service. A Windows Scheduled Task runs
it **hourly** from `C:\ScheduledTasks\InternetSpeedTest`; it measures the connection, writes the result to
SQL Server, and **once a day** emails a summary of the previous day. It runs to completion and exits, so
there is no host loop, no long-lived state, and every scheduling decision is made from disk.

It is one of three apps that enqueue mail through the **EmailerUtility** package for `EmailerService` to
send — see `..\Email\EmailerService\CLAUDE.md`, and the TODO at the bottom of this file.

## Commands

```powershell
dotnet build
dotnet run                    # runs one cycle and exits, exactly as the scheduled task does
# Framework-dependent publish
dotnet publish InternetSpeedTest.csproj -c Release -o C:\ScheduledTasks\InternetSpeedTest

# Single-file publish (preferred deployment - see README.md "Build and Deploy")
dotnet publish InternetSpeedTest.csproj --configuration Release --runtime win-x64 `
  --self-contained true -p:PublishSingleFile=true --output C:\ScheduledTasks\InternetSpeedTest
```

**There are no tests.** No test project exists, so every change is verified by running it.

## Architecture

### One pass, then exit (`Program.cs`)

`Host.CreateDefaultBuilder` → resolve `IInternetSpeedTestService` → `RunAsync()` then
`RunDailyIfNeededAsync()` → dispose. Two things follow from that:

- **The hourly cadence lives in Task Scheduler, not in the code.** Nothing here sleeps or loops.
- **Anything remembered between runs must be on disk or in the database.**

### The daily gate is a file, not memory

`RunDailyIfNeededAsync` reads and writes `daily-state.json`, whose location comes from
`DailyRun:StatePath` (`C:\ScheduledTasks\InternetSpeedTest\daily-state.json`). Because the process exits
every hour, this file is the *only* thing preventing 24 daily reports a day.

The comparison is deliberately in **local** time. Two commits exist purely about this
(`RunDailyIfNeededAsync firing too early at midnight local time`, and a `yesterday` UTC/local mismatch),
so treat any change from local to UTC here as a regression unless you have re-derived the reasoning.

A failure to persist the state is logged as a warning and **not** treated as fatal, so a run whose save
fails will repeat its daily work on the next hourly pass.

### Speed measurement — two implementations

`SpeedTest:UseCloudflare` (currently `true`) selects `CloudflareSpeedTestService`, which measures in
process. Otherwise the app shells out to `SpeedTest:Executable` (`speedtest.exe`) with
`--accept-license --accept-gdpr --format=json` and parses the JSON. `SpeedTest:LowSpeedWarning` (100.0)
is the threshold the report highlights.

### One local context, plus the shared mail queue

| Connection | Used by | Holds |
| --- | --- | --- |
| `connLocal` | `PopsContext` | Speed results, plus the `VGigaClearByDay` view the daily report reads |
| `Emailer` | `EmailerUtility` (package) | The shared mail queue |

`PopsContext` is registered with a **120-second command timeout**, which is deliberate: a commit exists
fixing a DB timeout on this path.

**There is no local `Emailer` DbContext, and there must not be one again.** Mail is enqueued only through
`EmailerUtility.EmailerClient.EnqueueAsync`, which uses the `EmailerDataModels` package. A scaffolded
duplicate of those entities lived in `DataModels/Emailer/` until 2026-09-04 — dead code that nothing
resolved, and a trap: its `EmailMessage.Subject` still carried `[Unicode(false)]` after the column became
`nvarchar(512)`, so anyone who started writing mail rows through it would have silently reintroduced the
non-ASCII mangling described in the TODO below. **Send mail through `EmailerClient` only.**

`Program.cs` still validates `ConnectionStrings:Emailer` at startup even though nothing in this app opens
that connection — EmailerUtility resolves it itself, and a missing value should fail at startup rather
than mid-send.

## Configuration

`appsettings.json` is **tracked in git**. Both connection strings use `Integrated Security`, so it holds
no credentials today — keep it that way. A `UserSecretsId` is present for anything that does need
protecting.

## Logging

Serilog to console plus `E:\Logs\BEELINK-1\InternetSpeedTest-.log` — 4 MiB roll, daily, **7 retained**.

> **The machine name is hard-coded into that path.** Run this on any machine other than BEELINK-1 and the
> logs still land under `E:\Logs\BEELINK-1\`. Unlike FinRite and EmailerService, nothing derives the
> folder from `Environment.MachineName`.

## Deployment

Published to `C:\ScheduledTasks\InternetSpeedTest` and driven by an hourly scheduled task; see README.md
for the task setup and post-deployment checks. `dotnet publish` adds and overwrites but never deletes, so
`daily-state.json` survives a deploy — which is what you want, since deleting it causes a duplicate daily
report.

**The preferred mode is a self-contained single-file publish**, matching PcMaintenance. Three things in
the code exist only to make that mode work, and none of them fail in a framework-dependent publish, so a
regression is invisible until someone publishes single-file:

- `Program.cs` reads the build-date banner from **`Environment.ProcessPath`**, not `Assembly.Location`,
  which is an empty string inside a bundle (`InternetSpeedTestLib.BuildConfig` uses
  `AppContext.BaseDirectory` for the same reason). The IL3000 analyzer catches regressions, but only
  during a single-file publish.
- `Program.cs` names the **Serilog sink assemblies explicitly** in a `ConfigurationReaderOptions`;
  `DependencyContext`-based discovery does not work in a bundle. Adding a sink package means adding its
  assembly there too.
- `Program.cs` pins **`.UseContentRoot( AppContext.BaseDirectory )`** so `appsettings.json` is found
  whatever the working directory. Do not replace this with a JSON file re-added in
  `ConfigureAppConfiguration` — that lands after the env-var and command-line providers and overrides
  both.

Verified 2026-09-04: single-file publish is warning-clean and a run from an unrelated working directory
starts, logs to both sinks, completes a Cloudflare test and exits 0.

Versioning is **manual and inconsistent**: `<Version>1.0.10</Version>` alongside
`<AssemblyVersion>1.0.0.1206</AssemblyVersion>` and `<FileVersion>2.0.0.0826</FileVersion>`, which
disagree with each other and with the package version the app logs at startup. The many
`AssemblyInfo*`/`UpdateAssemblyVersion` properties in the csproj are settings for a Visual Studio
extension and do nothing on the command line. Sibling projects use MinVer (PcMaintenance) or
Nerdbank.GitVersioning (EmailerService); this one has neither.

## TODO — update EmailerUtility so subjects can hold non-ASCII

**This app cannot currently put an emoji, or any non-ASCII character, in an email subject, and fails
silently when it tries.**

On 2026-09-03 `Emailer.dbo.EmailMessages.Subject` was widened from `varchar(512)` to `nvarchar(512)`, and
`[Unicode(false)]` was removed from `EmailMessage.Subject` in `EmailerDataModels`. **Both halves are
required** — widening the column achieves nothing while EF still sends `varchar` parameters, and the
mapping lives in the entity, not in the calling code.

This project references **`EmailerUtility 0.1.15` as a package only**, with no project reference, so it
inherits the old mapping through the transitive `EmailerDataModels`. PcMaintenance project-references
`EmailerDataModels` and so picked the fix up on a rebuild; this app and FinRite did not.

Nothing is being lost today, because the subject is currently plain ASCII
(`Daily Internet Speed Test Report for <date>`). The limitation only bites when someone adds a character
outside ASCII.

**To close it:**

1. Rebuild and repack `EmailerDataModels`, then `EmailerUtility` (which project-references it), bumping
   both versions. They publish to `E:\Repos\Email\EmailerUtility\nupkgs`, which is the `EmailerUtilityLocal`
   feed in `NuGet.Config`. That folder currently holds `EmailerUtility.0.1.14/0.1.15` and
   `EmailerDataModels.1.0.0`.
2. Bump `<PackageReference Include="EmailerUtility" Version="0.1.15" />` in `InternetSpeedTest.csproj`.
3. Do FinRite at the same time — it has the identical problem, and one repack unblocks both. See
   `..\FinRite\CLAUDE.md`.
4. Verify **from the data, not the console**: `sqlcmd` prints `??` for astral-plane characters whether or
   not the stored value is intact, which looks exactly like the failure.

   ```sql
   SELECT UNICODE(SUBSTRING(Subject,1,1)) FROM EmailMessages WHERE MessageId = <id>;
   ```

   `55357` (a UTF-16 high surrogate) means intact; `63` is a literal `?` and means it was mangled.

The duplicate `DataModels/Emailer/` model that would otherwise reintroduce the same bug has already been
deleted (2026-09-04).

## Other documentation in this repo

Substantial and worth reading before assuming anything here is complete:

| File | Covers |
| --- | --- |
| `README.md` | Fullest account: usage, configuration, build/deploy, scheduled task setup |
| `WARP.md` | Guidance for a different agent tool; overlaps this file |
| `.github/copilot-instructions.md` | Ditto, for Copilot |

Three agent-guidance files now exist (`WARP.md`, `copilot-instructions.md`, this one). They will drift.
If you correct something here that the others also state, correct it there too or delete the stale copy.

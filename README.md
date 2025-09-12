# InternetSpeedTest

CLI application for automated internet speed testing. Runs every hour via Task Scheduler job.
Supports both Ookla Speedtest CLI and Cloudflare speed testing, with results stored in a database.

## Speed Test Providers

### Ookla Speedtest (Default)
Runs the official SPEEDTEST.EXE from Ookla and processes JSON output into database table.
- Uses global Ookla server network
- Requires speedtest.exe executable
- Stores actual Speedtest.net result URLs

### Cloudflare Speed Test
Custom HTTP-based speed testing using Cloudflare's global CDN network.
- Tests against Cloudflare edge servers
- No external executable required
- Progressive download/upload testing (1MB → 10MB → 25MB)
- 5-ping latency measurement with jitter calculation
- Stores "CloudFlare" as ResultUrl identifier

## Configuration

Configure speed test provider in `appsettings.json`:

```json
"SpeedTest": {
  "UseCloudflare": false,        // true = Cloudflare, false = Ookla
  "Executable": "speedtest.exe", // Ookla executable path
  "Arguments": "--accept-license --accept-gdpr --format=json"
}
```

## Features

- **Dual Provider Support**: Choose between Ookla or Cloudflare testing
- **Automated Scheduling**: Runs every hour via Windows Task Scheduler
- **Database Storage**: Results stored in SQL Server with consistent schema
- **Daily Email Reports**: Automated daily summary emails
- **Comprehensive Logging**: Serilog with file and console output
- **JSON Processing**: Robust deserialization with error handling
- **Data Quality**: Raw bandwidth values (x10 correction disabled)

## Usage

### Manual Execution
```bash
# Run with default Ookla testing
dotnet run

# Enable Cloudflare testing (modify appsettings.json first)
dotnet run
```

### Scheduled Execution
The application is designed to run via Windows Task Scheduler every hour:
- Executable: `InternetSpeedTest.exe`
- Schedule: Hourly
- Working Directory: Application bin folder

## Database Schema

Results are stored in the `InternetSpeed` table:

| Column | Type | Description |
|--------|------|-------------|
| ResultDateTime | DateTime | Local timestamp of test |
| DownLoadBandwidth | bigint | Download speed (bytes/sec) |
| UploadBandWidth | bigint | Upload speed (bytes/sec) |
| PingLatency | float | Average ping latency (ms) |
| PingJitter | float | Ping jitter (ms) |
| PingHigh | float | Highest ping (ms) |
| PingLow | float | Lowest ping (ms) |
| ResultUrl | string | Speedtest.net URL or "CloudFlare" |
| ResultJson | string | Complete JSON response |

## Logging

Logs are written to:
- **Console**: Real-time output during execution
- **File**: `C:\logs\InternetSpeedTest\InternetSpeedTest-{date}.log`
- **Retention**: 10 files, 4MB each, daily rotation

## Dependencies

- **.NET 9.0**: Runtime environment
- **SQL Server**: Database storage (LocalDB supported)
- **Ookla Speedtest CLI**: Optional, for Ookla testing
- **Internet Connection**: Required for both test providers

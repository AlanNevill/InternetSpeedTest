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
Advanced HTTP-based speed testing using Cloudflare's global CDN network with browser-level performance optimizations.
- **Parallel multi-connection testing** (default: 4 simultaneous connections)
- **HTTP/2 optimized** with connection pooling and reuse
- **Connection warmup phase** to overcome TCP slow-start effects
- **Streaming data transfer** for accurate throughput measurement
- **Time-based testing** (10-second duration) instead of fixed data sizes
- **Configurable parameters** for fine-tuning performance
- 5-ping latency measurement with jitter calculation
- No external executable required
- Stores "CloudFlare" as ResultUrl identifier

## Configuration

Configure speed test provider in `appsettings.json`:

```json
"SpeedTest": {
  "UseCloudflare": false,        // true = Cloudflare, false = Ookla
  "Executable": "speedtest.exe", // Ookla executable path
  "Arguments": "--accept-license --accept-gdpr --format=json",
  "Cloudflare": {
    "ParallelConnections": 4,     // Number of simultaneous connections
    "TestDurationSeconds": 10,   // Duration of each test phase
    "WarmupDurationSeconds": 2   // Connection warmup time
  }
}
```

## Performance Optimizations

The Cloudflare speed test implementation includes advanced optimizations to match browser-based test accuracy:

### HTTP Client Optimizations
- **HTTP/2 Preferred**: Uses HTTP/2 with fallback to HTTP/1.1
- **Connection Pooling**: Optimized connection reuse (5min lifetime, 2min idle timeout)
- **Parallel Connections**: Multiple simultaneous connections (configurable, default: 4)
- **Streaming Transfer**: Data flows without buffering entire responses
- **Compression Disabled**: Accurate speed measurement without encoding overhead
- **Proxy Bypass**: Direct connection to test servers

### Testing Methodology
- **Connection Warmup**: Pre-establishes connections to overcome TCP slow-start
- **Time-Based Testing**: Fixed duration (10s) vs. fixed data sizes for consistent measurement
- **Parallel Upload/Download**: Multiple streams maximize connection utilization
- **High-Resolution Timing**: Precise measurement excluding connection establishment overhead

### Configuration Tuning
Adjust parameters based on your connection characteristics:
- **More connections** (6-8) for very high-speed connections (>500 Mbps)
- **Longer test duration** (15-20s) for more stable results
- **Shorter warmup** (1s) for consistent low-latency connections

### Expected Performance Improvements
With these optimizations, Cloudflare speed tests should:
- **Match browser results**: Typically within 5-10% of web-based Cloudflare speed tests
- **Utilize full bandwidth**: Parallel connections maximize throughput
- **Provide consistent results**: Time-based testing reduces variability
- **Handle high-speed connections**: Optimized for gigabit+ connections

## Features

- **Dual Provider Support**: Choose between Ookla or Cloudflare testing
- **Browser-Level Accuracy**: Cloudflare tests match web-based results
- **Automated Scheduling**: Runs every hour via Windows Task Scheduler
- **Database Storage**: Results stored in SQL Server with consistent schema
- **Daily Email Reports**: Automated daily summary emails
- **Drive Space / PC Health Report**: Daily HTML table of local fixed drives (capacity, free GB, % free, LOW flag < 10%)
- **Comprehensive Logging**: Serilog with file and console output
- **JSON Processing**: Robust deserialization with error handling
- **Configurable Performance**: Tunable parameters for optimal results

## Daily Reports
The daily job performs:
1. Internet speed summary for the previous day (aggregate stats)


Drives with less than 10% free space are highlighted and marked LOW.

## Usage

### Manual Execution
```bash
# Run with default Ookla testing
dotnet run

# Run with optimized Cloudflare testing (set UseCloudflare: true in appsettings.json)
dotnet run
```

### Performance Tuning Examples

**High-Speed Connection (>500 Mbps)**:
```json
"Cloudflare": {
  "ParallelConnections": 6,
  "TestDurationSeconds": 15,
  "WarmupDurationSeconds": 3
}
```

**Stable/Consistent Connection**:
```json
"Cloudflare": {
  "ParallelConnections": 4,
  "TestDurationSeconds": 20,
  "WarmupDurationSeconds": 1
}
```

**Lower-Speed/Unstable Connection**:
```json
"Cloudflare": {
  "ParallelConnections": 2,
  "TestDurationSeconds": 12,
  "WarmupDurationSeconds": 3
}
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
- **File**: `C:\\Synv\\logs\\BEELINK-1\\InternetSpeedTest-{date}.log`
- **Retention**: 7 files, 4MB each, daily rotation

### Cloudflare Test Logging
Detailed logging includes:
- Configuration parameters (connections, duration, warmup time)
- Connection warmup progress and results
- Per-connection performance metrics during testing
- Aggregate results with breakdown by upload/download
- Connection failures and retry information
- Final speed calculations in both bytes/sec and Mbps

## Troubleshooting Build: External EmailerUtility Project

- The build failed because the external project path didn�t exist. I updated `InternetSpeedTest.csproj` to default to the local `EmailerUtility` and only reference the external path when you set `UseExternalEmailer=true` and the csproj exists.
- Build now succeeds.

When you finish copying to `C:\\Users\\alann\\OneDrive\\Repos\\Email\\EmailerUtility`:
- Set a property in a `Directory.Build.props` or via CLI: `/p:UseExternalEmailer=true`
- Or edit the csproj to set `UseExternalEmailer` to `true`.

## Troubleshooting Speed Test Accuracy

### Cloudflare Results Lower Than Expected

If Cloudflare results are still lower than browser-based tests:

1. **Increase parallel connections**:
   - Try 6-8 connections for very fast connections (>500 Mbps)
   - Monitor logs for connection failures

2. **Extend test duration**:
   - Use 15-20 seconds for more stable measurements
   - Longer tests average out temporary fluctuations

3. **Check system resources**:
   - Ensure CPU isn't limiting (multiple parallel streams are CPU-intensive)
   - Close other network-intensive applications during testing

4. **Network configuration**:
   - Verify IPv6 connectivity (check logs for IP addresses used)
   - Test different times of day to rule out ISP throttling
   - Temporarily disable VPN/proxy if enabled

### Ookla vs Cloudflare Differences

- **Ookla**: Tests against ISP-optimized servers, may show higher speeds
- **Cloudflare**: Tests against global CDN, more representative of real-world performance
- **Different methodologies**: Each provider uses different testing algorithms



## Build and Deploy

### Building the Application

```powershell
# Build in Release configuration
dotnet build -c Release

# Or publish as self-contained executable
dotnet publish -c Release -r win-x64 --self-contained
```

### Deploying to Production

The application should be deployed to: `C:\ScheduledTasks\InternetSpeedTest`

```powershell
# Publish to a specific output directory
dotnet publish InternetSpeedTest.csproj -c Release -o C:\ScheduledTasks\InternetSpeedTest
```

### Post-Deployment Steps

1. **Configure Task Scheduler**:
   - Create a scheduled task to run `InternetSpeedTest.exe` hourly
   - Set working directory to: `C:\ScheduledTasks\InternetSpeedTest`
   - Run with appropriate user privileges

2. **Verify Configuration**:
   - Ensure `appsettings.json` has correct database connection string
   - Configure speed test provider (Ookla or Cloudflare)
   - Set up email settings for daily reports

3. **Test Deployment**:
   ```powershell
   # Test manual execution
   C:\ScheduledTasks\InternetSpeedTest\InternetSpeedTest.exe
   ```

## Dependencies

- **.NET 9.0**: Runtime environment
- **SQL Server**: Database storage (LocalDB supported)
- **Ookla Speedtest CLI**: Optional, for Ookla testing
- **Internet Connection**: Required for both test providers

# WARP.md

This file provides guidance to WARP (warp.dev) when working with code in this repository.

## Project Overview

InternetSpeedTest is a .NET 9.0 console application that provides automated internet speed testing via two providers:
1. **Ookla Speedtest CLI**: Traditional speedtest.exe with ISP-optimized server selection
2. **Cloudflare Speed Test**: Advanced HTTP-based testing with browser-level performance optimizations

The application runs periodically via Task Scheduler to monitor internet connection performance, processes results, and stores them in a SQL Server database for analysis. It also provides daily email reports and PC health monitoring.

## Common Commands

### Build and Run
```powershell
# Build the project
dotnet build

# Run the application
dotnet run

# Build for release
dotnet build --configuration Release

# Run with specific configuration
dotnet run --configuration Release
```

### Database Operations
```powershell
# Add Entity Framework migration
dotnet ef migrations add <MigrationName>

# Update database
dotnet ef database update

# Drop database (be careful!)
dotnet ef database drop
```

### Development
```powershell
# Run with hot reload during development
dotnet watch run

# Clean and rebuild
dotnet clean && dotnet build

# Pack for distribution
dotnet pack
```

### Testing Speed Test Providers
```powershell
# Test with Ookla (set UseCloudflare: false in appsettings.json)
dotnet run

# Test with Cloudflare (set UseCloudflare: true in appsettings.json)
dotnet run

# Test with custom Cloudflare settings for high-speed connections
# Modify appsettings.json: ParallelConnections: 6, TestDurationSeconds: 15
dotnet run
```

## Architecture

### Core Components

- **Program.cs**: Entry point with dependency injection setup, configures two DbContexts (PopsContext and Emailer)
- **IInternetSpeedTestService**: Service interface for running speed tests
- **InternetSpeedTestService**: Main orchestration service that delegates to appropriate speed test provider
- **CloudflareSpeedTestService**: Advanced HTTP-based speed testing with parallel connections and HTTP/2 optimization

### Data Layer

- **PopsContext**: Entity Framework DbContext for the main internet speed data
- **InternetSpeed**: Main entity representing a speed test result
- **InternetSpeedJSON**: Nested classes for deserializing Ookla speedtest JSON output
- **InternetSpeedDto**: DTO for data transfer operations

### Configuration

- **Connection Strings**: Supports multiple database targets (connLocal, Emailer)
- **SpeedTest Provider Selection**: Choose between Ookla (`UseCloudflare: false`) or Cloudflare (`UseCloudflare: true`)
- **Ookla Settings**: Configurable executable path and arguments via `appsettings.json`
- **Cloudflare Settings**: Tunable performance parameters (ParallelConnections, TestDurationSeconds, WarmupDurationSeconds)
- **User Secrets**: Uses .NET user secrets for sensitive configuration data

### Key Features

#### Speed Testing
- **Dual Provider Support**: Ookla (ISP-optimized) and Cloudflare (CDN-based) speed testing
- **Browser-Level Performance**: Cloudflare tests use parallel connections, HTTP/2, and connection warmup
- **Configurable Optimization**: Tune parallel connections, test duration, and warmup time
- **Streaming Data Transfer**: Efficient memory usage and accurate throughput measurement

#### System Features
- **Daily Email Reports**: Automated speed test summaries and PC health checks
- **Drive Space Monitoring**: Daily reports on local drive capacity and free space
- **Comprehensive Logging**: Serilog with file and console outputs, detailed performance metrics
- **Process Management**: Robust async process execution with cancellation token support
- **Error Handling**: Comprehensive error handling for both HTTP and process-based testing

## Configuration Notes

### Speed Test Providers
- **Ookla (default)**: `speedtest.exe --accept-license --accept-gdpr --format=json`
- **Cloudflare**: HTTP-based testing with configurable parameters:
  - `ParallelConnections: 4` (default) - adjust for connection speed
  - `TestDurationSeconds: 10` (default) - longer = more stable results
  - `WarmupDurationSeconds: 2` (default) - connection establishment time

### System Configuration
- Logs are written to `C:\logs\InternetSpeedTest\` with daily rolling and size limits
- Database schema expects SQL Server with specific column types and constraints
- The application is designed to run as a scheduled task every hour
- Daily tasks include speed test summaries and PC health monitoring

## Dependencies

- .NET 9.0 runtime
- Entity Framework Core 9.0 with SQL Server provider
- Serilog for structured logging
- System.Text.Json for JSON processing
- System.Net.Http with SocketsHttpHandler for HTTP/2 optimization
- Ookla speedtest.exe (external dependency, optional - only needed for Ookla testing)

## Database Schema

The `InternetSpeed` table captures:
- Bandwidth measurements (download/upload)
- Ping metrics (jitter, latency, low/high)
- Result metadata (URL, timestamp)
- Raw JSON output for full data preservation

## Troubleshooting

### Cloudflare Speed Test Issues

**Lower speeds than expected**:
- Increase `ParallelConnections` (try 6-8 for >500 Mbps connections)
- Extend `TestDurationSeconds` to 15-20 for more stable results
- Check CPU usage during testing (parallel connections are CPU-intensive)
- Verify IPv6 connectivity and check logs for IP addresses used

**Connection failures**:
- Check firewall settings for HTTPS (443) outbound connections
- Verify DNS resolution for `speed.cloudflare.com`
- Reduce `ParallelConnections` if seeing timeout errors

### Ookla Speed Test Issues

**Executable not found**:
- Download `speedtest.exe` from Ookla and place in application directory
- Update `SpeedTest:Executable` path in `appsettings.json`

**Permission issues**:
- Run application as administrator if speedtest.exe fails to execute
- Check that speedtest.exe has execute permissions

### Database Issues

**Connection failures**:
- Verify SQL Server is running and accessible
- Check connection strings in `appsettings.json`
- Ensure database exists or run `dotnet ef database update`

**Schema errors**:
- Run `dotnet ef migrations add InitialCreate` if no migrations exist
- Use `dotnet ef database update` to apply pending migrations

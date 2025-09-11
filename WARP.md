# WARP.md

This file provides guidance to WARP (warp.dev) when working with code in this repository.

## Project Overview

InternetSpeedTest is a .NET 9.0 console application that runs Ookla's `speedtest.exe` periodically via Task Scheduler to monitor internet connection performance. It processes the JSON output and stores results in a SQL Server database for analysis.

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

## Architecture

### Core Components

- **Program.cs**: Entry point with dependency injection setup, configures two DbContexts (PopsContext and Emailer)
- **IInternetSpeedTestService**: Service interface for running speed tests
- **InternetSpeedTestService**: Main service that executes speedtest.exe, processes JSON output, and persists results

### Data Layer

- **PopsContext**: Entity Framework DbContext for the main internet speed data
- **InternetSpeed**: Main entity representing a speed test result
- **InternetSpeedJSON**: Nested classes for deserializing Ookla speedtest JSON output
- **InternetSpeedDto**: DTO for data transfer operations

### Configuration

- **Connection Strings**: Supports multiple database targets (connLocal, connSnowBall, connWillbot, optional Emailer)
- **SpeedTest Settings**: Configurable executable path and arguments via `appsettings.json`
- **User Secrets**: Uses .NET user secrets for sensitive configuration data

### Key Features

- **Bandwidth Correction Logic**: Automatically adjusts bandwidth values if they appear to be in wrong units (8-digit correction)
- **Comprehensive Logging**: Serilog with file and console outputs, structured logging
- **Process Management**: Robust async process execution with cancellation token support
- **Error Handling**: Comprehensive error handling for process execution and JSON deserialization

## Configuration Notes

- Default speedtest command: `speedtest.exe --accept-license --accept-gdpr --format=json`
- Logs are written to `C:\logs\InternetSpeedTest\` with daily rolling and size limits
- Database schema expects SQL Server with specific column types and constraints
- The application is designed to run as a scheduled task every hour

## Dependencies

- .NET 9.0 runtime
- Entity Framework Core 9.0 with SQL Server provider
- Serilog for structured logging
- System.Text.Json for JSON processing
- Ookla speedtest.exe (external dependency)

## Database Schema

The `InternetSpeed` table captures:
- Bandwidth measurements (download/upload)
- Ping metrics (jitter, latency, low/high)
- Result metadata (URL, timestamp)
- Raw JSON output for full data preservation

$content = Get-Content "C:\Users\alann\OneDrive\Repos\InternetSpeedTest\README.md" -Raw

$buildSection = @"


## Build and Deploy

### Building the Application

``````powershell
# Build in Release configuration
dotnet build -c Release

# Or publish as self-contained executable
dotnet publish -c Release -r win-x64 --self-contained
``````

### Deploying to Production

The application should be deployed to: ``C:\ScheduledTasks\InternetSpeedTest``

``````powershell
# Publish to the deployment folder
dotnet publish -c Release -o C:\ScheduledTasks\InternetSpeedTest

# Or for self-contained deployment (includes .NET runtime)
dotnet publish -c Release -r win-x64 --self-contained -o C:\ScheduledTasks\InternetSpeedTest
``````

### Post-Deployment Steps

1. **Configure Task Scheduler**:
   - Create a scheduled task to run ``InternetSpeedTest.exe`` hourly
   - Set working directory to: ``C:\ScheduledTasks\InternetSpeedTest``
   - Run with appropriate user privileges

2. **Verify Configuration**:
   - Ensure ``appsettings.json`` has correct database connection string
   - Configure speed test provider (Ookla or Cloudflare)
   - Set up email settings for daily reports

3. **Test Deployment**:
   ``````powershell
   # Test manual execution
   C:\ScheduledTasks\InternetSpeedTest\InternetSpeedTest.exe
   ``````

"@

$newContent = $content -replace '## Dependencies', ($buildSection + "`n## Dependencies")
Set-Content "C:\Users\alann\OneDrive\Repos\InternetSpeedTest\README.md" -Value $newContent -NoNewline

Write-Host "README.md updated successfully with Build and Deploy section!"

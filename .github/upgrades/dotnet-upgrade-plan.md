# .NET 10 Upgrade Plan

## Execution Steps

Execute steps below sequentially one by one in the order they are listed.

1. Validate that a .NET 10 SDK required for this upgrade is installed on the machine and if not, help to get it installed.
2. Ensure that the SDK version specified in global.json files is compatible with the .NET 10 upgrade.
3. Upgrade EmailerUtility\EmailerUtility.csproj to .NET 10
4. Upgrade InternetSpeedTest.csproj to .NET 10

## Settings

This section contains settings and data used by execution steps.

### Aggregate NuGet packages modifications across all projects

NuGet packages used across all selected projects or their dependencies that need version update in projects that reference them.

| Package Name                                      | Current Version | New Version                | Description                                   |
|:--------------------------------------------------|:---------------:|:--------------------------:|:----------------------------------------------|
| Microsoft.EntityFrameworkCore                     | 9.0.9           | 10.0.0-rc.2.25502.107      | Recommended for .NET 10                       |
| Microsoft.EntityFrameworkCore.SqlServer           | 9.0.9           | 10.0.0-rc.2.25502.107      | Recommended for .NET 10                       |
| Microsoft.EntityFrameworkCore.Tools               | 9.0.9           | 10.0.0-rc.2.25502.107      | Recommended for .NET 10                       |
| Microsoft.Extensions.Configuration.UserSecrets    | 9.0.9           | 10.0.0-rc.2.25502.107      | Recommended for .NET 10                       |
| Microsoft.Extensions.Hosting                      | 9.0.9           | 10.0.0-rc.2.25502.107      | Recommended for .NET 10                       |

### Project upgrade details

This section contains details about each project upgrade and modifications that need to be done in the project.

#### EmailerUtility\EmailerUtility.csproj modifications

Project properties changes:
  - Target framework should be changed from `net9.0` to `net10.0`

#### InternetSpeedTest.csproj modifications

Project properties changes:
  - Target framework should be changed from `net9.0` to `net10.0`

NuGet packages changes:
  - Microsoft.EntityFrameworkCore should be updated from `9.0.9` to `10.0.0-rc.2.25502.107` (*recommended for .NET 10*)
  - Microsoft.EntityFrameworkCore.SqlServer should be updated from `9.0.9` to `10.0.0-rc.2.25502.107` (*recommended for .NET 10*)
  - Microsoft.EntityFrameworkCore.Tools should be updated from `9.0.9` to `10.0.0-rc.2.25502.107` (*recommended for .NET 10*)
  - Microsoft.Extensions.Configuration.UserSecrets should be updated from `9.0.9` to `10.0.0-rc.2.25502.107` (*recommended for .NET 10*)
  - Microsoft.Extensions.Hosting should be updated from `9.0.9` to `10.0.0-rc.2.25502.107` (*recommended for .NET 10*)

# .NET 10 Upgrade Report

## Project Target Framework Modifications

| Project name                                                                | Old Target Framework | New Target Framework | Commits           |
|:----------------------------------------------------------------------------|:--------------------:|:--------------------:|-------------------|
| EmailerUtility\EmailerUtility.csproj                                        | net9.0               | net10.0              | f43ade2d          |
| InternetSpeedTest.csproj                                                    | net9.0               | net10.0              | 5d766c66          |

## NuGet Packages

| Package Name                                      | Old Version | New Version                | Commit ID                                 |
|:--------------------------------------------------|:-----------:|:--------------------------:|-------------------------------------------|
| Microsoft.EntityFrameworkCore                     | 9.0.9       | 10.0.0-rc.2.25502.107      | 21147f05                                  |
| Microsoft.EntityFrameworkCore.SqlServer           | 9.0.9       | 10.0.0-rc.2.25502.107      | 21147f05                                  |
| Microsoft.EntityFrameworkCore.Tools               | 9.0.9       | 10.0.0-rc.2.25502.107      | 21147f05                                  |
| Microsoft.Extensions.Configuration.UserSecrets    | 9.0.9       | 10.0.0-rc.2.25502.107      | 21147f05                                  |
| Microsoft.Extensions.Hosting                      | 9.0.9       | 10.0.0-rc.2.25502.107      | 21147f05                                  |
| Microsoft.Build.Tasks.Core                        | -           | 17.14.28                   | e075fbc2                                  |
| Microsoft.Build.Utilities.Core                    | -           | 17.14.28                   | e075fbc2                                  |

## All Commits

| Commit ID | Description                                                                                      |
|:----------|:-------------------------------------------------------------------------------------------------|
| 25f14014  | Commit upgrade plan                                                                              |
| f43ade2d  | Update target framework to net10.0 in EmailerUtility.csproj                                      |
| e075fbc2  | Add MSBuild Core packages to InternetSpeedTest.csproj                                            |
| 5d766c66  | Update InternetSpeedTest.csproj to target .NET 10.0                                              |
| 21147f05  | Update package versions in InternetSpeedTest.csproj                                              |

## Summary

Successfully upgraded both projects from .NET 9.0 to .NET 10.0 (Preview - RC2). The upgrade included:

- Updated target framework for 2 projects
- Upgraded 5 Entity Framework Core and Microsoft Extensions packages to version 10.0.0-rc.2.25502.107
- Added 2 MSBuild packages (Tasks.Core and Utilities.Core) at version 17.14.28 for build support

All projects validated successfully with no breaking changes or compilation errors.

## Next Steps

- **Test thoroughly**: Since .NET 10 is in preview (RC2), test all functionality to ensure compatibility
- **Monitor for updates**: Watch for newer preview releases and the final .NET 10 release
- **Review breaking changes**: Check the [.NET 10 breaking changes documentation](https://learn.microsoft.com/en-us/dotnet/core/compatibility/10.0) for any relevant changes
- **Consider rollback plan**: Keep the `dev` branch stable in case you need to revert

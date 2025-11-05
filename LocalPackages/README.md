# LocalPackages

This directory contains local NuGet packages that are not published to NuGet.org.

## EmailerUtility Package

Place the EmailerUtility NuGet package (.nupkg) file in this directory.

To create the EmailerUtility package from the project:
1. Navigate to the EmailerUtility project directory
2. Run: `dotnet pack -c Release -o ../LocalPackages`

The package will be automatically discovered by NuGet restore due to the configuration in `NuGet.Config`.

## Package Versioning

When updating the EmailerUtility package:
1. Increment the version in EmailerUtility.csproj
2. Run `dotnet pack` again
3. Update the version in InternetSpeedTest.csproj if needed (or use `Version="*"` for latest)
4. Run `dotnet restore` in the solution directory

## Note

The `NuGet.Config` file at the solution root configures this directory as a package source.

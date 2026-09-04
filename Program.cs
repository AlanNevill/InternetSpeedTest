using EmailerUtility.DependencyInjection;

using InternetSpeedTest; // For IInternetSpeedTestService interface
using InternetSpeedTest.DataModels;
using InternetSpeedTest.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Serilog;
using Serilog.Settings.Configuration;

using System; // For Exception / InvalidOperationException
using System.Reflection;

// Bootstrap host with Serilog integrated so ILogger<T> routes to Serilog sinks defined in appsettings.json
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console() // early console
    .CreateLogger();

try
{
    // Serilog.Settings.Configuration normally discovers sink assemblies via DependencyContext, which does
    // not exist in a single-file publish. Name them explicitly so the Serilog section in appsettings.json
    // resolves identically in both publish modes. A new sink package must be added here too.
    var serilogReaderOptions = new ConfigurationReaderOptions(
        typeof( ConsoleLoggerConfigurationExtensions ).Assembly,
        typeof( FileLoggerConfigurationExtensions ).Assembly );

    var host = Host.CreateDefaultBuilder( args )
        // Anchor the content root to the exe directory so CreateDefaultBuilder's JSON providers find
        // appsettings.json regardless of the working directory (e.g. a scheduled task with no "Start in").
        // Re-adding the JSON file in ConfigureAppConfiguration would land *after* the environment-variable
        // and command-line providers, silently overriding both; setting the content root keeps the standard
        // precedence: appsettings.json -> appsettings.{Environment}.json -> env vars -> command line.
        .UseContentRoot( AppContext.BaseDirectory )
        .UseSerilog( (ctx, services, loggerConfig) =>
        {
            loggerConfig
                .ReadFrom.Configuration( ctx.Configuration, serilogReaderOptions )
                .ReadFrom.Services( services )
                .Enrich.FromLogContext();
        } )
        .ConfigureServices( (ctx, services) =>
        {
            var config = ctx.Configuration;

            var connectionString = config.GetConnectionString( "connLocal" );
            ArgumentNullException.ThrowIfNullOrWhiteSpace( connectionString, nameof( connectionString ) );

            // EmailerUtility reads ConnectionStrings:Emailer itself; validate here so a missing value fails at startup, not mid-send.
            var emailerConnectionString = config.GetConnectionString( "Emailer" );
            ArgumentNullException.ThrowIfNullOrWhiteSpace( emailerConnectionString, nameof( emailerConnectionString ) );

            services.AddDbContextFactory<PopsContext>( options =>
                options.UseSqlServer( connectionString, sqlOptions =>
                    sqlOptions.CommandTimeout( 120 ) ) );

            // Register TimeProvider for better testability
            services.AddSingleton( TimeProvider.System );

            services.AddScoped<CloudflareSpeedTestService>();
            services.AddScoped<IInternetSpeedTestService, InternetSpeedTestService>();

            // EmailerUtility registration (uses ConnectionStrings:Emailer automatically)
            services.AddEmailerUtility( ctx.Configuration );
            // Explicit registration for EmailerClient if not added by extension
            services.AddTransient<EmailerUtility.EmailerClient>();
        } )
        .Build();

    // Display version information AFTER Serilog is fully configured (writes to configured sinks)
    var assembly = Assembly.GetExecutingAssembly();
    var assemblyVersion = assembly.GetName().Version;
    var fileVersion = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
    var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
    // Assembly.Location is an empty string inside a single-file bundle; ProcessPath is the exe in both
    // publish modes. GetLastWriteTime (not GetCreationTime) because Windows preserves the original
    // creation timestamp when publish overwrites the file.
    var exePath = Environment.ProcessPath;
    var buildDate = !string.IsNullOrEmpty( exePath ) && System.IO.File.Exists( exePath )
        ? System.IO.File.GetLastWriteTime( exePath )
        : (DateTime?)null;
    var packageVersion = informationalVersion?.Split( '+' )[0]; // Extract version before '+' metadata

    using var _ = HelperLib.BeginMethodScope("Program");

    Log.Information( "InternetSpeedTest Starting" );
    //Log.Information( "Assembly Version: {AssemblyVersion}", assemblyVersion?.ToString() ?? "Unknown" );
    //Log.Information( "File Version: {FileVersion}", fileVersion ?? "Unknown" );
    Log.Information( "Package Version: {PackageVersion}", packageVersion ?? informationalVersion ?? "Unknown" );
    Log.Information( "Build Date: {BuildDate}", buildDate?.ToString( "yyyy-MM-dd HH:mm:ss" ) ?? "Unknown" );
    Log.Information( "=========================================================" );

    // Run the service functions
    using var scope = host.Services.CreateScope();
    var svc = scope.ServiceProvider.GetRequiredService<IInternetSpeedTestService>();
    await svc.RunAsync();
    await svc.RunDailyIfNeededAsync();
}
catch ( Exception ex )
{
    Log.Fatal( ex, "Unhandled exception" );
    throw;
}
finally
{
    Log.Information( "Shutting down\n" );
    Log.CloseAndFlush();
}

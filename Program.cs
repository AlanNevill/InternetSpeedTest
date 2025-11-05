using EmailerUtility.DependencyInjection;

using InternetSpeedTest; // For IInternetSpeedTestService interface
using InternetSpeedTest.DataModels;
using InternetSpeedTest.DataModels.Emailer;
using InternetSpeedTest.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Serilog;

using System; // For Exception / InvalidOperationException
using System.Reflection;

// Bootstrap host with Serilog integrated so ILogger<T> routes to Serilog sinks defined in appsettings.json
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console() // early console
    .CreateLogger();

try
{
    var host = Host.CreateDefaultBuilder( args )
        .UseSerilog( (ctx, services, loggerConfig) =>
        {
            loggerConfig
                .ReadFrom.Configuration( ctx.Configuration )
                .ReadFrom.Services( services )
                .Enrich.FromLogContext();
        } )
        .ConfigureServices( (ctx, services) =>
        {
            var config = ctx.Configuration;

            var connectionString = config.GetConnectionString( "connLocal" );
            ArgumentNullException.ThrowIfNullOrWhiteSpace( connectionString, nameof( connectionString ) );
            
            var emailerConnectionString = config.GetConnectionString( "Emailer" );
            ArgumentNullException.ThrowIfNullOrWhiteSpace( emailerConnectionString, nameof( emailerConnectionString ) );

            services.AddDbContextFactory<PopsContext>( options => options.UseSqlServer( connectionString ) );
            services.AddDbContextFactory<Emailer>( options => options.UseSqlServer( emailerConnectionString ) );

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
    var buildDate = System.IO.File.GetCreationTime( assembly.Location );

    Log.Information("InternetSpeedTest Starting");
    Log.Information("Assembly Version: {AssemblyVersion}", assemblyVersion?.ToString() ?? "Unknown");
    Log.Information("File Version: {FileVersion}", fileVersion ?? "Unknown");
    Log.Information("Package Version: {PackageVersion}", informationalVersion ?? "Unknown");
    Log.Information("Build Date: {BuildDate:yyyy-MM-dd HH:mm:ss}", buildDate);
    Log.Information("==========================================");

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

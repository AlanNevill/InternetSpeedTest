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

            var connectionString = config.GetConnectionString( "connLocal" )
                ?? throw new InvalidOperationException( "Connection string 'connLocal' is not configured." );
            var emailerConnectionString = config.GetConnectionString( "Emailer" )
                ?? throw new InvalidOperationException( "Connection string 'Emailer' is not configured." );

            services.AddDbContextFactory<PopsContext>( options => options.UseSqlServer( connectionString ) );
            services.AddDbContextFactory<Emailer>( options => options.UseSqlServer( emailerConnectionString ) );

            services.AddScoped<CloudflareSpeedTestService>();
            services.AddScoped<IInternetSpeedTestService, InternetSpeedTestService>();
        } )
        .Build();

    // Display version information AFTER Serilog is fully configured (writes to configured sinks)
    var assembly = Assembly.GetExecutingAssembly();
    var version = assembly.GetName().Version;
    var fileVersion = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;

    // Include build date in yyMMdd format alongside file version
    var buildDate = System.IO.File.GetLastWriteTime( assembly.Location );
    var fileVersionWithDate = fileVersion is not null ? $"{fileVersion} ({buildDate:yy-MM-dd})" : "Unknown";

    Log.Information("InternetSpeedTest Starting");
    Log.Information("Version: {Version}", version?.ToString() ?? "Unknown");
    Log.Information("File Version: {FileVersion}", fileVersionWithDate);
    Log.Information("======================================");

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

using InternetSpeedTest;
using InternetSpeedTest.DataModels;
using InternetSpeedTest.DataModels.Emailer; // Added for Emailer DbContext

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder( args );

// Database connection
var connectionString = builder.Configuration.GetConnectionString( "connLocal" );

if ( string.IsNullOrWhiteSpace( connectionString ) )
{
    throw new System.InvalidOperationException( "Connection string is not configured. Check appsettings.*.json or user secrets." );
}

// Optional separate connection string for Emailer
var emailerConnectionString = builder.Configuration.GetConnectionString( "Emailer" ) ?? null;

builder.Services.AddDbContextFactory<PopsContext>( options =>
{
    options.UseSqlServer( connectionString );
} );

builder.Services.AddDbContextFactory<Emailer>( options =>
{
    options.UseSqlServer( emailerConnectionString );
} );

builder.Services.AddScoped<IInternetSpeedTestService, InternetSpeedTestService>();

var app = builder.Build();

// Resolve and run the service
using ( var scope = app.Services.CreateScope() )
{
    var svc = scope.ServiceProvider.GetRequiredService<IInternetSpeedTestService>();
    await svc.RunAsync();
}

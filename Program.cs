using InternetSpeedTest;
using InternetSpeedTest.DataModels;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using System;

var builder = Host.CreateApplicationBuilder(args);

// Database connection selection by machine
var connectionString = builder.Configuration.GetConnectionString("connLocal");
var machine = System.Environment.MachineName?.ToUpperInvariant();
if (machine == "SNOWBALL")
{
    connectionString = builder.Configuration.GetConnectionString("connSnowball") ?? connectionString;
}
else if (machine == "WILLBOT")
{
    connectionString = builder.Configuration.GetConnectionString("connWillbot") ?? connectionString;
}

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new System.InvalidOperationException("Connection string is not configured. Check appsettings.*.json or user secrets.");
}

builder.Services.AddDbContextFactory<PopsContext>(options =>
{
    options.UseSqlServer(connectionString);
});

builder.Services.AddScoped<IInternetSpeedTestService, InternetSpeedTestService>();

var app = builder.Build();

// Resolve and run the service
using (var scope = app.Services.CreateScope())
{
    var svc = scope.ServiceProvider.GetRequiredService<IInternetSpeedTestService>();
    await svc.RunAsync();
}

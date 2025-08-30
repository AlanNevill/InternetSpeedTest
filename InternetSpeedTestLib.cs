using InternetSpeedTest.DataModels;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

using Serilog;

using System;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace InternetSpeedTest;

internal static class InternetSpeedTestLib
{
    public static string? _cnStr = string.Empty;

    /// <summary>
    /// Build the configuration and logger
    /// </summary>
    internal static void BuildConfig()
    {
        var config = new ConfigurationBuilder()
            .SetBasePath( Path.GetDirectoryName( Assembly.GetEntryAssembly()!.Location )! )
            .AddJsonFile( "appsettings.json", optional: false, reloadOnChange: true )
            .AddJsonFile( $"appsettings.{Environment.GetEnvironmentVariable( "ASPNETCORE_ENVIRONMENT" ) ?? "Production"}.json", optional: true )
            .AddEnvironmentVariables()
            .AddUserSecrets( Assembly.GetExecutingAssembly(), optional: true )
            .Build();

        // serilog configuration
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration( config )
            .CreateLogger();

        // Ensure the log prominently shows the database and server being used
        Log.Information( $"""
        {new String( '-', 130 )}
                                InternetSpeedTest: v{System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}
                                COMPUTERNAME:      {Environment.GetEnvironmentVariable( "COMPUTERNAME" )}
                                _cnStr:            [configured via DI]
        {new String( '-', 130 )}
        """ );
    }


    /// <summary>
    /// Run the speed test and return the output
    /// </summary>
    /// <param name="strCommand"></param>
    /// <param name="strCommandParameters"></param>
    /// <returns></returns>
    internal static string SpeedTest(string strCommand, string strCommandParameters)
    {
        throw new NotSupportedException("Use IInternetSpeedTestService via DI instead of InternetSpeedTestLib.SpeedTest");
    }

    /// <summary>
    /// Deserialize the output and save into database table
    /// </summary>
    /// <param name="strOutput">string</param>
    internal static void ProcessResult(string strOutput)
    {
        throw new NotSupportedException("Use IInternetSpeedTestService via DI instead of InternetSpeedTestLib.ProcessResult");
    }
}


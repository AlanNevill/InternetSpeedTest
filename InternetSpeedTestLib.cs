using InternetSpeedCheck;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;
using InternetSpeedTest.DataModels;
using System.Text.Json;
using System.Reflection;

namespace InternetSpeedTest;

internal static class InternetSpeedTestLib
{
    public static string _cnStr = string.Empty;

    /// <summary>
    /// Build the configuration and logger
    /// </summary>
    internal static void BuildConfig()
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location))
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
            .AddEnvironmentVariables()
            .AddUserSecrets<Program>()
            .Build();

        // get the database connection string from the appsettings.json file
        _cnStr = Environment.GetEnvironmentVariable("COMPUTERNAME") == "SNOWBALL" ? config.GetConnectionString("connSnowball") : config.GetConnectionString("connWillbot");

        // serilog configuration
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(config)
            .CreateLogger();

        // Ensure the log prominently shows the database and server being used
        Log.Information($"""

        {new String('-', 130)}
                                InternetSpeedTest v{System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}
                                Using: { _cnStr}
        {new String('-', 130)}
        """);
    }


    internal static string SpeedTest(string strCommand, string strCommandParameters)
    {
        string? strOutput, strError;

        Log.Information($"""
            InternetSpeedTestLib/SpeedTest paramters:
                strCommand:                                                  {strCommand}
                strCommandParameters:                                        {strCommandParameters}
                Path.GetDirectoryName(Assembly.GetEntryAssembly().Location): {Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}

            """);

        //Create process
        System.Diagnostics.Process pProcess = new System.Diagnostics.Process();

        //strCommand is path and file name of command to run
        pProcess.StartInfo.FileName = strCommand;

        //strCommandParameters are parameters to pass to program
        pProcess.StartInfo.Arguments = strCommandParameters;

        pProcess.StartInfo.UseShellExecute = false;

        //Set output of program to be written to process output stream
        pProcess.StartInfo.RedirectStandardOutput = true;
        pProcess.StartInfo.RedirectStandardError = true;

        //Optional
        //pProcess.StartInfo.WorkingDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);

        // eliminate the process window
        pProcess.StartInfo.CreateNoWindow = true;

        try
        {
           //Start the process
            pProcess.Start();

            //Get program output
            strOutput = pProcess.StandardOutput.ReadToEnd();
            strError  = pProcess.StandardError.ReadToEnd();

            //Wait for process to finish
            pProcess.WaitForExit();

        }
        catch (Exception e)
        {
            Log.Error($"InternetSpeedTestLib/SpeedTest exception: {e}");
            throw;
        }

        // check exit code
        if (pProcess.ExitCode == 0) // success
        {
            Log.Information(strOutput);
            ProcessResult(strOutput);
        }
        else    // error
        {
            Log.Error($"InternetSpeedTestLib/SpeedTest pProcess.ExitCode: {pProcess.ExitCode}");

            if (!string.IsNullOrEmpty(strError))
            {
                Log.Error($"InternetSpeedTestLib/SpeedTest strError: {strError}");
            }

            if (strOutput.Length != 0)
            {
                Log.Error($"InternetSpeedTestLib/SpeedTest strOutput: {strOutput}");
            }
        }

        return strOutput;
    }

    internal static void ProcessResult(string strOutput)
    {
        InternetSpeedJSON.Root? myDeserializedClass = JsonSerializer.Deserialize<InternetSpeedJSON.Root>(strOutput);

        //InternetSpeedDto? internetSpeedDto = JsonSerializer.Deserialize<InternetSpeedDto>(strOutput);

        Log.Information($"""
            InternetSpeedTestLib/ProcessResult:
                myDeserializedClass.Result.Url:                 {myDeserializedClass!.Result.Url}
                myDeserializedClass.Timestamp:                  {myDeserializedClass.Timestamp.ToLocalTime()}
                myDeserializedClass.Ping.Jitter:                {myDeserializedClass.Ping.Jitter}
                myDeserializedClass.Ping.Latency:               {myDeserializedClass.Ping.Latency}
                myDeserializedClass.Ping.High:                  {myDeserializedClass.Ping.High}
                myDeserializedClass.Ping.Low:                   {myDeserializedClass.Ping.Low}
                myDeserializedClass.Download.Bandwidth (bytes): {myDeserializedClass.Download.Bandwidth}
                myDeserializedClass.Upload.Bandwidth (bytes):   {myDeserializedClass.Upload.Bandwidth}
                {new String('=', 130)}

            """);


        using (var context = new PopsContext())
        {
            InternetSpeed internetSpeed = new ()
            {
                ResultUrl           = myDeserializedClass.Result.Url,
                DownLoadBandwidth   = myDeserializedClass.Download.Bandwidth,
                UploadBandWidth     = myDeserializedClass.Upload.Bandwidth,
                ResultDateTime      = myDeserializedClass.Timestamp.ToLocalTime(),
                PingJitter          = myDeserializedClass.Ping.Jitter,
                PingLatency         = myDeserializedClass.Ping.Latency,
                PingHigh            = myDeserializedClass.Ping.High,
                PingLow             = myDeserializedClass.Ping.Low,
                ResultJson          = strOutput
            };
            context.InternetSpeed.Add(internetSpeed);
            context.SaveChanges();
        }
    }
}


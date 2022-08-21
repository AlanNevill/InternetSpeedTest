﻿using InternetSpeedCheck;
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

namespace InternetSpeedTest;

internal static class InternetSpeedTestLib
{
    private static string _cnStr = string.Empty;

    /// <summary>
    /// Build the configuration and logger
    /// </summary>
    internal static void BuildConfig()
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
            .AddEnvironmentVariables()
            .AddUserSecrets<Program>()
            .Build();

        _cnStr = config.GetConnectionString("myTest");

        // dependency injection
        //var host = Host.CreateDefaultBuilder()
        //    .ConfigureServices((context, services) =>
        //    {
        //        services.AddDbContext<Finance_TestContext>(options =>
        //        {
        //            options.UseSqlServer(_cnStr);
        //        });
        //    })
        //    .UseSerilog()
        //    .Build();

        // serilog configuration
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(config)
            .CreateLogger();

        // Ensure the log prominently shows the database and server being used
        //var CnStr = new SqlConnectionStringBuilder(_cnStr);
        Serilog.Log.Information(new String('-', 50));
        Serilog.Log.Information($"InternetSpeedTest starting using DATABASE: {_cnStr}");
        Serilog.Log.Information(new String('-', 50));
    }

    internal static string SpeedTest(string strCommand, string strCommandParameters, string strWorkingDirectory)
    {
        string? strOutput, strError;

        Log.Information($"""
            InternetSpeedTestLib/SpeedTest starting:
                strCommand: {strCommand}
                strCommandParameters: {strCommandParameters}
                strWorkingDirectory: {strWorkingDirectory}
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
        pProcess.StartInfo.WorkingDirectory = strWorkingDirectory;

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
        if (pProcess.ExitCode == 0)
        {
            Log.Information(strOutput);
            ProcessResult(strOutput);
        }
        else
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
                myDeserializedClass.Result.Id:              {myDeserializedClass.Result.Id}
                myDeserializedClass.Timestamp:              {myDeserializedClass.Timestamp}
                myDeserializedClass.Ping.Jitter:            {myDeserializedClass.Ping.Jitter}
                myDeserializedClass.Ping.Low:               {myDeserializedClass.Ping.Low}
                myDeserializedClass.Ping.High:              {myDeserializedClass.Ping.High}
                myDeserializedClass.Download.Bandwidth:     {myDeserializedClass.Download.Bandwidth}
                myDeserializedClass.Upload.Bandwidth:       {myDeserializedClass.Upload.Bandwidth}
            """);

    }
}


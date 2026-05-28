using InternetSpeedTest.DataModels;
using InternetSpeedTest.DataModels.Emailer;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace InternetSpeedTest.Services;

internal sealed class InternetSpeedTestService(
    IDbContextFactory<PopsContext> popsContextFactory,
    ILogger<InternetSpeedTestService> logger,
    IConfiguration configuration,
    CloudflareSpeedTestService cloudflareService,
    EmailerUtility.EmailerClient _emailerClient,
    TimeProvider timeProvider)
    : IInternetSpeedTestService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly string _dailyStatePath = ResolveDailyStatePath( configuration );

    public async Task<string> RunAsync(CancellationToken cancellationToken = default)
    {
        using var _ = HelperLib.BeginMethodScope();

        // Check if we should use Cloudflare instead of Ookla
        var useCloudflare = configuration.GetValue<bool>( "SpeedTest:UseCloudflare", false );

        string output;
        if ( useCloudflare )
        {
            logger.LogInformation( "Running Cloudflare speed test" );
            var cloudflareResult = await cloudflareService.RunSpeedTestAsync( cancellationToken );
            output = cloudflareResult.ToOoklaCompatibleJson();
        }
        else
        {
            // Resolve command and args from configuration with sane defaults
            var exe = configuration["SpeedTest:Executable"] ?? "speedtest.exe";
            var args = configuration["SpeedTest:Arguments"] ?? "--accept-license --accept-gdpr --format=json";

            logger.LogInformation( "Running speed test: {Exe} {Args}", exe, args );
            output = await RunProcessAsync( exe, args, cancellationToken );
        }

        await PersistAsync( output, cancellationToken );

        return output;
    }

    public async Task<bool> RunDailyIfNeededAsync(CancellationToken cancellationToken = default)
    {
        using var _ = HelperLib.BeginMethodScope();

        // Check if already run today - compare in local time so midnight-local runs
        // (which are still the previous UTC date) correctly start a new day.
        var today = timeProvider.GetLocalNow().Date;
        DailyState state;

        try
        {
            state = await LoadDailyStateAsync( cancellationToken ) ?? new DailyState();
        }
        catch ( Exception ex )
        {
            logger.LogWarning( ex, "Failed to load daily state; proceeding as if never run." );
            state = new DailyState();
        }

        if ( state.LastDailyRunUtc?.ToLocalTime().Date == today )
        {
            // Already ran today
            logger.LogInformation( "Daily tasks have already been run today." );

            return false;
        }

        // Perform daily tasks (placeholder for now)
        await DoDailyTasksAsync( cancellationToken );

        // Update state and persist to json file
        state.LastDailyRunUtc = timeProvider.GetUtcNow().DateTime;
        try
        {
            await SaveDailyStateAsync( state, cancellationToken );
        }
        catch ( Exception ex )
        {
            logger.LogWarning( ex, "Failed to persist daily state to {Path}.", _dailyStatePath );
        }

        return true;
    }

    private static async Task<string> RunProcessAsync(string fileName, string arguments, CancellationToken ct)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = AppContext.BaseDirectory,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            },
            EnableRaisingEvents = true
        };

        if ( !process.Start() )
        {
            throw new InvalidOperationException( $"Failed to start process: {fileName}" );
        }

        var stdOutTask = process.StandardOutput.ReadToEndAsync( ct );
        var stdErrTask = process.StandardError.ReadToEndAsync( ct );

        using var reg = ct.Register( () =>
        {
            try { if ( !process.HasExited ) process.Kill( true ); } catch { /* ignored */ }
        } );

        await process.WaitForExitAsync( ct );

        var stdOut = await stdOutTask;
        var stdErr = await stdErrTask;

        // Check exit code and return stdOut or throw
        return process.ExitCode != 0
            ? throw new InvalidOperationException( $"Speed test failed. ExitCode={process.ExitCode}, StdErr={stdErr}" )
            : stdOut;
    }

    /// <summary>
    /// Write the speed test result to the database
    /// </summary>
    /// <param name="json"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    private async Task PersistAsync(string json, CancellationToken ct)
    {
        if ( string.IsNullOrWhiteSpace( json ) )
        {
            logger.LogError( "Speed test produced empty output" );
            return;
        }

        InternetSpeedJSON.Root? root;
        try
        {
            root = JsonSerializer.Deserialize<InternetSpeedJSON.Root>( json, JsonOptions );
        }
        catch ( JsonException ex )
        {
            logger.LogError( ex, "Invalid JSON from speed test" );
            return;
        }

        if ( root is null )
        {
            logger.LogError( "Deserialized result is null" );
            return;
        }

        var record = new InternetSpeed
        {
            ResultUrl = root.Result.Url,
            DownLoadBandwidth = root.Download.Bandwidth,
            UploadBandWidth = root.Upload.Bandwidth,
            ResultDateTime = root.Timestamp.ToLocalTime(),
            PingJitter = root.Ping.Jitter,
            PingLatency = root.Ping.Latency,
            PingHigh = root.Ping.High,
            PingLow = root.Ping.Low,
            ResultJson = json
        };

        await using var db = await popsContextFactory.CreateDbContextAsync( ct );
        await db.internetSpeed.AddAsync( record, ct );
        await db.SaveChangesAsync( ct );
    }

    private static string ResolveDailyStatePath(IConfiguration configuration)
    {
        var overridePath = configuration["DailyRun:StatePath"];
        if ( !string.IsNullOrWhiteSpace( overridePath ) )
        {
            return overridePath!;
        }

        var programData = Environment.GetFolderPath( Environment.SpecialFolder.CommonApplicationData );
        var dir = Path.Combine( programData, "InternetSpeedTest" );
        try
        {
            Directory.CreateDirectory( dir );
        }
        catch
        {
            // Fall back to base directory if ProgramData is not writable
            dir = AppContext.BaseDirectory;
        }

        return Path.Combine( dir, "state.json" );
    }

    private sealed class DailyState
    {
        public DateTime? LastDailyRunUtc { get; set; }
    }

    private async Task<DailyState?> LoadDailyStateAsync(CancellationToken ct)
    {
        using var _ = HelperLib.BeginMethodScopeLocal( nameof( HelperLib ) );

        if ( !File.Exists( _dailyStatePath ) )
        {
            logger.LogError( "Daily state file does not exist at {Path}", _dailyStatePath );
            return null;
        }

        await using var fs = new FileStream( _dailyStatePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous );
        return await JsonSerializer.DeserializeAsync<DailyState>( fs, cancellationToken: ct );
    }

    private async Task SaveDailyStateAsync(DailyState state, CancellationToken ct)
    {
        var dir = Path.GetDirectoryName( _dailyStatePath );
        if ( !string.IsNullOrEmpty( dir ) )
        {
            Directory.CreateDirectory( dir );
        }

        await using var fs = new FileStream( _dailyStatePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous );
        await JsonSerializer.SerializeAsync( fs, state, cancellationToken: ct );
    }

    /// <summary>
    /// Do the daily tasks. 
    /// </summary>
    /// <param name="ct"></param>
    /// <returns></returns>
    private async Task DoDailyTasksAsync(CancellationToken ct)
    {
        using var _ = HelperLib.BeginMethodScope();

        // get yesterday's date using TimeProvider
        var yesterday = timeProvider.GetUtcNow().Date.AddDays( -1 );

        logger.LogInformation( "Performing daily tasks for {Date}", yesterday.ToString( "yyyy-MM-dd" ) );

        try
        {
            // Summarise results for yesterday and email
            var result = await SummariseResultsYesterday( yesterday, ct );

            // any more daily tasks can be added here

            logger.LogInformation( "Daily tasks completed at {UtcNow} UTC, EmailMessageId: {result}", timeProvider.GetUtcNow(), result );
        }
        catch ( Exception ex )
        {
            logger.LogError( ex, "Daily tasks failed for {Date}", yesterday.ToString( "yyyy-MM-dd" ) );
        }
    }

    private async Task<long> SummariseResultsYesterday(DateTime yesterday, CancellationToken ct)
    {
        // get yesterday's summary from the database view
        await using var popsDb = popsContextFactory.CreateDbContext();
        var vGigaClear4day = await popsDb.VGigaClearByDays
            .AsNoTracking()
            .FirstOrDefaultAsync( v => v.SmallDate == yesterday.ToString( "yyyy-MM-dd" ), ct );

        // format the email body as HTML
        string formattedEmail = HelperLib.FormatEmailForAcs( vGigaClear4day! );

        return await _emailerClient.EnqueueAsync(
            toAddress: "alannevill@gmail.com",
            subject: $"Daily Internet Speed Test Report for {yesterday:yyyy-MM-dd}",
            bodyHtml: formattedEmail,
            bodyText: HelperLib.HtmlToText( formattedEmail ),
            priority: 1,
           null,
           null,
           null,
           fromAddress: "NoReply@InternetSpeedTest.local",
           sourceServer: Environment.MachineName
        );
    }

}
using InternetSpeedTest.DataModels;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using System;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace InternetSpeedTest;

internal sealed class InternetSpeedTestService : IInternetSpeedTestService
{
    private readonly IDbContextFactory<PopsContext> _contextFactory;
    private readonly ILogger<InternetSpeedTestService> _logger;
    private readonly IConfiguration _configuration;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public InternetSpeedTestService(
        IDbContextFactory<PopsContext> contextFactory,
        ILogger<InternetSpeedTestService> logger,
        IConfiguration configuration)
    {
        _contextFactory = contextFactory;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<string> RunAsync(CancellationToken cancellationToken = default)
    {
        // Resolve command and args from configuration with sane defaults
        var exe = _configuration["SpeedTest:Executable"] ?? "speedtest.exe";
        var args = _configuration["SpeedTest:Arguments"] ?? "--accept-license --accept-gdpr --format=json";

        _logger.LogInformation( "Running speed test: {Exe} {Args}", exe, args );

        var output = await RunProcessAsync( exe, args, cancellationToken );

        await PersistAsync( output, cancellationToken );

        return output;
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

        if ( process.ExitCode != 0 )
        {
            throw new InvalidOperationException( $"Speed test failed. ExitCode={process.ExitCode}, StdErr={stdErr}" );
        }

        return stdOut;
    }

    private async Task PersistAsync(string json, CancellationToken ct)
    {
        if ( string.IsNullOrWhiteSpace( json ) )
        {
            _logger.LogError( "Speed test produced empty output" );
            return;
        }

        InternetSpeedJSON.Root? root;
        try
        {
            root = JsonSerializer.Deserialize<InternetSpeedJSON.Root>( json, JsonOptions );
        }
        catch ( JsonException ex )
        {
            _logger.LogError( ex, "Invalid JSON from speed test" );
            return;
        }

        if ( root is null )
        {
            _logger.LogError( "Deserialized result is null" );
            return;
        }

        try
        {
            if ( root.Download.Bandwidth is >= 10_000_000 and <= 99_999_999 )
            {
                root.Download.Bandwidth *= 10;
                _logger.LogWarning( "Download bandwidth was 8 digits; multiplied by 10" );
            }
            if ( root.Upload.Bandwidth is >= 10_000_000 and <= 99_999_999 )
            {
                root.Upload.Bandwidth *= 10;
                _logger.LogWarning( "Upload bandwidth was 8 digits; multiplied by 10" );
            }
        }
        catch ( Exception ex )
        {
            _logger.LogWarning( ex, "Bandwidth correction failed" );
        }

        var record = new DataModels.InternetSpeed
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

        await using var db = await _contextFactory.CreateDbContextAsync( ct );
        await db.internetSpeed.AddAsync( record, ct );
        await db.SaveChangesAsync( ct );
    }
}

using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace InternetSpeedTest.Services;

public class CloudflareSpeedTestService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CloudflareSpeedTestService> _logger;
    private const string BaseUrl = "https://speed.cloudflare.com";
    private const string TraceUrl = $"{BaseUrl}/cdn-cgi/trace";
    private const string DownloadUrl = $"{BaseUrl}/__down";
    private const string UploadUrl = $"{BaseUrl}/__up";

    public CloudflareSpeedTestService(ILogger<CloudflareSpeedTestService> logger)
    {
        _logger = logger;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes( 2 ) // Allow up to 2 minutes for speed tests
        };
    }

    public async Task<CloudflareSpeedTestResult> RunSpeedTestAsync(CancellationToken cancellationToken = default)
    {
        using var _ = HelperLib.BeginMethodScope();

        _logger.LogInformation( "Starting Cloudflare speed test" );

        var result = new CloudflareSpeedTestResult
        {
            Timestamp = DateTime.UtcNow
        };

        try
        {
            // Get server information
            result.Server = await GetServerInfoAsync( cancellationToken );
            _logger.LogInformation( "Connected to Cloudflare edge: {Colo} ({Location})", result.Server.Colo, result.Server.Location );

            // Measure ping/latency with a small request
            result.Ping = await MeasurePingAsync( cancellationToken );
            _logger.LogInformation( "Ping: {Latency}ms", result.Ping.Latency );

            // Measure download speed
            result.Download = await MeasureDownloadAsync( cancellationToken );
            _logger.LogInformation( "Download: {Speed:F2} Mbps", result.Download.Bandwidth / 1_000_000.0 * 8 );

            // Measure upload speed
            result.Upload = await MeasureUploadAsync( cancellationToken );
            _logger.LogInformation( "Upload: {Speed:F2} Mbps", result.Upload.Bandwidth / 1_000_000.0 * 8 );

            result.IsSuccess = true;
        }
        catch ( Exception ex )
        {
            _logger.LogError( ex, "Speed test failed" );
            result.Error = ex.Message;
            result.IsSuccess = false;
        }

        return result;
    }

    private async Task<CloudflareServerInfo> GetServerInfoAsync(CancellationToken cancellationToken)
    {
        var response = await _httpClient.GetStringAsync( TraceUrl, cancellationToken );
        var lines = response.Split( '\n', StringSplitOptions.RemoveEmptyEntries );

        var serverInfo = new CloudflareServerInfo();

        foreach ( var line in lines )
        {
            var parts = line.Split( '=', 2 );
            if ( parts.Length == 2 )
            {
                var key = parts[0].Trim();
                var value = parts[1].Trim();

                switch ( key )
                {
                    case "colo":
                        serverInfo.Colo = value;
                        break;
                    case "loc":
                        serverInfo.Location = value;
                        break;
                    case "ip":
                        serverInfo.ClientIp = value;
                        break;
                    case "ts":
                        if ( double.TryParse( value, out var timestamp ) )
                        {
                            serverInfo.ServerTimestamp = DateTimeOffset.FromUnixTimeMilliseconds( (long)(timestamp * 1000) );
                        }
                        break;
                }
            }
        }

        return serverInfo;
    }

    private async Task<CloudflarePingResult> MeasurePingAsync(CancellationToken cancellationToken)
    {
        const int pingCount = 5;
        var latencies = new List<double>();

        for ( int i = 0; i < pingCount; i++ )
        {
            var stopwatch = Stopwatch.StartNew();
            await _httpClient.GetStringAsync( $"{TraceUrl}?t={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}", cancellationToken );
            stopwatch.Stop();

            latencies.Add( stopwatch.Elapsed.TotalMilliseconds );

            if ( i < pingCount - 1 ) // Don't delay after the last ping
            {
                await Task.Delay( 100, cancellationToken ); // Brief delay between pings
            }
        }

        return new CloudflarePingResult
        {
            Latency = latencies.Average(),
            Jitter = CalculateJitter( latencies ),
            High = latencies.Max(),
            Low = latencies.Min()
        };
    }

    private static double CalculateJitter(IList<double> latencies)
    {
        if ( latencies.Count < 2 ) return 0;

        var differences = new List<double>();
        for ( int i = 1; i < latencies.Count; i++ )
        {
            differences.Add( Math.Abs( latencies[i] - latencies[i - 1] ) );
        }

        return differences.Average();
    }

    private async Task<CloudflareSpeedResult> MeasureDownloadAsync(CancellationToken cancellationToken)
    {
        using var _ = HelperLib.BeginMethodScope();

        // Progressive download test - start small and increase size
        var testSizes = new[] { 1_000_000, 10_000_000, 50_000_000, 250_000_000, 1_000_000_000, 2_000_000_000 }; // 1MB, 10MB, 50MB, 250MB, 1GB, 2GB
        var bestSpeed = 0.0;

        foreach ( var size in testSizes )
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                var response = await _httpClient.GetAsync( $"{DownloadUrl}?bytes={size}", cancellationToken );
                var data = await response.Content.ReadAsByteArrayAsync( cancellationToken );
                stopwatch.Stop();

                if ( data.Length == size ) // Verify we got the expected amount of data
                {
                    var speed = size / stopwatch.Elapsed.TotalSeconds;
                    bestSpeed = Math.Max( bestSpeed, speed );
                    _logger.LogInformation( "Download test {Size}MB: {Speed:F2} Mbps", size / 1_000_000.0, speed / 1_000_000.0 * 8 );
                }
            }
            catch ( Exception ex )
            {
                _logger.LogWarning( ex, "Download test failed for size {Size}", size );
                break; // Don't try larger sizes if smaller ones fail
            }
        }

        _logger.LogInformation( "Download - bestSpeed: {Speed:F2} Mbps", bestSpeed / 1_000_000.0 * 8 );

        return new CloudflareSpeedResult
        {
            Bandwidth = (long)bestSpeed
        };
    }

    private async Task<CloudflareSpeedResult> MeasureUploadAsync(CancellationToken cancellationToken)
    {
        using var _ = HelperLib.BeginMethodScope();

        // Progressive upload test
        var testSizes = new[] { 1_000_000, 10_000_000, 50_000_000, 250_000_000, 1_000_000_000, 2_000_000_000 }; // 1MB, 10MB, 50MB, 250MB, 1GB, 2GB
        var bestSpeed = 0.0;

        foreach ( var size in testSizes )
        {
            try
            {
                var data = new byte[size];
                // Fill with random data to prevent compression
                new Random().NextBytes( data );

                var content = new ByteArrayContent( data );
                content.Headers.Add( "Content-Type", "application/octet-stream" );

                var stopwatch = Stopwatch.StartNew();
                var response = await _httpClient.PostAsync( UploadUrl, content, cancellationToken );
                stopwatch.Stop();

                if ( response.IsSuccessStatusCode )
                {
                    var speed = size / stopwatch.Elapsed.TotalSeconds;
                    bestSpeed = Math.Max( bestSpeed, speed );
                    _logger.LogInformation( "Upload test {Size}MB: {Speed:F2} Mbps", size / 1_000_000.0, speed / 1_000_000.0 * 8 );
                }
            }
            catch ( Exception ex )
            {
                _logger.LogWarning( ex, "Upload test failed for size {Size}", size );
                break;
            }
        }
        _logger.LogInformation( "Upload - bestSpeed: {Speed:F2} Mbps", bestSpeed / 1_000_000.0 * 8 );

        return new CloudflareSpeedResult
        {
            Bandwidth = (long)bestSpeed
        };
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}

// Result classes matching your existing JSON structure
public class CloudflareSpeedTestResult
{
    public DateTime Timestamp { get; set; }
    public CloudflareServerInfo Server { get; set; } = new();
    public CloudflarePingResult Ping { get; set; } = new();
    public CloudflareSpeedResult Download { get; set; } = new();
    public CloudflareSpeedResult Upload { get; set; } = new();
    public bool IsSuccess { get; set; }
    public string? Error { get; set; }

    // Convert to JSON format compatible with your existing Ookla structure
    public string ToOoklaCompatibleJson()
    {
        var ooklaFormat = new
        {
            type = "result",
            timestamp = Timestamp.ToString( "yyyy-MM-ddTHH:mm:ss.fffZ" ),
            ping = new
            {
                jitter = Ping.Jitter,
                latency = Ping.Latency,
                high = Ping.High,
                low = Ping.Low
            },
            download = new
            {
                bandwidth = Download.Bandwidth,
                bytes = Download.Bandwidth * 8, // Estimate bytes transferred
                elapsed = 8000 // Estimate in milliseconds
            },
            upload = new
            {
                bandwidth = Upload.Bandwidth,
                bytes = Upload.Bandwidth * 5,
                elapsed = 5000
            },
            result = new
            {
                id = "cloudflare",
                url = $"Cloudflare - {Server.Location}"
            },
            server = new
            {
                id = 999999,
                host = "speed.cloudflare.com",
                port = 443,
                name = $"Cloudflare - {Server.Location}",
                location = Server.Location,
                country = Server.Location,
                ip = "104.16.123.96" // Generic Cloudflare IP
            }
        };

        return JsonSerializer.Serialize( ooklaFormat, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        } );
    }
}

public class CloudflareServerInfo
{
    public string Colo { get; set; } = "";
    public string Location { get; set; } = "";
    public string ClientIp { get; set; } = "";
    public DateTimeOffset ServerTimestamp { get; set; }
}

public class CloudflarePingResult
{
    public double Latency { get; set; }
    public double Jitter { get; set; }
    public double High { get; set; }
    public double Low { get; set; }
}

public class CloudflareSpeedResult
{
    public long Bandwidth { get; set; }
}
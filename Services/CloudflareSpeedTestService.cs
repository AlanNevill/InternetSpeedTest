using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace InternetSpeedTest.Services;

public class CloudflareSpeedTestService(
    ILogger<CloudflareSpeedTestService> logger,
    IConfiguration configuration) : IDisposable
{
    private readonly HttpClient _httpClient = CreateHttpClient( configuration );
    private readonly int _parallelConnections = configuration.GetValue( "SpeedTest:Cloudflare:ParallelConnections", 4 );
    private readonly int _testDurationSeconds = configuration.GetValue( "SpeedTest:Cloudflare:TestDurationSeconds", 10 );
    private readonly int _warmupDurationSeconds = configuration.GetValue( "SpeedTest:Cloudflare:WarmupDurationSeconds", 2 );
    private readonly double _lowSpeedWarningMbps = configuration.GetValue( "SpeedTest:LowSpeedWarning", 100.0 );

    private const string BaseUrl = "https://speed.cloudflare.com";
    private const string TraceUrl = $"{BaseUrl}/cdn-cgi/trace";
    private const string DownloadUrl = $"{BaseUrl}/__down";
    private const string UploadUrl = $"{BaseUrl}/__up";
    private const int BufferSize = 1024 * 1024; // 1MB buffer for streaming upload test

    private static HttpClient CreateHttpClient(IConfiguration configuration)
    {
        var parallelConnections = configuration.GetValue( "SpeedTest:Cloudflare:ParallelConnections", 4 );

        // Create optimized HTTP handler
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes( 5 ),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes( 2 ),
            MaxConnectionsPerServer = parallelConnections * 2, // Allow extra connections
            EnableMultipleHttp2Connections = true,
            UseCookies = false, // Disable cookies for performance
            UseProxy = false, // Bypass proxy for accurate testing
            ConnectTimeout = TimeSpan.FromSeconds( 10 ),
            ResponseDrainTimeout = TimeSpan.FromSeconds( 5 )
        };

        var client = new HttpClient( handler )
        {
            Timeout = TimeSpan.FromMinutes( 5 ),
            DefaultRequestVersion = HttpVersion.Version20, // Prefer HTTP/2
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower
        };

        // Set optimized headers
        client.DefaultRequestHeaders.Add( "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36" );
        client.DefaultRequestHeaders.Add( "Accept", "*/*" );
        client.DefaultRequestHeaders.Add( "Origin", "https://speed.cloudflare.com" );
        client.DefaultRequestHeaders.Add( "Referer", "https://speed.cloudflare.com/" );

        return client;
    }

    public async Task<CloudflareSpeedTestResult> RunSpeedTestAsync(CancellationToken cancellationToken = default)
    {
        using var _ = HelperLib.BeginMethodScope();

        logger.LogInformation( "Starting Cloudflare speed test" );

        var result = new CloudflareSpeedTestResult
        {
            Timestamp = DateTime.UtcNow
        };

        try
        {
            // Get server information
            result.Server = await GetServerInfoAsync( cancellationToken );
            logger.LogInformation( "Connected to Cloudflare edge: {Colo} ({Location})", result.Server.Colo, result.Server.Location );

            // Measure ping/latency with a small request
            result.Ping = await MeasurePingAsync( cancellationToken );
            logger.LogInformation( "Ping: {Latency}ms", result.Ping.Latency );

            // Measure download speed
            result.Download = await MeasureDownloadAsync( cancellationToken );
            var downloadSpeedMbps = result.Download.Bandwidth / 1_000_000.0 * 8;
            logger.LogInformation( "Download: {Speed:F2} Mbps", downloadSpeedMbps );
            if ( downloadSpeedMbps < _lowSpeedWarningMbps )
            {
                logger.LogWarning( "Download speed {Speed:F2} Mbps is below threshold of {Threshold:F2} Mbps", downloadSpeedMbps, _lowSpeedWarningMbps );
            }

            // Measure upload speed
            result.Upload = await MeasureUploadAsync( cancellationToken );
            var uploadSpeedMbps = result.Upload.Bandwidth / 1_000_000.0 * 8;
            logger.LogInformation( "Upload: {Speed:F2} Mbps", uploadSpeedMbps );
            if ( uploadSpeedMbps > 0 && uploadSpeedMbps < _lowSpeedWarningMbps )
            {
                logger.LogWarning( "Upload speed {Speed:F2} Mbps is below threshold of {Threshold:F2} Mbps", uploadSpeedMbps, _lowSpeedWarningMbps );
            }

            result.IsSuccess = true;
        }
        catch ( Exception ex )
        {
            logger.LogError( ex, "Speed test failed" );
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
        List<double> latencies = [];  // Collection expression (C# 12+)

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

        List<double> differences = [];  // Collection expression
        for ( int i = 1; i < latencies.Count; i++ )
        {
            differences.Add( Math.Abs( latencies[i] - latencies[i - 1] ) );
        }

        return differences.Average();
    }

    private async Task<CloudflareSpeedResult> MeasureDownloadAsync(CancellationToken cancellationToken)
    {
        using var _ = HelperLib.BeginMethodScope();


        // Warmup phase - establish connections and overcome TCP slow-start
        await WarmupConnectionsAsync( isDownload: true, cancellationToken );

        // Use a large size that will ensure we test for the full duration
        const long testSizePerConnection = 500_000_000L; // 500MB per connection

        logger.LogInformation( "Starting parallel download test with {Connections} connections and file size {testSizePerConnection:N0}", _parallelConnections, testSizePerConnection );

        var testStopwatch = Stopwatch.StartNew();

        // Create parallel download tasks
        var downloadTasks = Enumerable.Range( 0, _parallelConnections )
            .Select( connectionId => DownloadStreamAsync( connectionId, testSizePerConnection, testStopwatch, cancellationToken ) )
            .ToArray();

        // Wait for all downloads to complete or timeout
        var completedTasks = await Task.WhenAll( downloadTasks );
        testStopwatch.Stop();

        // Sum up bytes transferred from all connections
        long totalBytesTransferred = completedTasks.Sum();

        var totalSpeed = totalBytesTransferred / testStopwatch.Elapsed.TotalSeconds;

        logger.LogInformation( "Download completed: {BytesTransferred:N0} bytes in {Duration:F2}s = {Speed:F2} Mbps",
            totalBytesTransferred, testStopwatch.Elapsed.TotalSeconds, totalSpeed / 1_000_000.0 * 8 );

        return new CloudflareSpeedResult
        {
            Bandwidth = (long)totalSpeed
        };
    }

    private async Task<long> DownloadStreamAsync(int connectionId, long maxBytes, Stopwatch testStopwatch, CancellationToken cancellationToken)
    {
        var totalBytesRead = 0L;
        var buffer = new byte[BufferSize];

        try
        {
            var requestUri = $"{DownloadUrl}?bytes={maxBytes}&connection={connectionId}";
            using var response = await _httpClient.GetAsync( requestUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken );

            if ( !response.IsSuccessStatusCode )
            {
                logger.LogError( "Connection {ConnectionId}: HTTP {StatusCode}", connectionId, response.StatusCode );
                return 0;
            }

            using var stream = await response.Content.ReadAsStreamAsync( cancellationToken );

            while ( testStopwatch.Elapsed.TotalSeconds < _testDurationSeconds && !cancellationToken.IsCancellationRequested )
            {
                var bytesRead = await stream.ReadAsync( buffer, 0, buffer.Length, cancellationToken );
                if ( bytesRead == 0 ) break;

                totalBytesRead += bytesRead;
            }
        }
        catch ( Exception ex )
        {
            logger.LogWarning( ex, "Download stream {ConnectionId} failed after {Bytes} bytes", connectionId, totalBytesRead );
        }

        return totalBytesRead;
    }

    private async Task<CloudflareSpeedResult> MeasureUploadAsync(CancellationToken cancellationToken)
    {
        using var _ = HelperLib.BeginMethodScope();

        logger.LogInformation( "Starting parallel upload test with {Connections} connections", _parallelConnections );

        // Warmup phase - establish connections
        await WarmupConnectionsAsync( isDownload: false, cancellationToken );
        
        var testStopwatch = Stopwatch.StartNew();

        // Create parallel upload tasks
        var uploadTasks = Enumerable.Range( 0, _parallelConnections )
            .Select( connectionId => UploadStreamAsync( connectionId, testStopwatch, cancellationToken ) )
            .ToArray();

        // Wait for all uploads to complete or timeout
        var completedTasks = await Task.WhenAll( uploadTasks );
        testStopwatch.Stop();

        // Sum up bytes transferred from all connections
        long totalBytesTransferred = completedTasks.Sum();

        var totalSpeed = totalBytesTransferred / testStopwatch.Elapsed.TotalSeconds;

        logger.LogInformation( "Upload completed: {BytesTransferred:N0} bytes in {Duration:F2}s = {Speed:F2} Mbps",
            totalBytesTransferred, testStopwatch.Elapsed.TotalSeconds, totalSpeed / 1_000_000.0 * 8 );

        return new CloudflareSpeedResult
        {
            Bandwidth = (long)totalSpeed
        };
    }

    private async Task<long> UploadStreamAsync(int connectionId, Stopwatch testStopwatch, CancellationToken cancellationToken)
    {
        var totalBytesUploaded = 0L;
        var chunkSize = BufferSize; // 1MB chunks

        try
        {
            while ( testStopwatch.Elapsed.TotalSeconds < _testDurationSeconds && !cancellationToken.IsCancellationRequested )
            {
                // Generate random data to prevent compression
                var data = new byte[chunkSize];
                Random.Shared.NextBytes( data );

                var content = new ByteArrayContent( data );
                content.Headers.Add( "Content-Type", "application/octet-stream" );
                content.Headers.Add( "X-Connection-Id", connectionId.ToString() );

                var response = await _httpClient.PostAsync( UploadUrl, content, cancellationToken );

                if ( response.IsSuccessStatusCode )
                {
                    totalBytesUploaded += chunkSize;
                }
                else
                {
                    logger.LogError( "Upload chunk failed for ConnectionId: {ConnectionId}, response.StatusCode: {StatusCode}", connectionId, response.StatusCode );
                    break;
                }
            }
        }
        catch ( Exception ex )
        {
            logger.LogError( ex, "Upload stream {ConnectionId} failed after {Bytes} bytes", connectionId, totalBytesUploaded );
        }

        return totalBytesUploaded;
    }

    private async Task WarmupConnectionsAsync(bool isDownload, CancellationToken cancellationToken)
    {
        logger.LogInformation( "Warming up connections for {TestType}...", isDownload ? "download" : "upload" );

        var warmupTasks = new List<Task>();

        for ( int i = 0; i < _parallelConnections; i++ )
        {
            if ( isDownload )
            {
                // Small download to establish connection
                warmupTasks.Add( WarmupDownloadConnectionAsync( i, cancellationToken ) );
            }
            else
            {
                // Small upload to establish connection
                warmupTasks.Add( WarmupUploadConnectionAsync( i, cancellationToken ) );
            }
        }

        try
        {
            await Task.WhenAll( warmupTasks );
            logger.LogInformation( "Connection warmup completed." );
        }
        catch ( Exception ex )
        {
            logger.LogError( ex, "Some warmup connections failed, continuing with test." );
        }
    }

    private async Task WarmupDownloadConnectionAsync(int connectionId, CancellationToken cancellationToken)
    {
        try
        {
            var warmupSize = 200_000; // 200KB warmup
            var requestUri = $"{DownloadUrl}?bytes={warmupSize}&warmup={connectionId}";
            using var response = await _httpClient.GetAsync( requestUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken );
            using var stream = await response.Content.ReadAsStreamAsync( cancellationToken );

            var buffer = new byte[8192];
            var totalRead = 0;
            var startTime = Stopwatch.GetTimestamp();

            while ( totalRead < warmupSize && Stopwatch.GetElapsedTime( startTime ).TotalSeconds < _warmupDurationSeconds )
            {
                var bytesRead = await stream.ReadAsync( buffer, 0, buffer.Length, cancellationToken );
                if ( bytesRead == 0 ) break;
                totalRead += bytesRead;
            }
        }
        catch ( Exception ex )
        {
            logger.LogError( ex, "WarmupDownloadConnectionAsync - Warmup download connection {ConnectionId} failed", connectionId );
        }
    }

    private async Task WarmupUploadConnectionAsync(int connectionId, CancellationToken cancellationToken)
    {
        try
        {
            var warmupData = new byte[50_000]; // 50KB warmup
            Random.Shared.NextBytes( warmupData );

            var content = new ByteArrayContent( warmupData );
            content.Headers.Add( "Content-Type", "application/octet-stream" );
            content.Headers.Add( "X-Warmup", connectionId.ToString() );

            using var response = await _httpClient.PostAsync( UploadUrl, content, cancellationToken );
            // Just ensure the request completes successfully
        }
        catch ( Exception ex )
        {
            logger.LogError( ex, "WarmupUploadConnectionAsync - Warmup upload connection {ConnectionId} failed", connectionId );
        }
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
                bandwidth = (int)Math.Min( Download.Bandwidth, int.MaxValue ), // Cast to int with overflow protection
                bytes = (int)Math.Min( Download.Bandwidth * 8, int.MaxValue ),
                elapsed = 8000
            },
            upload = new
            {
                bandwidth = (int)Math.Min( Upload.Bandwidth, int.MaxValue ), // Cast to int with overflow protection
                bytes = (int)Math.Min( Upload.Bandwidth * 5, int.MaxValue ),
                elapsed = 5000
            },
            isp = "Unknown", // Required property for deserialization
            @interface = new // Required property for deserialization
            {
                internalIp = Server.ClientIp ?? "Unknown",
                name = "Unknown",
                macAddr = "Unknown",
                isVpn = false,
                externalIp = Server.ClientIp ?? "Unknown"
            },
            result = new
            {
                id = "cloudflare",
                url = $"Revised 2025-09-19. Cloudflare - {Server.Colo} {Server.Location}",
                persisted = true
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
    public string Colo { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string ClientIp { get; set; } = string.Empty;
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
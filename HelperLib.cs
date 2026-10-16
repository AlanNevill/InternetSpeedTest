using HtmlAgilityPack;

using InternetSpeedTest.DataModels;

// Removed ILogger injection; helper now purely static.
using Serilog;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace InternetSpeedTest;

public static class HelperLib
{
    /// <summary>
    /// Helper method to create method-named scopes for logging throughout the application
    /// </summary>
    public static IDisposable BeginMethodScope([CallerMemberName] string methodName = "")
    {
        return Serilog.Context.LogContext.PushProperty( "MethodName", methodName );
    }

    /// <summary>
    /// Push both Serilog SourceContext and MethodName into the LogContext for the duration of the scope
    /// </summary>
    public static IDisposable BeginMethodScopeLocal(string sourceContext, [CallerMemberName] string methodName = "")
    {
        var source = Serilog.Context.LogContext.PushProperty( "SourceContext", sourceContext );
        var method = Serilog.Context.LogContext.PushProperty( "MethodName", methodName );
        return new CompositeDisposable( source, method );
    }

    /// <summary>
    /// Formats the daily summary email HTML. Optionally embeds a drive health table if provided.
    /// </summary>
    /// <param name="vGigaClearByDay">Daily speed aggregation row</param>
    /// <param name="driveTableHtml">Optional pre-rendered drive health HTML table from FormatEmailDrives()</param>
    public static string FormatEmailForAcs(VGigaClearByDay vGigaClearByDay)
    {
        var yesterday = DateTime.Today.AddDays( -1 );
        var sb = new StringBuilder();
        sb.AppendLine( "<!DOCTYPE html>" )
          .AppendLine( "<html>" )
          .AppendLine( "<head>" )
          .AppendLine( "    <meta charset='utf-8'>" )
          .AppendLine( "    <title>Daily Internet Speed Test Report</title>" )
          .AppendLine( "    <style>" )
          .AppendLine( "        body { font-family: 'Segoe UI', Arial, sans-serif; margin: 20px; }" )
          .AppendLine( "        .header { background-color: #f0f0f0; padding: 10px 14px; border: 1px solid #ccc; margin-bottom: 20px; }" )
          .AppendLine( "        .section { margin-bottom: 28px; }" )
          .AppendLine( "        .log-content { background-color: #f9f9f9; padding: 15px; border: 1px solid #ddd; white-space: pre-wrap; font-family: 'Courier New', monospace; }" )
          .AppendLine( "        .footer { margin-top: 30px; font-size: 0.85em; color: #666; }" )
          .AppendLine( "        table { margin-top: 8px; }" )
          .AppendLine( "        h3 { margin-bottom: 6px; }" )
          .AppendLine( "    </style>" )
          .AppendLine( "</head>" )
          .AppendLine( "<body>" )
          .AppendLine( "    <div class='header'>" )
          .AppendLine( "        <h2 style='margin:0;'>Daily Internet Speed Test Report</h2>" )
          .AppendLine( $"        <p style='margin:4px 0 0 0;'><strong>Date:</strong> {yesterday:yyyy-MM-dd} | <strong>Generated:</strong> {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>" )
          .AppendLine( "    </div>" )
          .AppendLine( "    <div class='section'>" )
          .AppendLine( "        <h3 style='font-family:Segoe UI,Arial;margin:0 0 8px 0;'>Speed Summary</h3>" )
          .AppendLine( "        <div class='log-content'>" );

        var emailBody = vGigaClearByDay != null
            ? new StringBuilder()
                .AppendLine( $"Date: {vGigaClearByDay.SmallDate}" )
                .AppendLine( $"Number of Samples: {vGigaClearByDay.NumSamples}</br>" )
                .AppendLine( "<table style='border-collapse:collapse;font-family:Segoe UI,Arial;font-size:12px;margin-top:8px;'>" )
                .AppendLine( "<thead>" )
                .AppendLine( "<tr style='background:#f0f0f0'>" )
                .AppendLine( "<th style='border:1px solid #ccc;padding:4px 8px;text-align:left;'>Measure (Mbps)</th>" )
                .AppendLine( "<th style='border:1px solid #ccc;padding:4px 8px;text-align:right;'>Avg</th>" )
                .AppendLine( "<th style='border:1px solid #ccc;padding:4px 8px;text-align:right;'>Stdv</th>" )
                .AppendLine( "</tr>" )
                .AppendLine( "</thead>" )
                .AppendLine( "<tbody>" )
                .AppendLine( "<tr>" )
                .AppendLine( "<td style='border:1px solid #ccc;padding:4px 8px;'>Download Speed</td>" )
                .AppendLine( $"<td style='border:1px solid #ccc;padding:4px 8px;text-align:right;font-weight: bold;'>{vGigaClearByDay.AvgDownMbps:N0}</td>" )
                .AppendLine( $"<td style='border:1px solid #ccc;padding:4px 8px;text-align:right;font-weight: bold;'>{vGigaClearByDay.StdDownMbps:N0}</td>" )
                .AppendLine( "</tr>" )
                .AppendLine( "<tr>" )
                .AppendLine( "<td style='border:1px solid #ccc;padding:4px 8px;'>Upload Speed</td>" )
                .AppendLine( $"<td style='border:1px solid #ccc;padding:4px 8px;text-align:right;font-weight: bold;'>{vGigaClearByDay.AvgUpMbps:N0}</td>" )
                .AppendLine( $"<td style='border:1px solid #ccc;padding:4px 8px;text-align:right;font-weight: bold;'>{vGigaClearByDay.StdUpMbps:N0}</td>" )
                .AppendLine( "</tr>" )
                .AppendLine( "</tbody>" )
                .AppendLine( "</table>" )
                .ToString()
            : "No data available for the specified date.";

        //var escapedContent = WebUtility.HtmlEncode( emailBody );
        sb.AppendLine( emailBody )
          .AppendLine( "        </div>" )
          .AppendLine( "    </div>" );

        sb.AppendLine( "    <div class='footer'>" )
          .AppendLine( "        <p>This is an automated report from Internet Speed Test Application.</p>" )
          .AppendLine( "    </div>" )
          .AppendLine( "</body>" )
          .AppendLine( "</html>" );

        return sb.ToString();
    }

    public sealed record DriveHealthRow(string Drive, double TotalGB, double FreeGB, double PctFree, bool LowSpace);

    public static string FormatEmailDrives(IEnumerable<DriveHealthRow> rows, string? title = null)
    {
        var list = rows?.ToList() ?? new List<DriveHealthRow>();
        var sb = new StringBuilder();

        if ( !string.IsNullOrWhiteSpace( title ) )
        {
            sb.AppendLine( $"<h3 style='font-family:Segoe UI,Arial;margin:8px 0;'>{WebUtility.HtmlEncode( title )}</h3>" );
        }

        sb.AppendLine( "<table style='border-collapse:collapse;font-family:Segoe UI,Arial;font-size:12px;'>" )
          .AppendLine( "<thead>" )
          .AppendLine( "<tr style='background:#f0f0f0'>" )
          .AppendLine( "<th style='border:1px solid #ccc;padding:4px 8px;text-align:left;'>Drive</th>" )
          .AppendLine( "<th style='border:1px solid #ccc;padding:4px 8px;text-align:right;'>Total (GB)</th>" )
          .AppendLine( "<th style='border:1px solid #ccc;padding:4px 8px;text-align:right;'>Free (GB)</th>" )
          .AppendLine( "<th style='border:1px solid #ccc;padding:4px 8px;text-align:right;'>Free %</th>" )
          .AppendLine( "<th style='border:1px solid #ccc;padding:4px 8px;text-align:center;'>Status</th>" )
          .AppendLine( "</tr>" )
          .AppendLine( "</thead>" )
          .AppendLine( "<tbody>" );

        if ( list.Count == 0 )
        {
            sb.AppendLine( "<tr><td colspan='5' style='border:1px solid #ccc;padding:6px;font-style:italic;'>No drive data</td></tr>" );
        }
        else
        {
            foreach ( var r in list )
            {
                var status = r.LowSpace ? "LOW" : "OK";
                var rowColor = r.LowSpace ? "#ffe5e5" : "#ffffff";
                sb.AppendLine( $"<tr style='background:{rowColor};'>" )
                  .AppendLine( $"<td style='border:1px solid #ccc;padding:4px 8px;'>{WebUtility.HtmlEncode( r.Drive )}</td>" )
                  .AppendLine( $"<td style='border:1px solid #ccc;padding:4px 8px;text-align:right;'>{r.TotalGB:N2}</td>" )
                  .AppendLine( $"<td style='border:1px solid #ccc;padding:4px 8px;text-align:right;'>{r.FreeGB:N2}</td>" )
                  .AppendLine( $"<td style='border:1px solid #ccc;padding:4px 8px;text-align:right;'>{r.PctFree:N1}</td>" )
                  .AppendLine( $"<td style='border:1px solid #ccc;padding:4px 8px;text-align:center;font-weight:{(r.LowSpace ? "600" : "400")};color:{(r.LowSpace ? "#b30000" : "#006400")};'>{status}</td>" )
                  .AppendLine( "</tr>" );
            }
        }

        sb.AppendLine( "</tbody>" )
          .AppendLine( "</table>" );

        return sb.ToString();
    }

    public static string HtmlToText(string html)
    {
        if ( string.IsNullOrWhiteSpace( html ) ) return string.Empty;
        var doc = new HtmlDocument();
        doc.LoadHtml( html );

        var removeNodes = doc.DocumentNode.SelectNodes( "//script|//style" );
        if ( removeNodes != null ) foreach ( var n in removeNodes ) n.Remove();

        var brs = doc.DocumentNode.SelectNodes( "//br" );
        if ( brs != null ) foreach ( var br in brs ) br.ParentNode.ReplaceChild( doc.CreateTextNode( "\n" ), br );

        var blocks = doc.DocumentNode.SelectNodes( "//p|//div|//li|//h1|//h2|//h3|//h4|//h5|//h6|//tr" );
        if ( blocks != null ) foreach ( var b in blocks ) b.InnerHtml += "\n";

        var text = WebUtility.HtmlDecode( doc.DocumentNode.InnerText );
        var sb = new StringBuilder();
        bool previousBlank = false;
        foreach ( var raw in text.Replace( "\r", string.Empty ).Split( '\n' ) )
        {
            var line = raw.TrimEnd();
            bool isBlank = string.IsNullOrWhiteSpace( line );
            if ( isBlank ) { if ( !previousBlank ) { sb.AppendLine(); previousBlank = true; } }
            else { sb.AppendLine( line.Trim() ); previousBlank = false; }
        }
        return sb.ToString().TrimEnd();
    }

}

// File-scoped helper class (C# 11+)
file sealed class CompositeDisposable(IDisposable first, IDisposable second) : IDisposable
{
    private bool _disposed;
    
    public void Dispose()
    {
        if ( _disposed ) return;
        _disposed = true;
        try { second.Dispose(); }
        finally { first.Dispose(); }
    }
}

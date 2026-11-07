using HtmlAgilityPack;

using InternetSpeedTest.DataModels;

using System;
using System.Net;
using System.Text;

namespace InternetSpeedTest.Services;

/// <summary>
/// Builds the daily summary email (HTML and plain-text bodies) from a <see cref="VGigaClearByDay"/> row.
/// </summary>
internal static class DailyReportBuilder
{
    /// <summary>
    /// Formats the daily summary email HTML.
    /// </summary>
    /// <param name="summary">Daily speed aggregation row, or null if there is no data for the day</param>
    /// <param name="reportDate">The day being reported. Passed in so the header always agrees with the
    /// date the caller used for the query and the subject line.</param>
    public static string FormatHtml(VGigaClearByDay? summary, DateTime reportDate)
    {
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
          .AppendLine( $"        <p style='margin:4px 0 0 0;'><strong>Date:</strong> {reportDate:yyyy-MM-dd} | <strong>Generated:</strong> {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>" )
          .AppendLine( "    </div>" )
          .AppendLine( "    <div class='section'>" )
          .AppendLine( "        <h3 style='font-family:Segoe UI,Arial;margin:0 0 8px 0;'>Speed Summary</h3>" )
          .AppendLine( "        <div class='log-content'>" );

        var emailBody = summary != null
            ? new StringBuilder()
                .AppendLine( $"Date: {summary.SmallDate}" )
                .AppendLine( $"Number of Samples: {summary.NumSamples}</br>" )
                .AppendLine( summary.NumDownFailed > 0 || summary.NumUpFailed > 0
                    ? $"Failed measurements (excluded from averages): download {summary.NumDownFailed}, upload {summary.NumUpFailed}</br>"
                    : string.Empty )
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
                .AppendLine( $"<td style='border:1px solid #ccc;padding:4px 8px;text-align:right;font-weight: bold;'>{summary.AvgDownMbps:N0}</td>" )
                .AppendLine( $"<td style='border:1px solid #ccc;padding:4px 8px;text-align:right;font-weight: bold;'>{summary.StdDownMbps:N0}</td>" )
                .AppendLine( "</tr>" )
                .AppendLine( "<tr>" )
                .AppendLine( "<td style='border:1px solid #ccc;padding:4px 8px;'>Upload Speed</td>" )
                .AppendLine( $"<td style='border:1px solid #ccc;padding:4px 8px;text-align:right;font-weight: bold;'>{summary.AvgUpMbps:N0}</td>" )
                .AppendLine( $"<td style='border:1px solid #ccc;padding:4px 8px;text-align:right;font-weight: bold;'>{summary.StdUpMbps:N0}</td>" )
                .AppendLine( "</tr>" )
                .AppendLine( "</tbody>" )
                .AppendLine( "</table>" )
                .ToString()
            : "No data available for the specified date.";

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

    /// <summary>
    /// Converts the HTML report to the plain-text alternative body.
    /// </summary>
    public static string ToPlainText(string html)
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

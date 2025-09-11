using HtmlAgilityPack;

using InternetSpeedTest.DataModels;
using InternetSpeedTest.DataModels.Emailer;

using Microsoft.Extensions.Logging;

using Serilog;

using System;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace InternetSpeedTest;

public sealed class HelperLib(ILogger<HelperLib> logger)
{
    private readonly ILogger<HelperLib> _logger = logger;

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
    /// Formats the email (HTML) for ACS based on provided log content.
    /// </summary>
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
          .AppendLine( "        body { font-family: 'Courier New', monospace; margin: 20px; }" )
          .AppendLine( "        .header { background-color: #f0f0f0; padding: 10px; border: 1px solid #ccc; margin-bottom: 20px; }" )
          .AppendLine( "        .log-content { background-color: #f9f9f9; padding: 15px; border: 1px solid #ddd; white-space: pre-wrap; }" )
          .AppendLine( "        .footer { margin-top: 20px; font-size: 0.9em; color: #666; }" )
          .AppendLine( "    </style>" )
          .AppendLine( "</head>" )
          .AppendLine( "<body>" )
          .AppendLine( "    <div class='header'>" )
          .AppendLine( "        <h2>Daily Internet Speed Test Report</h2>" )
          .AppendLine( $"        <p><strong>Date:</strong> {yesterday:yyyy-MM-dd}</p>" )
          .AppendLine( $"        <p><strong>Generated:</strong> {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>" )
          .AppendLine( "    </div>" )
          .AppendLine( "    <div class='log-content'>" );

        var emailBody = vGigaClearByDay != null
            ? new StringBuilder()
                .AppendLine( $"Date: {vGigaClearByDay.SmallDate}" )
                .AppendLine( $"Number of Samples: {vGigaClearByDay.NumSamples}" )
                .AppendLine( $"Average Download Speed (Mbps): {vGigaClearByDay.AvgDownMbps:N2}" )
                .AppendLine( $"Download Speed Std Dev (Mbps): {vGigaClearByDay.StdDownMbps:N2}" )
                .AppendLine( $"Average Upload Speed (Mbps): {vGigaClearByDay.AvgUpMbps:N2}" )
                .AppendLine( $"Upload Speed Std Dev (Mbps): {vGigaClearByDay.StdUpMbps:N2}" )
                .ToString()
            : "<br></br>No data available for the specified date.";

        var escapedContent = WebUtility.HtmlEncode( emailBody );

        sb.AppendLine( escapedContent )
          .AppendLine( "    </div>" )
          .AppendLine( "    <div class='footer'>" )
          .AppendLine( "        <p>This is an automated report from Internet Speed Test Application.</p><br></br>" )
          .AppendLine( $"        <p>Log entries: {emailBody.Split( Environment.NewLine, StringSplitOptions.RemoveEmptyEntries ).Length}</p>" )
          .AppendLine( "    </div>" )
          .AppendLine( "</body>" )
          .AppendLine( "</html>" );
        var formatted = sb.ToString();

        return formatted;
    }

    public static string HtmlToText(string html)
    {
        if ( string.IsNullOrWhiteSpace( html ) ) return string.Empty;

        var doc = new HtmlDocument();
        doc.LoadHtml( html );

        // Remove scripts and styles
        var removeNodes = doc.DocumentNode.SelectNodes( "//script|//style" );
        if ( removeNodes != null )
        {
            foreach ( var n in removeNodes )
                n.Remove();
        }

        // Replace <br> with newlines (FIX: removed invalid XPath union with trailing slash that caused XPathException)
        var brs = doc.DocumentNode.SelectNodes( "//br" );
        if ( brs != null )
        {
            foreach ( var br in brs )
                br.ParentNode.ReplaceChild( doc.CreateTextNode( "\n" ), br );
        }

        // Add newline after common block elements to preserve structure
        var blocks = doc.DocumentNode.SelectNodes( "//p|//div|//li|//h1|//h2|//h3|//h4|//h5|//h6|//tr" );
        if ( blocks != null )
        {
            foreach ( var b in blocks )
                b.InnerHtml += "\n";
        }

        var text = doc.DocumentNode.InnerText;
        text = WebUtility.HtmlDecode( text );

        // Normalize whitespace and collapse extra blank lines
        var sb = new StringBuilder();
        bool previousBlank = false;
        foreach ( var raw in text.Replace( "\r", string.Empty ).Split( '\n' ) )
        {
            var line = raw.TrimEnd();
            bool isBlank = string.IsNullOrWhiteSpace( line );
            if ( isBlank )
            {
                if ( !previousBlank )
                {
                    sb.AppendLine();
                    previousBlank = true;
                }
            }
            else
            {
                sb.AppendLine( line.Trim() );
                previousBlank = false;
            }
        }

        return sb.ToString().TrimEnd();
    }

    public static async Task<bool> EmailerService_WriteMessage(string subject, string bodyHtml, string toAddress, Emailer emailerDb)
    {
        using var _ = BeginMethodScope();
        try
        {
            var bodyText = HtmlToText( bodyHtml );

            // Create a new EmailerMessage
            var message = new EmailMessage
            {
                FromAddress = string.Empty,
                Subject = subject,
                BodyText = bodyText,
                BodyHtml = bodyHtml,
                Priority = 1,
                Status = "Queued",
                RetryCount = 0,
                MaxRetries = 3,
                CreatedAt = DateTime.UtcNow,
                ScheduledAt = DateTime.UtcNow,
            };
            Log.Information( "EmailCreate message object initialized" );

            message.EmailRecipients.Add( new EmailRecipient
            {
                EmailAddress = toAddress,
                RecipientType = "To",
                Status = "Pending"
            } );

            emailerDb.EmailMessages.Add( message );
            await emailerDb.SaveChangesAsync();

            Log.Information( "EmailCreate saved message with Id {Id}", message.MessageId );

            return true;
        }
        catch ( Exception ex )
        {
            Log.Error( ex, "EmailCreate submit failed" );
            return false;
        }
    }

    private sealed class CompositeDisposable : IDisposable
    {
        private readonly IDisposable _first;
        private readonly IDisposable _second;
        private bool _disposed;

        public CompositeDisposable(IDisposable first, IDisposable second)
        {
            _first = first;
            _second = second;
        }

        public void Dispose()
        {
            if ( _disposed ) return;
            _disposed = true;
            // Dispose in reverse order of creation
            try { _second.Dispose(); } finally { _first.Dispose(); }
        }
    }

}

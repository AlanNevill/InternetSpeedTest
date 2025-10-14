using Microsoft.Data.SqlClient;
using System;
using System.Data;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace EmailerUtility;

public interface IEmailerClient
{
    System.Threading.Tasks.Task<long> EnqueueAsync(
        string toAddress,
        string subject,
        string bodyHtml,
        string? bodyText = null,
        int priority = 1,
        System.DateTime? scheduledAtUtc = null,
        System.Threading.CancellationToken ct = default);
}

public sealed class EmailerClient : IEmailerClient
{
    private readonly string _connectionString;

    public EmailerClient(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException( nameof( connectionString ) );
    }

    public async System.Threading.Tasks.Task<long> EnqueueAsync(
        string toAddress,
        string subject,
        string bodyHtml,
        string? bodyText = null,
        int priority = 1,
        System.DateTime? scheduledAtUtc = null,
        System.Threading.CancellationToken ct = default)
    {
        if ( string.IsNullOrWhiteSpace( toAddress ) ) throw new ArgumentException( "toAddress is required", nameof( toAddress ) );
        if ( string.IsNullOrWhiteSpace( subject ) ) throw new ArgumentException( "subject is required", nameof( subject ) );
        if ( string.IsNullOrWhiteSpace( bodyHtml ) ) throw new ArgumentException( "bodyHtml is required", nameof( bodyHtml ) );

        bodyText ??= HtmlToText( bodyHtml );

        await using var cn = new SqlConnection( _connectionString );
        await cn.OpenAsync( ct );
        await using var tx = await cn.BeginTransactionAsync( ct );

        try
        {
            const string insertMessageSql = @"
INSERT INTO EmailMessages (FromAddress, Subject, BodyText, BodyHtml, Priority, Status, RetryCount, MaxRetries, CreatedAt, ScheduledAt)
VALUES (@from, @subject, @bodyText, @bodyHtml, @priority, @status, @retryCount, @maxRetries, @createdAt, @scheduledAt);
SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";

            await using var cmd = new SqlCommand( insertMessageSql, cn, (SqlTransaction)tx );
            cmd.Parameters.Add( new SqlParameter( "@from", SqlDbType.NVarChar, 320 ) { Value = "InternetSpeedTest" } );
            cmd.Parameters.Add( new SqlParameter( "@subject", SqlDbType.NVarChar, 512 ) { Value = subject } );
            cmd.Parameters.Add( new SqlParameter( "@bodyText", SqlDbType.NVarChar, -1 ) { Value = (object?)bodyText ?? DBNull.Value } );
            cmd.Parameters.Add( new SqlParameter( "@bodyHtml", SqlDbType.NVarChar, -1 ) { Value = (object?)bodyHtml ?? DBNull.Value } );
            cmd.Parameters.Add( new SqlParameter( "@priority", SqlDbType.Int ) { Value = priority } );
            cmd.Parameters.Add( new SqlParameter( "@status", SqlDbType.NVarChar, 50 ) { Value = "Queued" } );
            cmd.Parameters.Add( new SqlParameter( "@retryCount", SqlDbType.Int ) { Value = 0 } );
            cmd.Parameters.Add( new SqlParameter( "@maxRetries", SqlDbType.Int ) { Value = 3 } );
            cmd.Parameters.Add( new SqlParameter( "@createdAt", SqlDbType.DateTime ) { Value = System.DateTime.UtcNow } );
            cmd.Parameters.Add( new SqlParameter( "@scheduledAt", SqlDbType.DateTime ) { Value = scheduledAtUtc.HasValue ? (object)scheduledAtUtc.Value : DBNull.Value } );

            var result = await cmd.ExecuteScalarAsync( ct );
            var messageId = System.Convert.ToInt64( result );

            const string insertRecipientSql = @"
INSERT INTO EmailRecipients (MessageId, EmailAddress, RecipientType, Status)
VALUES (@messageId, @email, @type, @status);";

            await using var cmdRec = new SqlCommand( insertRecipientSql, cn, (SqlTransaction)tx );
            cmdRec.Parameters.Add( new SqlParameter( "@messageId", SqlDbType.BigInt ) { Value = messageId } );
            cmdRec.Parameters.Add( new SqlParameter( "@email", SqlDbType.NVarChar, 320 ) { Value = toAddress } );
            cmdRec.Parameters.Add( new SqlParameter( "@type", SqlDbType.NVarChar, 10 ) { Value = "To" } );
            cmdRec.Parameters.Add( new SqlParameter( "@status", SqlDbType.NVarChar, 50 ) { Value = "Pending" } );
            await cmdRec.ExecuteNonQueryAsync( ct );

            await tx.CommitAsync( ct );
            return messageId;
        }
        catch
        {
            await tx.RollbackAsync( ct );
            throw;
        }
    }

    private static string HtmlToText(string html)
    {
        if ( string.IsNullOrWhiteSpace( html ) ) return string.Empty;

        var buffer = new char[html.Length];
        int j = 0;
        bool inTag = false;

        for ( int i = 0; i < html.Length; i++ )
        {
            var ch = html[i];
            if ( ch == '<' ) { inTag = true; continue; }
            if ( ch == '>' ) { inTag = false; continue; }
            if ( !inTag ) buffer[j++] = ch;
        }
        return WebUtility.HtmlDecode( new string( buffer, 0, j ) );
    }
}

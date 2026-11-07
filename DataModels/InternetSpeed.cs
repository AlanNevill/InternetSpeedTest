using Microsoft.EntityFrameworkCore;

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InternetSpeedTest.DataModels;

public partial class InternetSpeed
{
    [Key]
    public int Id { get; set; }

    public DateTime? ResultDateTime { get; set; }

    [StringLength( 2048 )]
    [Unicode( false )]
    public string? ResultUrl { get; set; }

    [Column( TypeName = "real" )]
    public double? PingJitter { get; set; }

    [Column( TypeName = "real" )]
    public double? PingLatency { get; set; }

    [Column( TypeName = "real" )]
    public double? PingLow { get; set; }

    [Column( TypeName = "real" )]
    public double? PingHigh { get; set; }

    public int? DownLoadBandwidth { get; set; }

    public int? UploadBandWidth { get; set; }

    [Unicode( false )]
    public string? ResultJson { get; set; }

    /// <summary>NULL = row predates the status columns (treated as good by the views).</summary>
    public bool? DownloadOk { get; set; }

    public bool? UploadOk { get; set; }

    /// <summary>Failed upload requests (timeouts, 5xx, exceptions) during the test.</summary>
    public int? UploadErrors { get; set; }
}

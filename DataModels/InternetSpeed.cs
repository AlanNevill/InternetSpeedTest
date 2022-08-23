using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace InternetSpeedTest.DataModels;

public partial class InternetSpeed
{
    [Key]
    public int Id { get; set; }

    public DateTime? ResultDateTime { get; set; }

    [StringLength(2048)]
    [Unicode(false)]
    public string? ResultUrl { get; set; }

    [Column(TypeName = "real")]
    public double? PingJitter { get; set; }

    [Column(TypeName = "real)")]
    public double? PingLatency { get; set; }

    [Column(TypeName = "real")]
    public double? PingLow { get; set; }

    [Column(TypeName = "real")]
    public double? PingHigh { get; set; }

    public int? DownLoadBandwidth { get; set; }

    public int? UploadBandWidth { get; set; }

    [Unicode(false)]
    public string? ResultJson { get; set; }
}

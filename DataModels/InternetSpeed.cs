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

    [StringLength(50)]
    [Unicode(false)]
    public string? Result { get; set; }

    [Column(TypeName = "decimal(9, 3)")]
    public decimal? PingJitter { get; set; }

    [Column(TypeName = "decimal(9, 3)")]
    public decimal? PingLatency { get; set; }

    [Column(TypeName = "decimal(9, 3)")]
    public decimal? PingLow { get; set; }

    [Column(TypeName = "decimal(9, 3)")]
    public decimal? PingHigh { get; set; }

    public int? DownLoadBandwidth { get; set; }

    public int? UploadBandWidth { get; set; }

    [Unicode(false)]
    public string? ResultJson { get; set; }
}

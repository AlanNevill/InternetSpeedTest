using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace InternetSpeedTest.DataModels;

public partial class InternetSpeedDto
{

    public DateTime? timestamp { get; set; }

    [StringLength(50)]
    [Unicode(false)]
    public string? type { get; set; }

    [Column(TypeName = "decimal(9, 3)")]
    public decimal? Jitter { get; set; }

    [Column(TypeName = "decimal(9, 3)")]
    public decimal? Latency { get; set; }

    [Column(TypeName = "decimal(9, 3)")]
    public decimal? Low { get; set; }

    [Column(TypeName = "decimal(9, 3)")]
    public decimal? High { get; set; }

    public int? DownLoadBandwidth { get; set; }

    public int? UploadBandWidth { get; set; }
}

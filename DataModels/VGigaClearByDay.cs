using Microsoft.EntityFrameworkCore;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InternetSpeedTest.DataModels;

[Keyless]
public partial class VGigaClearByDay
{
    [StringLength( 30 )]
    [Unicode( false )]
    public string? SmallDate { get; set; }

    public int? NumSamples { get; set; }

    [Column( "avgDownMbps", TypeName = "numeric(38, 6)" )]
    public decimal? AvgDownMbps { get; set; }

    [Column( "stdDownMbps" )]
    public double? StdDownMbps { get; set; }

    [Column( "avgUpMbps", TypeName = "numeric(38, 6)" )]
    public decimal? AvgUpMbps { get; set; }

    [Column( "stdUpMbps" )]
    public double? StdUpMbps { get; set; }
}

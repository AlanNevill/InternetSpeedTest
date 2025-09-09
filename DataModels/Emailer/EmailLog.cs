using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace InternetSpeedTest.DataModels.Emailer;

[Index("MessageId", Name = "IX_EmailLogs_MessageId")]
public partial class EmailLog
{
    [Key]
    public long LogId { get; set; }

    public long MessageId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime LogTime { get; set; }

    [StringLength(20)]
    [Unicode(false)]
    public string Level { get; set; } = null!;

    [Column(TypeName = "text")]
    public string Message { get; set; } = null!;

    [ForeignKey("MessageId")]
    [InverseProperty("EmailLogs")]
    public virtual EmailMessage MessageNavigation { get; set; } = null!;
}
